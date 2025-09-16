using FellowOakDicom;
using Rdmp.Core.ReusableLibraryCode.Checks;
using Rdmp.Core.ReusableLibraryCode.Progress;
using Rdmp.Dicom.Extraction.FoDicomBased.DirectoryDecisions;
using Rdmp.Core.DataExport.DataExtraction.Commands;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.DataFlowPipeline.Requirements;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.Repositories.Construction;
using Rdmp.Core.MapsDirectlyToDatabaseTable.Versioning;
using System.Data;
using static FellowOakDicom.DicomAnonymizer;
using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Rdmp.Core.QueryBuilding;
using Amazon.Auth.AccessControlPolicy;

namespace Rdmp.Dicom.Extraction.FoDicomBased;

/// <summary>
/// Goes directly to the referenced file locations (which cannot be in zip files) and runs DicomAnonymizer on the files that are referenced
/// in  <see cref="RelativeArchiveColumnName"/>.
/// </summary>
public partial class FoDicomAnonymiser : IPluginDataFlowComponent<DataTable>, IPipelineRequirement<IExtractCommand>
{
    private IExtractDatasetCommand _extractCommand;

    [DemandsInitialization("If the path filename contains relative file uris to images then this is the root directory")]
    public string ArchiveRootIfAny { get; set; }

    [DemandsInitialization("The column name in the extracted dataset which contains the location of the dicom files", Mandatory = true)]
    public string RelativeArchiveColumnName { get; set; }

    [DemandsInitialization("The mapping database for UID fields", Mandatory = true)]
    public ExternalDatabaseServer UIDMappingServer { get; set; }

    [DemandsInitialization("Determines how dicom files are written to the project output directory", TypeOf = typeof(IPutDicomFilesInExtractionDirectories), Mandatory = true)]
    public Type PutterType { get; set; }

    [DemandsInitialization("Retain Full Dates in dicom tags during anonymisation")]
    public bool RetainDates { get; set; }

    [DemandsInitialization("The number of errors (e.g. failed to find/anonymise file) to allow before abandoning the extraction", DefaultValue = 100)]
    public int ErrorThreshold { get; set; }

    [DemandsInitialization("Comma separated list of top level tags that you want deleted from the dicom dataset of files being extracted.  This field exists to cover any anonymisation gaps e.g. ditching ReferencedImageSequence")]
    public string DeleteTags { get; set; }

    [DemandsInitialization("Number of times to attempt the read again when encountering an Exception", DefaultValue = 0)]
    public int RetryCount { get; set; }

    [DemandsInitialization("Number of milliseconds to wait after encountering an Exception reading before trying", DefaultValue = 100)]
    public int RetryDelay { get; set; }

    [DemandsInitialization("Set to true to skip anonymisation process on structured reports (Modality=SR).  PatientID and UID tags will still be anonymised.", DefaultValue = false)]
    public bool SkipAnonymisationOnStructuredReports { get; set; }

    [DemandsInitialization("Set to true to skip opening/anonymising files and just process the metadata already in the database.", DefaultValue = false)]
    public bool MetadataOnly { get; set; }

    [DemandsInitialization("How many tries to allow for fetching the file. This setting may be useful on network drives or oversubscribed resources", DefaultValue = 0)]

    public int FileFetchRetryLimit { get; set; }

    [DemandsInitialization("How long to wait between file fetch retries in milliseconds.", DefaultValue = 100)]

    public int FileFetchRetryTimeout { get; set; }

    [DemandsInitialization("A | separated list of DICOM group,element pairs  to NOT anonymize e.g. 0008,0020|0008,0030")]
    public string DicomTagsToKeep { get; set; }

    private IPutDicomFilesInExtractionDirectories _putter;

    private int _anonymisedImagesCount = 0;
    readonly Stopwatch _sw = new();

    private int _errors = 0;


    // private variables set up with Initialize
    private int _projectNumber;
    private IMappingRepository _uidSubstitutionLookup;
    private DirectoryInfo _destinationDirectory;
    private DicomTag[] _deleteTags;
    private List<string> _tagsToKeep;

    private bool initialized;

    public DataTable ProcessPipelineData(DataTable toProcess, IDataLoadEventListener listener, GracefulCancellationToken cancellationToken)
    {
        //Things we ignore, Lookups, SupportingSql etc
        if (_extractCommand == null)
        {
            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "Ignoring non dataset command "));
            return toProcess;
        }

        if (IgnoreDataset(toProcess, listener))
        {
            return toProcess;
        }

        _putter ??= (IPutDicomFilesInExtractionDirectories)ObjectConstructor.Construct(PutterType);

        if (!initialized)
        {
            Initialize(
                _extractCommand.Configuration.Project.ProjectNumber.Value,
                new DirectoryInfo(Path.Combine(_extractCommand.GetExtractionDirectory().FullName, "Images")));
        }

        if (MetadataOnly)
        {
            var matching = GetMetadataOnlyColumnsToProcess(toProcess);

            if (!matching.Any())
            {
                // this should have already returned above via IgnoreDataset, bad times if you end up here.
                return toProcess;
            }

            var dictionary = matching.ToDictionary(k => k,
                c => UIDMapping.SupportedTags.First(k => k.Key.DictionaryEntry.Keyword.Equals(c.ColumnName)));

            var releaseIdentifierColumn = GetReleaseIdentifierColumn().GetRuntimeName();

            foreach (DataRow row in toProcess.Rows)
            {
                SubstituteMetadataOnly(row, dictionary, toProcess.Columns[releaseIdentifierColumn]);
            }
            return toProcess;
        }

        using var pool = new ZipPool();

        var releaseColumn = GetReleaseIdentifierColumn();

        _sw.Start();

        var fileRows = new Dictionary<string, DataRow>();
        var releaseIDs = new Dictionary<string, string>();
        var dicomFiles = new List<(string, string)>();
        foreach (DataRow processRow in toProcess.Rows)
        {
            var file = (string)processRow[RelativeArchiveColumnName];
            fileRows.Add(file, processRow);
            dicomFiles.Add((file, file));
            releaseIDs.Add(file, processRow[releaseColumn.GetRuntimeName()].ToString());
        }
        foreach (var dicomFile in new AmbiguousFilePath(ArchiveRootIfAny, dicomFiles).GetDataset(FileFetchRetryLimit, FileFetchRetryTimeout, listener))
        {
            if (_errors > 0 && _errors > ErrorThreshold)
                throw new Exception($"Number of errors reported ({_errors}) reached the threshold ({ErrorThreshold})");
            cancellationToken.ThrowIfAbortRequested();
            ProcessFile(dicomFile.Item2, listener, releaseIDs[dicomFile.Item1], _putter, fileRows[dicomFile.Item1]);
        }

        _sw.Stop();

        return toProcess;
    }

    private IColumn GetReleaseIdentifierColumn()
    {
        return _extractCommand.QueryBuilder.SelectColumns.Select(c => c.IColumn).Single(c => c.IsExtractionIdentifier);
    }

    private static DataColumn[] GetMetadataOnlyColumnsToProcess(DataTable toProcess)
    {
        return toProcess.Columns.Cast<DataColumn>().Where(
                c => UIDMapping.SupportedTags.Any(k => k.Key.DictionaryEntry.Keyword.Equals(c.ColumnName)))
            .ToArray();
    }

    private bool IgnoreDataset(DataTable toProcess, IDataLoadEventListener listener)
    {
        if (MetadataOnly)
        {
            if (GetMetadataOnlyColumnsToProcess(toProcess).Length == 0)
            {
                listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "Ignoring non imaging dataset, it had no UID columns"));
                return true;
            }

            // metadata only and some legit columns yay
            return false;
        }

        //if it isn't a dicom dataset don't process it
        if (!toProcess.Columns.Contains(RelativeArchiveColumnName))
        {
            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning,
                $"Dataset {_extractCommand.DatasetBundle.DataSet} did not contain field '{RelativeArchiveColumnName}' so we will not attempt to extract images"));
            return true;
        }

        return false;
    }

    private void SubstituteMetadataOnly(DataRow row, Dictionary<DataColumn, KeyValuePair<DicomTag, UIDType>> dictionary, DataColumn releaseIdentifierCol)
    {
        string studyUid = null;
        string seriesUid = null;
        string sopUid = null;

        //rewrite the UIDs
        foreach (var kvp in dictionary)
        {
            // no UID substitution server so no UID subs
            if (_uidSubstitutionLookup == null)
                throw new Exception($"{nameof(MetadataOnly)} is on but there is no UID lookup server configured");

            var value = row[kvp.Key].ToString();

            //if it has a value for this UID
            if (value == null) continue;

            row[kvp.Key] = _uidSubstitutionLookup.GetOrAllocateMapping(value, _projectNumber, kvp.Value.Value);

            if (kvp.Value.Key == DicomTag.StudyInstanceUID)
                studyUid = row[kvp.Key].ToString();

            if (kvp.Value.Key == DicomTag.SeriesInstanceUID)
                seriesUid = row[kvp.Key].ToString();

            if (kvp.Value.Key == DicomTag.SOPInstanceUID)
                sopUid = row[kvp.Key].ToString();
        }

        var releaseIdentifier = row[releaseIdentifierCol].ToString();

        // if we have RelativeArchiveUri then we had better make sure that matches too
        if (row.Table.Columns.Contains(RelativeArchiveColumnName))
        {
            var outPath = _putter.PredictOutputPath(_destinationDirectory, releaseIdentifier, studyUid, seriesUid, sopUid);

            // if we are able to calculate the 'would be' output path from the metadata alone
            if (!string.IsNullOrWhiteSpace(outPath))
            {
                // then update the row
                row[RelativeArchiveColumnName] = outPath;
            }
        }
    }


    /// <summary>
    /// Setup class ready to start anonymising.  Pass in
    /// </summary>
    /// <param name="projectNumber"></param>
    /// <param name="destinationDirectory">Destination directory to pass to <see cref="IPutDicomFilesInExtractionDirectories"/>
    /// instances later on or null your putter does not require it</destinationDirectory>
    /// <param name="uidSubstitutionLookup">Custom IMappingRepository or null to use <see cref="UIDMappingServer"/></param>
    public void Initialize(int projectNumber, DirectoryInfo destinationDirectory, IMappingRepository uidSubstitutionLookup = null)
    {
        _projectNumber = projectNumber;

        _uidSubstitutionLookup = uidSubstitutionLookup ?? (UIDMappingServer == null ? null : new MappingRepository(UIDMappingServer));
        _destinationDirectory = destinationDirectory;

        _deleteTags = GetDeleteTags().ToArray();
        if (DicomTagsToKeep is not null)
        {
            _tagsToKeep = DicomTagsToKeep.Split('|').ToList();
        }

        initialized = true;
    }

    /// <summary>
    /// Anonymises a dicom file at <paramref name="path"/> (which may be in a zip file)
    /// </summary>
    /// <param name="path">Location of the zip file</param>
    /// <param name="listener">Where to report errors/progress to</param>
    /// <param name="releaseColumnValue">The substitution to enter in for PatientID</param>
    /// <param name="putter">Determines where the anonymous image is written to</param>
    /// <param name="rowIfAny">If a <see cref="DataTable"/> is kicking around, pass the row and it's UID fields will be updated.  Otherwise pass null.</param>
    public void ProcessFile(DicomFile dicomFile, IDataLoadEventListener listener, string releaseColumnValue,
        IPutDicomFilesInExtractionDirectories putter,
        DataRow rowIfAny)
    {
        DicomDataset ds;

        try
        {
            // do not anonymise SRs if this flag is set
            var skipAnon = SkipAnonymisationOnStructuredReports && dicomFile.Dataset.GetSingleValue<string>(DicomTag.Modality) == "SR";

            // See: ftp://medical.nema.org/medical/dicom/2011/11_15pu.pdf
            var flags = skipAnon ?
                //don't anonymise
                SecurityProfileOptions.RetainSafePrivate |
                SecurityProfileOptions.RetainDeviceIdent |
                SecurityProfileOptions.RetainInstitutionIdent |
                SecurityProfileOptions.RetainUIDs |
                SecurityProfileOptions.RetainLongFullDates |
                SecurityProfileOptions.RetainPatientChars :
                // do anonymise
                SecurityProfileOptions.BasicProfile |
                SecurityProfileOptions.CleanStructdCont |
                SecurityProfileOptions.CleanDesc |
                SecurityProfileOptions.RetainUIDs;

            if (RetainDates && !skipAnon)
                flags |= SecurityProfileOptions.RetainLongFullDates;
            var defaultProfile = DefaultProfile;

            var profile = SecurityProfile.LoadProfile(new StringReader(defaultProfile), flags);


            // I know we said skip anonymisation but still remove this stuff cmon
            if (skipAnon)
                RemovePatientNameEtc(profile);

            if (_tagsToKeep is not null && _tagsToKeep.Any())
            {
                foreach (var tag in _tagsToKeep)
                {
                    var k = profile.Keys.Where(k => k.ToString() == tag).FirstOrDefault();
                    if (k is not null)
                    {
                        profile.Remove(k);
                        profile.Add(new Regex(tag), SecurityProfileActions.K);
                    }

                }
            }

            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, profile.ToString()));
            var anonymiser = new DicomAnonymizer(profile);


            ds = anonymiser.Anonymize(dicomFile.Dataset);
        }
        catch (Exception e)
        {
            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, $"Failed to anonymize image", e));
            _errors++;
            return;
        }

        //now we want to explicitly use our own release Id regardless of what FoDicom said
        ds.AddOrUpdate(DicomTag.PatientID, releaseColumnValue);

        //rewrite the UIDs
        foreach (var (key, uidType) in UIDMapping.SupportedTags)
        {
            if (!ds.Contains(key))
                continue;

            // no UID substitution server so no UID subs
            if (_uidSubstitutionLookup == null)
                continue;

            var value = ds.GetValue<string>(key, 0);

            //if it has a value for this UID
            if (value == null) continue;
            var releaseValue = _uidSubstitutionLookup.GetOrAllocateMapping(value, _projectNumber, uidType);

            //change value in dataset
            ds.AddOrUpdate(key, releaseValue);


            //and change value in DataTable
            if (rowIfAny != null && rowIfAny.Table.Columns.Contains(key.DictionaryEntry.Keyword))
                rowIfAny[key.DictionaryEntry.Keyword] = releaseValue;
        }

        foreach (var tag in _deleteTags)
        {
            if (ds.Contains(tag))
            {
                ds.Remove(tag);
            }
        }

        var newPath = putter.WriteOutDataset(_destinationDirectory, releaseColumnValue, ds);

        if (rowIfAny != null)
            rowIfAny[RelativeArchiveColumnName] = newPath;

        _anonymisedImagesCount++;

        listener.OnProgress(this, new ProgressEventArgs("Writing ANO images", new ProgressMeasurement(_anonymisedImagesCount, ProgressType.Records), _sw.Elapsed));
    }

    private static readonly Regex patientLevelRegex = PatientLevelRegex();
    private static void RemovePatientNameEtc(SecurityProfile profile)
    {
        // we still want to remove PatientName, PatientAddress etc see these:
        // https://dicom.nema.org/medical/dicom/2015c/output/chtml/part03/sect_C.2.3.html
        profile.Add(patientLevelRegex, SecurityProfileActions.Z);
    }

    private IEnumerable<DicomTag> GetDeleteTags()
    {
        List<DicomTag> toReturn = new();
        var alsoDelete = DeleteTags?.Split(",", StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

        foreach (var s in alsoDelete)
        {
            try
            {
                toReturn.Add(DicomDictionary.Default[s]);
            }
            catch (Exception)
            {
                throw new Exception($"Could not find a tag called '{s}' when resolving {nameof(DeleteTags)} property.  All names must exactly match DicomTags");
            }
        }

        return toReturn;
    }

    public void Dispose(IDataLoadEventListener listener, Exception pipelineFailureExceptionIfAny)
    {

    }

    public void Abort(IDataLoadEventListener listener)
    {

    }

    public void PreInitialize(IExtractCommand value, IDataLoadEventListener listener)
    {
        _extractCommand = value as IExtractDatasetCommand;
    }

    private static readonly object CreateServersOneAtATime = new();

    public void Check(ICheckNotifier notifier)
    {
        try
        {
            GetDeleteTags();
        }
        catch (Exception ex)
        {
            notifier.OnCheckPerformed(new CheckEventArgs($"Error processing {nameof(DeleteTags)}", CheckResult.Fail, ex));
        }


        lock (CreateServersOneAtATime)
        {
            if (UIDMappingServer == null)
            {
                throw new Exception($"{nameof(UIDMappingServer)} not set, set it existing UID mapping server or to an empty database to create a new one");
            }

            var patcher = new SMIDatabasePatcher();

            if (UIDMappingServer.WasCreatedBy(patcher)) return;
            if (!string.IsNullOrWhiteSpace(UIDMappingServer.CreatedByAssembly))
            {
                notifier.OnCheckPerformed(new CheckEventArgs(
                    $"{nameof(UIDMappingServer)} '{UIDMappingServer}' was created by '{UIDMappingServer.CreatedByAssembly}' not a UID patcher.  Try creating a new server reference to a blank database",
                    CheckResult.Fail));
                return;
            }

            var create = notifier.OnCheckPerformed(new CheckEventArgs(
                $"{nameof(UIDMappingServer)} is not set up yet", CheckResult.Warning, null,
                "Attempt to create UID mapping schema"));

            if (!create) return;
            var db = UIDMappingServer.Discover(Core.ReusableLibraryCode.DataAccess.DataAccessContext.DataExport);

            if (!db.Exists())
            {
                notifier.OnCheckPerformed(new CheckEventArgs($"About to create {db}", CheckResult.Success));
                db.Create();
            }

            notifier.OnCheckPerformed(new CheckEventArgs($"Creating UID Mapping schema in {db}",
                CheckResult.Success));

            var scripter = new MasterDatabaseScriptExecutor(db);
            scripter.CreateAndPatchDatabase(patcher, new AcceptAllCheckNotifier());

            UIDMappingServer.CreatedByAssembly = patcher.Name;
            UIDMappingServer.SaveToDatabase();
        }
    }

    [GeneratedRegex("0010,.*", RegexOptions.CultureInvariant)]
    private static partial Regex PatientLevelRegex();


    private const string DefaultProfile = "\r\n                50[0-9A-F]{2},[0-9A-F]{4};X;;;;;;;;;;C\r\n                60[0-9A-F]{2},4000;X;;;;;;;;;;C\r\n                60[0-9A-F]{2},3000;X;;;;;;;;;;C                \r\n\t\t\t\t0008,0050;Z;;;;;;;;;;\r\n\t\t\t\t0018,4000;X;;;;;;;;C;;\r\n\t\t\t\t0040,0555;X/Z;;;;;;;;;C;\r\n\t\t\t\t0008,0022;X/Z;;;;;;K;C;;;\r\n\t\t\t\t0008,002A;X/Z/D;;;;;;K;C;;;\r\n\t\t\t\t0018,1400;X/D;;;;;;;;C;;\r\n\t\t\t\t0018,11BB;D;;;;;;;;C;;\r\n\t\t\t\t0018,9424;X;;;;;;;;C;;\r\n\t\t\t\t0008,0032;X/Z;;;;;;K;C;;;\r\n\t\t\t\t0008,0017;U;;K;;;;;;;;\r\n\t\t\t\t0040,4035;X;;;;;;;;;;\r\n\t\t\t\t0010,21B0;X;;;;;;;;C;;\r\n\t\t\t\t0040,A353;X;;;;;;;;;;\r\n\t\t\t\t0038,0010;X;;;;;;;;;;\r\n\t\t\t\t0038,0020;X;;;;;;K;C;;;\r\n\t\t\t\t0008,1084;X;;;;;;;;C;;\r\n\t\t\t\t0008,1080;X;;;;;;;;C;;\r\n\t\t\t\t0038,0021;X;;;;;;K;C;;;\r\n\t\t\t\t0000,1000;X;;K;;;;;;;;\r\n\t\t\t\t0010,2110;X;;;;;C;;;C;;\r\n\t\t\t\t006A,0006;X;;;;;;;;C;;\r\n\t\t\t\t006A,0005;D;;;;;;;;C;;\r\n\t\t\t\t006A,0003;D;;K;;;;;;;;\r\n\t\t\t\t0044,0004;X;;;;;;K;C;;;\r\n\t\t\t\t4000,0010;X;;;;;;;;;;\r\n\t\t\t\t0044,0104;D;;;;;;K;C;;;\r\n\t\t\t\t0044,0105;X;;;;;;K;C;;;\r\n\t\t\t\t0400,0562;D;;;;;;K;C;;;\r\n\t\t\t\t0040,A078;X;;;;;;;;;;\r\n\t\t\t\t2200,0005;X/Z;;;;;;;;;;\r\n\t\t\t\t300A,00C3;X;;;;;;;;C;;\r\n\t\t\t\t300C,0127;D;;;K;;;K;C;;;\r\n\t\t\t\t300A,00DD;X;;;;;;;;C;;\r\n\t\t\t\t0010,1081;X;;;;;;;;;;\r\n\t\t\t\t0014,407E;X;;;K;;;K;C;;;\r\n\t\t\t\t0018,1203;Z;;;K;;;K;C;;;\r\n\t\t\t\t0014,407C;X;;;K;;;K;C;;;\r\n\t\t\t\t0016,004D;X;;;;;;;;;;\r\n\t\t\t\t0018,1007;X;;;K;;;;;;;\r\n\t\t\t\t0400,0115;D;;;;;;;;;;\r\n\t\t\t\t0400,0310;X;;;;;;K;C;;;\r\n\t\t\t\t0012,0060;Z;;;;K;;;;;;\r\n\t\t\t\t0012,0082;X;;;;;;;;;;\r\n\t\t\t\t0012,0081;D;;;;K;;;;;;\r\n\t\t\t\t0012,0020;D;;;;;;;;;;\r\n\t\t\t\t0012,0021;Z;;;;;;;;;;\r\n\t\t\t\t0012,0072;X;;;;;;;;C;;\r\n\t\t\t\t0012,0071;X;;;;;;;;;;\r\n\t\t\t\t0012,0030;Z;;;;K;;;;;;\r\n\t\t\t\t0012,0031;Z;;;;K;;;;;;\r\n\t\t\t\t0012,0010;D;;;;;;;;;;\r\n\t\t\t\t0012,0040;D;;;;;;;;;;\r\n\t\t\t\t0012,0042;D;;;;;;;;;;\r\n\t\t\t\t0012,0051;X;;;;;;;;C;;\r\n\t\t\t\t0012,0050;Z;;;;;;;;;;\r\n\t\t\t\t0040,0310;X;;;;;;;;C;;\r\n\t\t\t\t0040,0280;X;;;;;;;;C;;\r\n\t\t\t\t300A,02EB;X;;;;;;;;C;;\r\n\t\t\t\t0020,9161;U;;K;;;;;;;;\r\n\t\t\t\t3010,000F;Z;;;;;;;;C;;\r\n\t\t\t\t3010,0017;Z;;;;;;;;C;;\r\n\t\t\t\t3010,0006;U;;K;;;;;;;;\r\n\t\t\t\t0040,3001;X;;;;;;;;;;\r\n\t\t\t\t3010,0013;U;;K;;;;;;;;\r\n\t\t\t\t0008,009C;Z;;;;;;;;;;\r\n\t\t\t\t0008,009D;X;;;;;;;;;;\r\n\t\t\t\t0050,001B;X;;;;;;;;;;\r\n\t\t\t\t0040,051A;X;;;;;;;;C;;\r\n\t\t\t\t0040,0512;D;;;;;;;;;;\r\n\t\t\t\t0070,0086;X;;;;;;;;;;\r\n\t\t\t\t0070,0084;Z/D;;;;;;;;;;\r\n\t\t\t\t0008,0023;Z/D;;;;;;K;C;;;\r\n\t\t\t\t0040,A730;D;;;;;;;;;C;\r\n\t\t\t\t0008,0033;Z/D;;;;;;K;C;;;\r\n\t\t\t\t0008,0107;D;;;;;;K;C;;;\r\n\t\t\t\t0008,0106;D;;;;;;K;C;;;\r\n\t\t\t\t0018,0010;Z/D;;;;;;;;C;;\r\n\t\t\t\t0018,1042;X;;;;;;K;C;;;\r\n\t\t\t\t0018,1043;X;;;;;;K;C;;;\r\n\t\t\t\t0018,A002;X;;;;;;K;C;;;\r\n\t\t\t\t0018,A003;X;;;;;;;;C;;\r\n\t\t\t\t0010,2150;X;;;;;;;;;;\r\n\t\t\t\t2100,0040;X;;;;;;K;C;;;\r\n\t\t\t\t2100,0050;X;;;;;;K;C;;;\r\n\t\t\t\t0040,A307;X;;;;;;;;;;\r\n\t\t\t\t0038,0300;X;;;;;;;;;;\r\n\t\t\t\t0008,0025;X;;;;;;K;C;;;\r\n\t\t\t\t0008,0035;X;;;;;;K;C;;;\r\n\t\t\t\t0040,A07C;X;;;;;;;;;;\r\n\t\t\t\tFFFC,FFFC;X;;;;;;;;;;\r\n\t\t\t\t0040,A121;D;;;;;;K;C;;;\r\n\t\t\t\t0040,A110;X;;;;;;K;C;;;\r\n\t\t\t\t0018,1205;X;;;K;;;K;C;;;\r\n\t\t\t\t0018,1200;X;;;K;;;K;C;;;\r\n\t\t\t\t0018,700C;X/D;;;K;;;K;C;;;\r\n\t\t\t\t0018,1204;X;;;K;;;K;C;;;\r\n\t\t\t\t0018,1012;X;;;;;;K;C;;;\r\n\t\t\t\t0040,A120;D;;;;;;K;C;;;\r\n\t\t\t\t0018,1202;X;;;K;;;K;C;;;\r\n\t\t\t\t0018,9701;D;;;;;;K;C;;;\r\n\t\t\t\t0018,937F;X;;;;;;;;C;;\r\n\t\t\t\t0008,2111;X;;;;;;;;C;;\r\n\t\t\t\t2100,0140;D;;;C;;;;;;;\r\n\t\t\t\t0018,700A;X/D;;;K;;;;;;;\r\n\t\t\t\t3010,001B;Z;;;;;;;;;;\r\n\t\t\t\t0050,0020;X;;;K;;;;;;;\r\n\t\t\t\t3010,002D;D;;;K;;;;;;;\r\n\t\t\t\t0018,1000;X/Z/D;;;K;;;;;;;\r\n\t\t\t\t0016,004B;X;;;;;;;;C;;\r\n\t\t\t\t0018,1002;U;;K;K;;;;;;;\r\n\t\t\t\t0400,0105;D;;;;;;K;C;;;\r\n\t\t\t\tFFFA,FFFA;X;;;;;;;;;;\r\n\t\t\t\t0400,0100;U;;;;;;;;;;\r\n\t\t\t\t0020,9164;U;;K;;;;;;;;\r\n\t\t\t\t0038,0030;X;;;;;;K;C;;;\r\n\t\t\t\t0038,0040;X;;;;;;;;C;;\r\n\t\t\t\t0038,0032;X;;;;;;K;C;;;\r\n\t\t\t\t300A,079A;X;;;;;;;;C;;\r\n\t\t\t\t4008,011A;X;;;;;;;;;;\r\n\t\t\t\t4008,0119;X;;;;;;;;;;\r\n\t\t\t\t300A,0016;X;;;;;;;;C;;\r\n\t\t\t\t300A,0013;U;;K;;;;;;;;\r\n\t\t\t\t3010,006E;U;;K;;;;;;;;\r\n\t\t\t\t0068,6226;D;;;;;;K;C;;;\r\n\t\t\t\t0042,0011;D;;;;;;;;;;\r\n\t\t\t\t0018,9517;X/D;;;;;;K;C;;;\r\n\t\t\t\t3010,0037;X;;;;;;;;C;;\r\n\t\t\t\t3010,0035;D;;;;;;;;C;;\r\n\t\t\t\t3010,0038;D;;;;;;;;C;;\r\n\t\t\t\t3010,0036;X;;;;;;;;C;;\r\n\t\t\t\t300A,0676;X;;;;;;;;C;;\r\n\t\t\t\t0012,0087;X;;;;;;K;C;;;\r\n\t\t\t\t0012,0086;X;;;;;;K;C;;;\r\n\t\t\t\t0010,2160;X;;;;;K;;;;;\r\n\t\t\t\t0010,2161;X;;;;;K;;;;;\r\n\t\t\t\t0018,9804;D;;;;;;K;C;;;\r\n\t\t\t\t0040,4011;X;;;;;;K;C;;;\r\n\t\t\t\t0008,0058;U;;K;;;;;;;;\r\n\t\t\t\t0070,031A;U;;K;;;;;;;;\r\n\t\t\t\t0040,2017;Z;;;;;;;;;;\r\n\t\t\t\t003A,032B;X;;;;;;;;C;;\r\n\t\t\t\t0040,A023;X;;;;;;K;C;;;\r\n\t\t\t\t0040,A024;X;;;;;;K;C;;;\r\n\t\t\t\t3008,0054;X/D;;;;;;K;C;;;\r\n\t\t\t\t300A,0196;X;;;;;;;;C;;\r\n\t\t\t\t0034,0002;D;;;;;;;;;;\r\n\t\t\t\t0034,0001;D;;;;;;;;;;\r\n\t\t\t\t3010,007F;Z;;;;;;;;C;;\r\n\t\t\t\t300A,0072;X;;;;;;;;C;;\r\n\t\t\t\t0018,9074;D;;;;;;K;C;;;\r\n\t\t\t\t0020,9158;X;;;;;;;;C;;\r\n\t\t\t\t0020,0052;U;;K;;;;;;;;\r\n\t\t\t\t0034,0007;D;;;;;;K;C;;;\r\n\t\t\t\t0018,9151;D;;;;;;K;C;;;\r\n\t\t\t\t0018,9623;D;;;;;;K;C;;;\r\n\t\t\t\t0018,1008;X;;;K;;;;;;;\r\n\t\t\t\t0018,1005;X;;;K;;;;;;;\r\n\t\t\t\t0016,0076;X;;;;;;;;;;\r\n\t\t\t\t0016,0075;X;;;;;;;;;;\r\n\t\t\t\t0016,008C;X;;;;;;;;;;\r\n\t\t\t\t0016,008D;X;;;;;;K;C;;;\r\n\t\t\t\t0016,0088;X;;;;;;;;;;\r\n\t\t\t\t0016,0087;X;;;;;;;;;;\r\n\t\t\t\t0016,008A;X;;;;;;;;;;\r\n\t\t\t\t0016,0089;X;;;;;;;;;;\r\n\t\t\t\t0016,0084;X;;;;;;;;;;\r\n\t\t\t\t0016,0083;X;;;;;;;;;;\r\n\t\t\t\t0016,0086;X;;;;;;;;;;\r\n\t\t\t\t0016,0085;X;;;;;;;;;;\r\n\t\t\t\t0016,008E;X;;;;;;;;;;\r\n\t\t\t\t0016,007B;X;;;;;;;;;;\r\n\t\t\t\t0016,0081;X;;;;;;;;;;\r\n\t\t\t\t0016,0080;X;;;;;;;;;;\r\n\t\t\t\t0016,0072;X;;;;;;;;;;\r\n\t\t\t\t0016,0071;X;;;;;;;;;;\r\n\t\t\t\t0016,0074;X;;;;;;;;;;\r\n\t\t\t\t0016,0073;X;;;;;;;;;;\r\n\t\t\t\t0016,0082;X;;;;;;;;;;\r\n\t\t\t\t0016,007A;X;;;;;;;;;;\r\n\t\t\t\t0016,008B;X;;;;;;;;;;\r\n\t\t\t\t0016,0078;X;;;;;;;;;;\r\n\t\t\t\t0016,007D;X;;;;;;;;;;\r\n\t\t\t\t0016,007C;X;;;;;;;;;;\r\n\t\t\t\t0016,0079;X;;;;;;;;;;\r\n\t\t\t\t0016,0077;X;;;;;;;;;;\r\n\t\t\t\t0016,007F;X;;;;;;;;;;\r\n\t\t\t\t0016,007E;X;;;;;;;;;;\r\n\t\t\t\t0016,0070;X;;;;;;;;;;\r\n\t\t\t\t0070,0001;D;;;;;;;;;;C\r\n\t\t\t\t0072,000A;D;;;;;;K;C;;;\r\n\t\t\t\t0040,E004;X;;;;;;K;C;;;\r\n\t\t\t\t0040,4037;X;;;;;;;;;;\r\n\t\t\t\t0040,4036;X;;;;;;;;;;\r\n\t\t\t\t0088,0200;X;;;;;;;;;;\r\n\t\t\t\t0008,4000;X;;;;;;;;C;;\r\n\t\t\t\t0020,4000;X;;;;;;;;C;;\r\n\t\t\t\t0028,4000;X;;;;;;;;;;\r\n\t\t\t\t0040,2400;X;;;;;;;;C;;\r\n\t\t\t\t003A,0314;D;;;;;;K;C;;;\r\n\t\t\t\t4008,0300;X;;;;;;;;C;;\r\n\t\t\t\t0068,6270;D;;;;;;K;C;;;\r\n\t\t\t\t0008,0015;X;;;;;;K;C;;;\r\n\t\t\t\t0008,0012;X/D;;;;;;K;C;;;\r\n\t\t\t\t0008,0013;X/Z/D;;;;;;K;C;;;\r\n\t\t\t\t0008,0014;U;;K;;;;;;;;\r\n\t\t\t\t0400,0600;X;;;;;;;;;;\r\n\t\t\t\t0008,0081;X;;;;K;;;;;;\r\n\t\t\t\t0008,1040;X;;;;K;;;;;;\r\n\t\t\t\t0008,1041;X;;;;K;;;;;;\r\n\t\t\t\t0008,0082;X/Z/D;;;;K;;;;;;\r\n\t\t\t\t0008,0080;X/Z/D;;;;K;;;;;;\r\n\t\t\t\t0018,9919;Z/D;;;;;;K;C;;;\r\n\t\t\t\t0010,1050;X;;;;;;;;;;\r\n\t\t\t\t3010,0085;X;;;;;;K;C;;;\r\n\t\t\t\t3010,004D;X/D;;;;;;K;C;;;\r\n\t\t\t\t3010,004C;X/D;;;;;;K;C;;;\r\n\t\t\t\t0040,1011;X;;;;;;;;;;\r\n\t\t\t\t300A,0741;D;;;;;;K;C;;;\r\n\t\t\t\t300A,0742;D;;;;;;;;C;;\r\n\t\t\t\t300A,0783;D;;;;;;;;C;;\r\n\t\t\t\t4008,0112;X;;;;;;K;C;;;\r\n\t\t\t\t4008,0113;X;;;;;;K;C;;;\r\n\t\t\t\t4008,0111;X;;;;;;;;;;\r\n\t\t\t\t4008,010C;X;;;;;;;;;;\r\n\t\t\t\t4008,0115;X;;;;;;;;C;;\r\n\t\t\t\t4008,0200;X;;;;;;;;;;\r\n\t\t\t\t4008,0202;X;;;;;;;;;;\r\n\t\t\t\t4008,0100;X;;;;;;K;C;;;\r\n\t\t\t\t4008,0101;X;;;;;;K;C;;;\r\n\t\t\t\t4008,0102;X;;;;;;;;;;\r\n\t\t\t\t4008,010B;X;;;;;;;;C;;\r\n\t\t\t\t4008,010A;X;;;;;;;;;;\r\n\t\t\t\t4008,0108;X;;;;;;K;C;;;\r\n\t\t\t\t4008,0109;X;;;;;;K;C;;;\r\n\t\t\t\t0018,0035;X;;;;;;K;C;;;\r\n\t\t\t\t0018,0027;X;;;;;;K;C;;;\r\n\t\t\t\t0008,3010;U;;K;;;;;;;;\r\n\t\t\t\t0040,2004;X;;;;;;K;C;;;\r\n\t\t\t\t0038,0011;X;;;;;;;;;;\r\n\t\t\t\t0038,0014;X;;;;;;;;;;\r\n\t\t\t\t0012,0022;X;;;;;;;;;;\r\n\t\t\t\t0012,0073;X;;;;;;;;;;\r\n\t\t\t\t0012,0032;X;;;;;;;;;;\r\n\t\t\t\t0012,0041;X;;;;;;;;;;\r\n\t\t\t\t0012,0043;X;;;;;;;;;;\r\n\t\t\t\t0012,0055;X;;;;;;;;;;\r\n\t\t\t\t0010,0021;X;;;;;;;;;;\r\n\t\t\t\t0038,0061;X;;;;;;;;;;\r\n\t\t\t\t0038,0064;X;;;;;;;;;;\r\n\t\t\t\t0040,0513;Z;;;;;;;;;;\r\n\t\t\t\t0040,0562;Z;;;;;;;;;;\r\n\t\t\t\t0040,2005;X;;;;;;K;C;;;\r\n\t\t\t\t2200,0002;X/Z;;;;;;;;C;;\r\n\t\t\t\t0028,1214;U;;K;;;;;;;;\r\n\t\t\t\t0010,21D0;X;;;;;;K;C;;;\r\n\t\t\t\t0016,004F;X;;;K;;;;;;;\r\n\t\t\t\t0016,0050;X;;;K;;;;;;;\r\n\t\t\t\t0016,0051;X;;;K;;;;;;;\r\n\t\t\t\t0016,004E;X;;;K;;;;;;;\r\n\t\t\t\t0050,0021;X;;;;;;;;C;;\r\n\t\t\t\t0400,0404;X;;;;;;;;;;\r\n\t\t\t\t0016,002B;X;;;;;;;;C;;\r\n\t\t\t\t0018,100B;U;;K;K;;;;;;;\r\n\t\t\t\t3010,0043;Z;;;K;;;;;;;\r\n\t\t\t\t0002,0003;U;;K;;;;;;;;\r\n\t\t\t\t0010,2000;X;;;;;;;;C;;\r\n\t\t\t\t0010,1090;X;;;;;;;;;;\r\n\t\t\t\t0010,1080;X;;;;;;;;;;\r\n\t\t\t\t0400,0550;X;;;;;;;;;;\r\n\t\t\t\t0020,3403;X;;;;;;K;C;;;\r\n\t\t\t\t0020,3406;X;;;;;;;;;;\r\n\t\t\t\t0020,3405;X;;;;;;K;C;;;\r\n\t\t\t\t0020,3401;X;;;K;;;;;;;\r\n\t\t\t\t0400,0563;D;;;K;;;;;;;\r\n\t\t\t\t3008,0056;X/D;;;;;;K;C;;;\r\n\t\t\t\t0018,937B;X;;;;;;;;C;;\r\n\t\t\t\t003A,0310;U;;K;;;;;;;;\r\n\t\t\t\t0008,1060;X;;;;;;;;;;\r\n\t\t\t\t0040,1010;X;;;;;;;;;;\r\n\t\t\t\t0008,1000;X;;;C;;;;;;;\r\n\t\t\t\t0400,0552;X;;;;;;;;;;\r\n\t\t\t\t0400,0551;X;;;;;;;;;;\r\n\t\t\t\t0040,A192;X;;;;;;K;C;;;\r\n\t\t\t\t0040,A032;X/D;;;;;;K;C;;;\r\n\t\t\t\t0040,A033;X;;;;;;K;C;;;\r\n\t\t\t\t0040,A402;U;;K;;;;;;;;\r\n\t\t\t\t0040,A193;X;;;;;;K;C;;;\r\n\t\t\t\t0040,A171;U;;K;;;;;;;;\r\n\t\t\t\t0010,2180;X;;;;;;;;C;;\r\n\t\t\t\t0008,1072;X/D;;;;;;;;;;\r\n\t\t\t\t0008,1070;X/Z/D;;;;;;;;;;\r\n\t\t\t\t0040,2010;X;;;;;;;;;;\r\n\t\t\t\t0040,2011;X;;;;;;;;;;\r\n\t\t\t\t0040,2008;X;;;;;;;;;;\r\n\t\t\t\t0040,2009;X;;;;;;;;;;\r\n\t\t\t\t0400,0561;X;;;;;;;;;;\r\n\t\t\t\t2100,0070;X;;;C;;;;;;;\r\n\t\t\t\t0012,0023;X;;;;;;;;;;\r\n\t\t\t\t0010,1000;X;;;;;;;;;;\r\n\t\t\t\t0010,1002;X;;;;;;;;;;\r\n\t\t\t\t0010,1001;X;;;;;;;;;;\r\n\t\t\t\t0008,0024;X;;;;;;K;C;;;\r\n\t\t\t\t0008,0034;X;;;;;;K;C;;;\r\n\t\t\t\t300A,0760;D;;;;;;K;C;;;\r\n\t\t\t\t0028,1199;U;;K;;;;;;;;\r\n\t\t\t\t0040,A07A;X;;;;;;;;;;\r\n\t\t\t\t0040,A082;Z;;;;;;K;C;;;\r\n\t\t\t\t0010,1040;X;;;;;;;;;;\r\n\t\t\t\t0010,1010;X;;;;;K;;;;;\r\n\t\t\t\t0010,0030;Z;;;;;;;;;;\r\n\t\t\t\t0010,1005;X;;;;;;;;;;\r\n\t\t\t\t0010,0032;X;;;;;;;;;;\r\n\t\t\t\t0038,0400;X;;;;;;;;;;\r\n\t\t\t\t0010,0050;X;;;;;;;;;;\r\n\t\t\t\t0010,1060;X;;;;;;;;;;\r\n\t\t\t\t0010,0010;Z;;;;;;;;;;\r\n\t\t\t\t0010,0101;X;;;;;;;;;;\r\n\t\t\t\t0010,0102;X;;;;;;;;;;\r\n\t\t\t\t0010,21F0;X;;;;;;;;;;\r\n\t\t\t\t0010,0040;Z;;;;;K;;;;;\r\n\t\t\t\t0010,2203;X/Z;;;;;K;;;;;\r\n\t\t\t\t0010,1020;X;;;;;K;;;;;\r\n\t\t\t\t0010,2155;X;;;;;;;;;;\r\n\t\t\t\t0010,2154;X;;;;;;;;;;\r\n\t\t\t\t0010,1030;X;;;;;K;;;;;\r\n\t\t\t\t0010,4000;X;;;;;;;;C;;\r\n\t\t\t\t0010,0020;Z/D;;;;;;;;;;\r\n\t\t\t\t300A,0794;X;;;;;;;;C;;\r\n\t\t\t\t300A,0650;U;;K;;;;;;;;\r\n\t\t\t\t0038,0500;X;;;;;C;;;C;;\r\n\t\t\t\t0040,1004;X;;;;;;;;;;\r\n\t\t\t\t300A,0792;X;;;;;;;;C;;\r\n\t\t\t\t300A,078E;X;;;;;;;;C;;\r\n\t\t\t\t0040,0243;X;;;;;;;;;;\r\n\t\t\t\t0040,0254;X;;;;;;;;C;;\r\n\t\t\t\t0040,0250;X;;;;;;K;C;;;\r\n\t\t\t\t0040,4051;X;;;;;;K;C;;;\r\n\t\t\t\t0040,0251;X;;;;;;K;C;;;\r\n\t\t\t\t0040,0253;X;;;;;;;;;;\r\n\t\t\t\t0040,0244;X;;;;;;K;C;;;\r\n\t\t\t\t0040,4050;X;;;;;;K;C;;;\r\n\t\t\t\t0040,0245;X;;;;;;K;C;;;\r\n\t\t\t\t0040,0241;X;;;C;;;;;;;\r\n\t\t\t\t0040,4030;X;;;K;;;;;;;\r\n\t\t\t\t0040,0242;X;;;K;;;;;;;\r\n\t\t\t\t0040,4028;X;;;K;;;;;;;\r\n\t\t\t\t0008,1050;X;;;;;;;;;;\r\n\t\t\t\t0008,1052;X;;;;;;;;;;\r\n\t\t\t\t0040,1102;X;;;;;;;;;;\r\n\t\t\t\t0040,1104;X;;;;;;;;;;\r\n\t\t\t\t0040,1103;X;;;;;;;;;;\r\n\t\t\t\t0040,1101;D;;;;;;;;;;\r\n\t\t\t\t0040,A123;D;;;;;;;;;;\r\n\t\t\t\t0008,1048;X;;;;;;;;;;\r\n\t\t\t\t0008,1049;X;;;;;;;;;;\r\n\t\t\t\t0008,1062;X;;;;;;;;;;\r\n\t\t\t\t4008,0114;X;;;;;;;;;;\r\n\t\t\t\t0040,2016;Z;;;;;;;;;;\r\n\t\t\t\t0018,1004;X;;;K;;;;;;;\r\n\t\t\t\t3002,0123;X;;;;;;;;C;;\r\n\t\t\t\t3002,0121;X;;;;;;;;C;;\r\n\t\t\t\t0010,21C0;X;;;;;K;;;;;\r\n\t\t\t\t0040,0012;X;;;;;C;;;;;\r\n\t\t\t\t300A,000E;X;;;;;;;;C;;\r\n\t\t\t\t3010,007B;Z;;;;;;;;C;;\r\n\t\t\t\t3010,0081;Z;;;;;;;;C;;\r\n\t\t\t\t0070,0082;X;;;;;;K;C;;;\r\n\t\t\t\t0070,0083;X;;;;;;K;C;;;\r\n\t\t\t\t0070,1101;U;;K;;;;;;;;\r\n\t\t\t\t0070,1102;U;;K;;;;;;;;\r\n\t\t\t\t3010,0061;X;;;;;;;;C;;\r\n\t\t\t\t0040,4052;X;;;;;;K;C;;;\r\n\t\t\t\t0044,000B;X;;;;;;K;C;;;\r\n\t\t\t\t0018,1030;X/D;;;;;;;;C;;\r\n\t\t\t\t0008,1088;X;;;;;;;;C;;\r\n\t\t\t\t0020,0027;X;;;;;;;;C;;\r\n\t\t\t\t0008,0019;U;;K;;;;;;;;\r\n\t\t\t\t300A,0619;D;;;;;;;;C;;\r\n\t\t\t\t300A,0623;D;;;;;;;;C;;\r\n\t\t\t\t300A,067D;Z;;;;;;;;C;;\r\n\t\t\t\t300A,067C;D;;;;;;;;C;;\r\n\t\t\t\t0018,1078;X;;;;;;K;C;;;\r\n\t\t\t\t0018,1072;X;;;;;;K;C;;;\r\n\t\t\t\t0018,1079;X;;;;;;K;C;;;\r\n\t\t\t\t0018,1073;X;;;;;;K;C;;;\r\n\t\t\t\t300C,0113;X;;;;;;;;C;;\r\n\t\t\t\t0040,100A;X;;;;;;;;C;;\r\n\t\t\t\t0032,1030;X;;;;;;;;C;;\r\n\t\t\t\t3010,005C;Z;;;;;;;;C;;\r\n\t\t\t\t0400,0565;D;;;;;;;;C;;\r\n\t\t\t\t0040,2001;X;;;;;;;;C;;\r\n\t\t\t\t0040,1002;X;;;;;;;;C;;\r\n\t\t\t\t0032,1066;X;;;;;;;;C;;\r\n\t\t\t\t0032,1067;X;;;;;;;;C;;\r\n\t\t\t\t0074,1234;X;;;C;;;;;;;\r\n\t\t\t\t300A,073A;D;;;;;;K;C;;;\r\n\t\t\t\t3010,000B;U;;K;;;;;;;;\r\n\t\t\t\t0040,A13A;D;;;;;;K;C;;;\r\n\t\t\t\t0400,0402;X;;;;;;;;;;\r\n\t\t\t\t300A,0083;U;;K;;;;;;;;\r\n\t\t\t\t3010,006F;U;;K;;;;;;;;\r\n\t\t\t\t3010,0031;U;;K;;;;;;;;\r\n\t\t\t\t3006,0024;U;;K;;;;;;;;\r\n\t\t\t\t0040,4023;U;;K;;;;;;;;\r\n\t\t\t\t0008,1140;X/Z/U*;;K;;;;;;;;\r\n\t\t\t\t0040,A172;U;;K;;;;;;;;\r\n\t\t\t\t0038,0004;X;;;;;;;;;;\r\n\t\t\t\t0010,1100;X;;;;;;;;;;\r\n\t\t\t\t0008,1120;X;;K;;;;;;;;\r\n\t\t\t\t0008,1111;X/Z/D;;K;;;;;;;;\r\n\t\t\t\t0400,0403;X;;;;;;;;;;\r\n\t\t\t\t0008,1155;U;;K;;;;;;;;\r\n\t\t\t\t0004,1511;U;;K;;;;;;;;\r\n\t\t\t\t0008,1110;X/Z;;K;;;;;;;;\r\n\t\t\t\t300A,0785;U;;K;;;;;;;;\r\n\t\t\t\t0008,0092;X;;;;;;;;;;\r\n\t\t\t\t0008,0090;Z;;;;;;;;;;\r\n\t\t\t\t0008,0094;X;;;;;;;;;;\r\n\t\t\t\t0008,0096;X;;;;;;;;;;\r\n\t\t\t\t0010,2152;X;;;;;;;;;;\r\n\t\t\t\t3006,00C2;U;;K;;;;;;;;\r\n\t\t\t\t0040,0275;X;;;;;;;;C;;\r\n\t\t\t\t0032,1070;X;;;;;;;;C;;\r\n\t\t\t\t0040,1400;X;;;;;;;;C;;\r\n\t\t\t\t0032,1060;X/Z;;;;;;;;C;;\r\n\t\t\t\t0040,1001;X;;;;;;;;;;\r\n\t\t\t\t0040,1005;X;;;;;;;;;;\r\n\t\t\t\t0018,9937;X;;;;;;;;C;;\r\n\t\t\t\t0000,1001;U;;K;;;;;;;;\r\n\t\t\t\t0074,1236;X;;;C;;;;;;;\r\n\t\t\t\t0032,1032;X;;;;;;;;;;\r\n\t\t\t\t0032,1033;X;;;;;;;;;;\r\n\t\t\t\t0018,9185;X;;;;;;;;C;;\r\n\t\t\t\t0010,2299;X;;;;;;;;;;\r\n\t\t\t\t0010,2297;X;;;;;;;;;;\r\n\t\t\t\t4008,4000;X;;;;;;;;C;;\r\n\t\t\t\t4008,0118;X;;;;;;;;;;\r\n\t\t\t\t4008,0040;X;;;;;;;;;;\r\n\t\t\t\t4008,0042;X;;;;;;;;;;\r\n\t\t\t\t0008,0054;X;;;C;;;;;;;\r\n\t\t\t\t300E,0004;Z;;;;;;K;C;;;\r\n\t\t\t\t300E,0008;X/Z;;;;;;;;;;\r\n\t\t\t\t300E,0005;Z;;;;;;K;C;;;\r\n\t\t\t\t3006,004D;X;;;;;;;;;;\r\n\t\t\t\t3006,002D;X;;;;;;K;C;;;\r\n\t\t\t\t3006,0028;X;;;;;;;;C;;\r\n\t\t\t\t3006,0038;X;;;;;;;;C;;\r\n\t\t\t\t3006,00A6;Z;;;;;;;;;;\r\n\t\t\t\t3006,004E;X;;;;;;;;;;\r\n\t\t\t\t3006,0026;Z;;;;;;;;C;;\r\n\t\t\t\t3006,002E;X;;;;;;K;C;;;\r\n\t\t\t\t3006,0088;X;;;;;;;;C;;\r\n\t\t\t\t3006,0085;X;;;;;;;;C;;\r\n\t\t\t\t300A,0615;Z;;;;;;;;;;\r\n\t\t\t\t300A,0611;Z;;;;;;;;;;\r\n\t\t\t\t3010,005A;Z;;;;;;;;C;;\r\n\t\t\t\t300A,0006;X/D;;;;;;K;C;;;\r\n\t\t\t\t300A,0004;X;;;;;;;;C;;\r\n\t\t\t\t300A,0002;D;;;;;;;;C;;\r\n\t\t\t\t300A,0003;X;;;;;;;;C;;\r\n\t\t\t\t300A,0007;X/D;;;;;;K;C;;;\r\n\t\t\t\t3010,0054;D;;;;;;;;C;;\r\n\t\t\t\t300A,062A;D;;;;;;;;C;;\r\n\t\t\t\t3010,0056;X/D;;;;;;;;C;;\r\n\t\t\t\t3010,003B;U;;K;;;;;;;;\r\n\t\t\t\t3008,0162;D;;;;;;K;C;;;\r\n\t\t\t\t3008,0164;D;;;;;;K;C;;;\r\n\t\t\t\t3008,0166;D;;;;;;K;C;;;\r\n\t\t\t\t3008,0168;D;;;;;;K;C;;;\r\n\t\t\t\t0038,001A;X;;;;;;K;C;;;\r\n\t\t\t\t0038,001B;X;;;;;;K;C;;;\r\n\t\t\t\t0038,001C;X;;;;;;K;C;;;\r\n\t\t\t\t0038,001D;X;;;;;;K;C;;;\r\n\t\t\t\t0040,4034;X;;;;;;;;;;\r\n\t\t\t\t0038,001E;X;;;;;;;;;;\r\n\t\t\t\t0040,0006;X;;;;;;;;;;\r\n\t\t\t\t0040,000B;X;;;;;;;;;;\r\n\t\t\t\t0040,0007;X;;;;;;;;C;;\r\n\t\t\t\t0040,0004;X;;;;;;K;C;;;\r\n\t\t\t\t0040,0005;X;;;;;;K;C;;;\r\n\t\t\t\t0040,4008;X;;;;;;K;C;;;\r\n\t\t\t\t0040,0009;X;;;;;;;;;;\r\n\t\t\t\t0040,0011;X;;;K;;;;;;;\r\n\t\t\t\t0040,4010;X;;;;;;K;C;;;\r\n\t\t\t\t0040,0002;X;;;;;;K;C;;;\r\n\t\t\t\t0040,4005;X;;;;;;K;C;;;\r\n\t\t\t\t0040,0003;X;;;;;;K;C;;;\r\n\t\t\t\t0040,0001;X;;;C;;;;;;;\r\n\t\t\t\t0040,4027;X;;;K;;;;;;;\r\n\t\t\t\t0040,0010;X;;;K;;;;;;;\r\n\t\t\t\t0040,4025;X;;;K;;;;;;;\r\n\t\t\t\t0032,1020;X;;;K;;;;;;;\r\n\t\t\t\t0032,1021;X;;;C;;;;;;;\r\n\t\t\t\t0032,1000;X;;;;;;K;C;;;\r\n\t\t\t\t0032,1001;X;;;;;;K;C;;;\r\n\t\t\t\t0032,1010;X;;;;;;K;C;;;\r\n\t\t\t\t0032,1011;X;;;;;;K;C;;;\r\n\t\t\t\t0072,005E;D;;;C;;;;;;;\r\n\t\t\t\t0072,005F;D;;;;;K;;;;;\r\n\t\t\t\t0072,0061;D;;;;;;K;C;;;\r\n\t\t\t\t0072,0063;D;;;;;;K;C;;;\r\n\t\t\t\t0072,0066;D;;;;;;;;C;;\r\n\t\t\t\t0072,0068;D;;;;;;;;C;;\r\n\t\t\t\t0072,0065;D;;;;;;;;;;\r\n\t\t\t\t0072,006A;D;;;;;;;;;;\r\n\t\t\t\t0072,006C;D;;;;;;;;C;;\r\n\t\t\t\t0072,006E;D;;;;;;;;C;;\r\n\t\t\t\t0072,006B;D;;;;;;K;C;;;\r\n\t\t\t\t0072,006D;D;;;;;;;;;;\r\n\t\t\t\t0072,0071;D;;;;;;;;;;\r\n\t\t\t\t0072,0070;D;;;;;;;;C;;\r\n\t\t\t\t0008,0021;X/D;;;;;;K;C;;;\r\n\t\t\t\t0008,103E;X;;;;;;;;C;;\r\n\t\t\t\t0020,000E;U;;K;;;;;;;;\r\n\t\t\t\t0008,0031;X/D;;;;;;K;C;;;\r\n\t\t\t\t0038,0062;X;;;;;;;;C;;\r\n\t\t\t\t0038,0060;X;;;;;;;;;;\r\n\t\t\t\t300A,01B2;X;;;;;;;;C;;\r\n\t\t\t\t300A,01A6;X;;;;;;;;C;;\r\n\t\t\t\t0040,06FA;X;;;;;;;;;;\r\n\t\t\t\t0010,21A0;X;;;;;K;;;;;\r\n\t\t\t\t0100,0420;X;;;;;;K;C;;;\r\n\t\t\t\t0008,0018;U;;K;;;;;;;;\r\n\t\t\t\t3010,0015;U;;K;;;;;;;;\r\n\t\t\t\t0018,936A;D;;;;;;K;C;;;\r\n\t\t\t\t0064,0003;U;;K;;;;;;;;\r\n\t\t\t\t0034,0005;D;;;;;;;;;;\r\n\t\t\t\t0008,2112;X/Z/U*;;K;;;;;;;;\r\n\t\t\t\t300A,0216;X;;;K;;;;;;;\r\n\t\t\t\t0400,0564;Z;;;;K;;;;;;\r\n\t\t\t\t3008,0105;X/Z;;;K;;;;;;;\r\n\t\t\t\t0018,9369;D;;;;;;K;C;;;\r\n\t\t\t\t300A,022C;D;;;;;;K;C;;;\r\n\t\t\t\t300A,022E;D;;;;;;K;C;;;\r\n\t\t\t\t0038,0050;X;;;;;C;;;;;\r\n\t\t\t\t0040,050A;X;;;;;;;;;;\r\n\t\t\t\t0040,0602;X;;;;;;;;C;;\r\n\t\t\t\t0040,0551;D;;;;;;;;;;\r\n\t\t\t\t0040,0610;Z;;;;;;;;;C;\r\n\t\t\t\t0040,0600;X;;;;;;;;C;;\r\n\t\t\t\t0040,0554;U;;K;;;;;;;;\r\n\t\t\t\t0018,9516;X/D;;;;;;K;C;;;\r\n\t\t\t\t0008,0055;X;;;C;;;;;;;\r\n\t\t\t\t0008,1010;X/Z/D;;;K;;;;;;;\r\n\t\t\t\t0088,0140;U;;K;;;;;;;;\r\n\t\t\t\t3006,0008;Z;;;;;;K;C;;;\r\n\t\t\t\t3006,0006;X;;;;;;;;C;;\r\n\t\t\t\t3006,0002;D;;;;;;;;C;;\r\n\t\t\t\t3006,0004;X;;;;;;;;C;;\r\n\t\t\t\t3006,0009;Z;;;;;;K;C;;;\r\n\t\t\t\t0032,1040;X;;;;;;K;C;;;\r\n\t\t\t\t0032,1041;X;;;;;;K;C;;;\r\n\t\t\t\t0032,4000;X;;;;;;;;C;;\r\n\t\t\t\t0032,1050;X;;;;;;K;C;;;\r\n\t\t\t\t0032,1051;X;;;;;;K;C;;;\r\n\t\t\t\t0008,0020;Z;;;;;;K;C;;;\r\n\t\t\t\t0008,1030;X;;;;;;;;C;;\r\n\t\t\t\t0020,0010;Z;;;;;;;;;;\r\n\t\t\t\t0032,0012;X;;;;;;;;;;\r\n\t\t\t\t0020,000D;U;;K;;;;;;;;\r\n\t\t\t\t0032,0034;X;;;;;;K;C;;;\r\n\t\t\t\t0032,0035;X;;;;;;K;C;;;\r\n\t\t\t\t0008,0030;Z;;;;;;K;C;;;\r\n\t\t\t\t0032,0032;X;;;;;;K;C;;;\r\n\t\t\t\t0032,0033;X;;;;;;K;C;;;\r\n\t\t\t\t0044,0010;X;;;;;;K;C;;;\r\n\t\t\t\t0020,0200;U;;K;;;;;;;;\r\n\t\t\t\t300A,0054;U;;K;;;;;;;;\r\n\t\t\t\t0018,2042;U;;K;;;;;;;;\r\n\t\t\t\t0040,A354;X;;;;;;;;;;\r\n\t\t\t\t0040,DB0D;U;;K;;;;;;;;\r\n\t\t\t\t0040,DB0C;U;;K;;;;;;;;\r\n\t\t\t\t0040,DB07;X;;;;;;K;C;;;\r\n\t\t\t\t0040,DB06;X;;;;;;K;C;;;\r\n\t\t\t\t4000,4000;X;;;;;;;;;;\r\n\t\t\t\t2030,0020;X;;;;;;;;;;\r\n\t\t\t\t0040,A122;D;;;;;;K;C;;;\r\n\t\t\t\t0040,A112;X;;;;;;K;C;;;\r\n\t\t\t\t0018,1201;X;;;K;;;K;C;;;\r\n\t\t\t\t0018,700E;X/D;;;K;;;K;C;;;\r\n\t\t\t\t0018,1014;X;;;;;;K;C;;;\r\n\t\t\t\t0008,0201;X;;;;;;K;C;;;\r\n\t\t\t\t0088,0910;X;;;;;;;;;;\r\n\t\t\t\t0088,0912;X;;;;;;;;;;\r\n\t\t\t\t0088,0906;X;;;;;;;;;;\r\n\t\t\t\t0088,0904;X;;;;;;;;;;\r\n\t\t\t\t0062,0021;U;;K;;;;;;;;\r\n\t\t\t\t0008,1195;U;;K;;;;;;;;\r\n\t\t\t\t0018,5011;X;;;K;;;;;;;\r\n\t\t\t\t3008,0024;D;;;;;;K;C;;;\r\n\t\t\t\t3008,0025;D;;;;;;K;C;;;\r\n\t\t\t\t3008,0250;X/D;;;;;;K;C;;;\r\n\t\t\t\t300A,00B2;X/Z;;;K;;;;;;;\r\n\t\t\t\t300A,0608;D;;;;;;;;C;;\r\n\t\t\t\t300A,0609;U;;K;;;;;;;;\r\n\t\t\t\t300A,0700;U;;K;;;;;;;;\r\n\t\t\t\t3010,0077;X/D;;;;;;;;C;;\r\n\t\t\t\t300A,000B;X;;;;;;;;C;;\r\n\t\t\t\t3010,007A;Z;;;;;;;;C;;\r\n\t\t\t\t3008,0251;X/D;;;;;;K;C;;;\r\n\t\t\t\t300A,0736;D;;;;;;K;C;;;\r\n\t\t\t\t300A,0734;D;;;;;;;;C;;\r\n\t\t\t\t0018,100A;X;;;K;;;;;;;\r\n\t\t\t\t0040,A124;U;;;;;;;;;;\r\n\t\t\t\t0018,1009;X;;;K;;;;;;;\r\n\t\t\t\t3010,0033;D;;;;;;;;C;;\r\n\t\t\t\t3010,0034;D;;;;;;;;C;;\r\n\t\t\t\t0040,A352;X;;;;;;;;;;\r\n\t\t\t\t0040,A358;X;;;;;;;;;;\r\n\t\t\t\t0040,A030;D;;;;;;K;C;;;\r\n\t\t\t\t0040,A088;Z;;;;;;;;;;\r\n\t\t\t\t0040,A075;D;;;;;;;;;;\r\n\t\t\t\t0040,A073;D;;;;;;;;;;\r\n\t\t\t\t0040,A027;D;;;;;;;;;;\r\n\t\t\t\t0038,4000;X;;;;;;;;C;;\r\n\t\t\t\t003A,0329;X;;;;;;;;C;;\r\n\t\t\t\t0018,9371;D;;;K;;;;;;;\r\n\t\t\t\t0018,9373;X;;;K;;;;;;;\r\n\t\t\t\t0018,9367;D;;;K;;;;;;;\r\n\t\t\t";
}