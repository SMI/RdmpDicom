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
using System.Threading.Tasks;

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

    private IPutDicomFilesInExtractionDirectories _putter;

    private int _anonymisedImagesCount = 0;
    readonly Stopwatch _sw = new();

    private int _errors = 0;


    // private variables set up with Initialize
    private int _projectNumber;
    private IMappingRepository _uidSubstitutionLookup;
    private DirectoryInfo _destinationDirectory;
    private DicomTag[] _deleteTags;

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
        Parallel.ForEach(new AmbiguousFilePath(ArchiveRootIfAny, dicomFiles).GetDataset(), dicomFile =>
        {
            if (_errors > 0 && _errors > ErrorThreshold)
                throw new Exception($"Number of errors reported ({_errors}) reached the threshold ({ErrorThreshold})");
            cancellationToken.ThrowIfAbortRequested();
            ProcessFile(dicomFile.Item2, listener, releaseIDs[dicomFile.Item1], _putter, fileRows[dicomFile.Item1]);
        });

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

            var profile = SecurityProfile.LoadProfile(null, flags);


            // I know we said skip anonymisation but still remove this stuff cmon
            if (skipAnon)
                RemovePatientNameEtc(profile);

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

        try
        {
            listener.OnProgress(this, new ProgressEventArgs("Writing ANO images", new ProgressMeasurement(_anonymisedImagesCount, ProgressType.Records), _sw.Elapsed));
        }
        catch (Exception) { }
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
}