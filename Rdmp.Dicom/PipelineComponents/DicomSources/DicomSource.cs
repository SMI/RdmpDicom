using FellowOakDicom;
using DicomTypeTranslation;
using DicomTypeTranslation.Elevation.Exceptions;
using DicomTypeTranslation.Elevation.Serialization;
using Rdmp.Core.ReusableLibraryCode.Checks;
using Rdmp.Core.ReusableLibraryCode.Progress;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.Curation.Data.DataLoad;
using Rdmp.Core.ReusableLibraryCode.Annotations;

namespace Rdmp.Dicom.PipelineComponents.DicomSources;

public abstract class DicomSource : IPluginDataFlowSource<DataTable>
{
    [DemandsInitialization("By default all encountered tags are turned into columns in the DataTable, setting this to an existing table will instead use the columns in that table")]
    public TableInfo FieldMapTableIfAny { get; set; }

    [DemandsInitialization("This overrides FieldMapTableIfAny.  By default all encountered tags are turned into columns in the DataTable, setting this will only load columns in the load's table(s) column(s) instead")]
    public LoadMetadata UseAllTableInfoInLoadAsFieldMap { get; set; }

    [DemandsInitialization("By default all encountered tags are turned into columns in the DataTable, setting this will only add tags that match the whitelist regex")]
    public Regex TagWhitelist { get; set; }

    [DemandsInitialization("By default all encountered tags are turned into columns in the DataTable, setting this will discard tags that match the blacklist regex.  This is compatible with other tag selection strategy arguments and is applied afterwards")]
    public Regex TagBlacklist { get; set; }

    [DemandsInitialization("Optional - Lets you specify deeply buried tags which should be fetched out and put into columns of the DataTable being generated")]
    public FileInfo TagElevationConfigurationFile { get; set; }

    [DemandsInitialization("Optional - Alternative to TagElevationConfigurationFile property.  Use this property to store the elevation XML directly in RDMP instead of a file on disk")]
    public TagElevationXml TagElevationConfigurationXml { get; set; }

    [DemandsInitialization("Optional - The root directory of the images you are trying to load.  If you set this then any image paths loaded from this directory will be expressed as relative subdirectories e.g. c:\\MyImages\\1\\image1.dcm could be expressed \\1\\image1.dcm")]
    public string ArchiveRoot
    {
        get => _archiveRoot;
        set => _archiveRoot = StandardisePath(value);
    }

    private static string StandardisePath(string value)
    {
        //trim leading and ending whitespace and normalize slashes
        value = value?.TrimStart().TrimEnd(' ', '\t', '\r', '\n');

        //Standardize on forward slashes (but don't try to fix \\ e.g. at the start of a UNC path
        if (!string.IsNullOrEmpty(value))
            value = value.StartsWith("\\\\", StringComparison.Ordinal)
                ? $"\\\\{value[2..].Replace('\\', '/')}"
                : value.Replace('\\', '/');

        //if it has a trailing slash (but isn't just '/') then trim the end
        if(value != null)
            return value.Length != 1 ? value.TrimEnd('\\', '/') : value;

        return null;
    }

    private TagElevationRequestCollection _elevationRequests;

    [DemandsInitialization("The field to store the physical location of the images into e.g. RelativeFileArchiveURI", DefaultValue = "RelativeFileArchiveURI", Mandatory = true)]
    public string FilenameField { get; set; }

    [DemandsInitialization(@"Determines what to do with DicomTags which cannot be deserialized into C# types e.g. 3.40282347e+038:
    MarkCorrupt - Message is added to the CorruptMessages list and Nacked/Not Loaded
    ThrowException - E.g. if a DecimalString cannot be turned into a decimal then throw an exception
    ConvertToNullAndWarn - E.g. if a DecimalString cannot be turned into a decimal store the cell value DBNull.Value instead", DefaultValue = InvalidDataHandling.ConvertToNullAndWarn)]
    public InvalidDataHandling InvalidDataHandlingStrategy { get; set; }


    [DemandsInitialization(@"Determines the behaviour of the system when strings are read from DICOM that do not conform to the maximum lengths in the database (based on VR)
    None - No checking takes place 
    TruncateAndWarn - Values are truncated to the maximum length allowed by the database
    MarkCorrupt - Message is added to the CorruptMessages list and Nacked/Not Loaded
    ConvertToNullAndWarn - Values are set to Null", DefaultValue = DataTooWideHandling.None)]
    public DataTooWideHandling DataTooLongHandlingStrategy { get; set; }

    private readonly object _oDataTableLock = new();
    private string _archiveRoot;

    /// <summary>
    /// The maximum length supported by the VR / Multiplicity of the tag (this is likely to be the size of the database columns - we would hope!)
    /// </summary>
    private readonly Dictionary<DicomTag, int> _maxTagLengths = new();

    private readonly object _oDictLock = new();


    public void Check(ICheckNotifier notifier)
    {
        if (FieldMapTableIfAny != null && TagWhitelist != null)
            notifier.OnCheckPerformed(new CheckEventArgs("Cannot specify both a FieldMapTableIfAny and a TagWhitelist", CheckResult.Fail));

        try
        {
            LoadElevationRequestsFile();
        }
        catch (Exception e)
        {
            notifier.OnCheckPerformed(new CheckEventArgs("Could not deserialize TagElevationConfigurationFile", CheckResult.Fail, e));
        }

        // Fail if a non-absolute ArchiveRoot is set:
        if (!string.IsNullOrWhiteSpace(ArchiveRoot) && !Path.IsPathFullyQualified(ArchiveRoot))
            notifier.OnCheckPerformed(new CheckEventArgs("ArchiveRoot is not rooted, it must be an absolute path e.g. c:\\temp\\MyImages\\", CheckResult.Fail));
    }

    private static IEnumerable<string> SquashTree(DicomDataset ds, DicomTag t)
    {
        if (ds.TryGetSingleValue(t, out string value)) yield return value;

        foreach (var datum in ds.OfType<DicomSequence>().SelectMany(seq => seq.SelectMany(i => SquashTree(i, t))))
            yield return datum;
    }

    /// <summary>
    /// Iterates through each DicomItem in the DicomDataset and creates a corresponding column in the DataTable (if it is a novel tag) and then populates
    /// the DataTable with the data for the image.
    /// </summary>
    /// <param name="filename"></param>
    /// <param name="ds"></param>
    /// <param name="dt"></param>
    /// <param name="listener"></param>
    /// <param name="otherValuesToStoreInRow"></param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    protected void ProcessDataset(string filename, DicomDataset ds, DataTable dt, IDataLoadEventListener listener, Dictionary<string, string> otherValuesToStoreInRow = null)
    {
        _elevationRequests ??= LoadElevationRequestsFile();

        filename = ApplyArchiveRootToMakeRelativePath(filename);

        var rowValues = new Dictionary<string, object>();

        // First collect all CodeMeaning and CodeValue values as strings:
        var meanings = string.Join('\n', SquashTree(ds, DicomTag.CodeMeaning));
        if (!string.IsNullOrWhiteSpace(meanings)) rowValues.Add("CodeMeanings", meanings);
        var values = string.Join('\n', SquashTree(ds, DicomTag.CodeValue));
        if (!string.IsNullOrWhiteSpace(values)) rowValues.Add("CodeValues", values);

        foreach (var item in ds)
        {
            // First special-case Sequences such as ICD11 diagnostic codes:
            if (item is DicomSequence seq && item.Tag == DicomTag.ConceptNameCodeSequence)
            {
                var code = seq.Items[0];
                var scheme = code.GetSingleValueOrDefault(DicomTag.CodingSchemeDesignator, "");
                if (scheme.Equals("I11", StringComparison.Ordinal) || scheme.Equals("ICD11", StringComparison.Ordinal))
                {
                    // Capture ICD11 code and meaning
                    rowValues.Add("ICD11code", code.GetSingleValueOrDefault(DicomTag.CodeValue, "missing"));
                    rowValues.Add("ICD11meaning", code.GetSingleValueOrDefault(DicomTag.CodeMeaning, "missing"));
                }

                continue;
            }

            //get the tag name (human readable)
            var entry = item.Tag.DictionaryEntry;

            var header = entry.Keyword;

            if (ShouldSkip(dt, item.Tag))
                continue;

            object value;

            switch (InvalidDataHandlingStrategy)
            {
                case InvalidDataHandling.ThrowException:

                    //enforce types and leave any Exceptions uncaught
                    value = DicomTypeTranslater.Flatten(new[] { DicomTypeTranslaterReader.GetCSharpValue(ds, item) });
                    break;
                case InvalidDataHandling.MarkCorrupt:

                    try
                    {
                        //try to enforce types
                        value = DicomTypeTranslater.Flatten(new[] { DicomTypeTranslaterReader.GetCSharpValue(ds, item) });
                    }
                    catch (Exception ex)
                    {
                        //something went wrong pulling out the value

                        //mark it as corrupt
                        MarkCorrupt(ds);

                        //but make sure to warn people listening
                        listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning,
                            $"Could not GetCSharpValue for DicomItem {item.Tag}({entry.Keyword}) for {GetProblemFileDescription(filename, otherValuesToStoreInRow)}", ex));

                        //do not add the row to the table
                        return;
                    }
                    break;
                case InvalidDataHandling.ConvertToNullAndWarn:

                    //try to enforce types
                    try
                    {
                        value = DicomTypeTranslater.Flatten(new[] { DicomTypeTranslaterReader.GetCSharpValue(ds, item) });
                    }
                    catch (Exception ex)
                    {

                        //but make sure to warn people listening
                        listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning,
                            $"Could not GetCSharpValue for DicomItem {item.Tag}({entry.Keyword}) for {GetProblemFileDescription(filename, otherValuesToStoreInRow)}", ex));

                        if (InvalidDataHandlingStrategy == InvalidDataHandling.MarkCorrupt)
                        {
                            MarkCorrupt(ds);
                            continue;
                        }

                        //It went wrong, we couldn't enforce the type so just use null instead
                        value = DBNull.Value;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        $"{nameof(InvalidDataHandlingStrategy)} had unknown value {InvalidDataHandlingStrategy}");
            }

            if (value is string s && DataTooLongHandlingStrategy != DataTooWideHandling.None)
                if (!IsValidLength(listener, item.Tag, s))
                {

                    //the string is too long!
                    switch (DataTooLongHandlingStrategy)
                    {
                        case DataTooWideHandling.TruncateAndWarn:
                            lock (_oDictLock)
                            {
                                value = s[.._maxTagLengths[item.Tag]];
                            }
                            break;
                        case DataTooWideHandling.MarkCorrupt:
                            MarkCorrupt(ds);
                            continue;
                        case DataTooWideHandling.ConvertToNullAndWarn:
                            value = DBNull.Value;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException($"{nameof(DataTooLongHandlingStrategy)} had unknown value {DataTooLongHandlingStrategy}");
                    }
                }

            try
            {
                rowValues.Add(header, value);
            }
            catch (Exception e)
            {
                listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning,
                    $"Error getting Tag '{header}' item.ValueRepresentation is ({item.ValueRepresentation}) {GetProblemFileDescription(filename, otherValuesToStoreInRow)}", e));
            }

        }

        //now run elevation requests
        if (_elevationRequests != null)
        {
            foreach (var request in _elevationRequests.Requests)
            {
                try
                {
                    object value;
                    switch (InvalidDataHandlingStrategy)
                    {
                        case InvalidDataHandling.ThrowException:
                            value = request.Elevator.GetValue(ds);
                            break;
                        case InvalidDataHandling.ConvertToNullAndWarn:
                            try
                            {
                                value = request.Elevator.GetValue(ds);
                            }
                            catch (Exception e)
                            {
                                if (e is TagNavigationException)
                                    throw;

                                listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning,
                                    $"Error getting Tag for ElevationRequest '{request.ColumnName}' for {GetProblemFileDescription(filename, otherValuesToStoreInRow)}", e));
                                value = DBNull.Value;
                            }

                            break;
                        default:
                            throw new ArgumentOutOfRangeException($"{nameof(InvalidDataHandlingStrategy)} has unknown value {InvalidDataHandlingStrategy}");
                    }

                    rowValues.Add(request.ColumnName, value);
                }
                catch (TagNavigationException e)
                {
                    listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning,
                        $"Rule for column {request.ColumnName} failed to resolve GetValue for {GetProblemFileDescription(filename, otherValuesToStoreInRow)}", e));
                }
            }
        }


        //do not add rows if there is nothing to store
        if (!rowValues.Any())
            return;

        lock (_oDataTableLock)
        {
            var row = dt.Rows.Add();
            row[FilenameField] = filename;

            if (otherValuesToStoreInRow != null)
                foreach (var (key, value) in otherValuesToStoreInRow)
                {
                    if (!dt.Columns.Contains(key))
                        dt.Columns.Add(key);
                    row[key] = value;
                }

            foreach (var (key, value) in rowValues)
                Add(dt, row, key, value);
        }
    }

    public bool ShouldSkip(DataTable dt, DicomTag tag)
    {

        //if there is a whitelist
        if (TagWhitelist != null)
            if (!TagWhitelist.IsMatch(tag.DictionaryEntry.Keyword)) //and the current header isn't matched by it
                return true;

        //if there is a blacklist
        if (TagBlacklist != null)
            if (TagBlacklist.IsMatch(tag.DictionaryEntry.Keyword)) //and the current header matches the blacklist
                return true; //skip it

        //if there is an explict mapping to follow
        if (FieldMapTableIfAny == null && UseAllTableInfoInLoadAsFieldMap == null) return false;
        //if we don't have the tag in our schema ignore it
        return !dt.Columns.Contains(tag.DictionaryEntry.Keyword);
    }

    private bool IsValidLength(IDataLoadEventListener listener, DicomTag tag, string value)
    {
        lock (_oDictLock)
        {
            if (!_maxTagLengths.TryGetValue(tag, out var maxLength))
            {
                var type = DicomTypeTranslater.GetNaturalTypeForVr(
                    tag.DictionaryEntry.ValueRepresentations, tag.DictionaryEntry.ValueMultiplicity);
                maxLength = type.Width ?? -1;
                _maxTagLengths.Add(tag, maxLength);
            }

            //we don't think it's a string or the string length is OK
            if (maxLength <= 0 || value.Length <= maxLength)
                return true;

            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning,
                $"Found value '{value}' that was too long for its VR ({tag}).  Max length was {maxLength} supplied value was length {value.Length}"));
        }

        return false;
    }

    protected virtual void MarkCorrupt(DicomDataset ds)
    {

    }

    private static string GetProblemFileDescription(string filename, Dictionary<string, string> otherValuesToStoreInRow)
    {
        var s = $"Problem File:{Environment.NewLine}{filename}";

        if (otherValuesToStoreInRow != null)
            s +=
                $"{Environment.NewLine}{string.Join(Environment.NewLine, otherValuesToStoreInRow.Select(kvp => $"{kvp.Key}:{kvp.Value}"))}";

        return s.Trim();
    }

    /// <summary>
    /// Takes the given path which may be rooted or not.  If it is rooted then the <see cref="ArchiveRoot"/> (if any)
    /// will be trimmed off the start (case insensitive).
    /// 
    /// <para>Returns the filename unaltered if it is not rooted or there is no <see cref="ArchiveRoot"/> set up</para>
    /// 
    /// <para>Also reconciles mixed backslash directions</para>
    /// </summary>
    /// <param name="filename"></param>
    /// <returns></returns>
    public string ApplyArchiveRootToMakeRelativePath(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return filename;

        //standardize directory separator character e.g. change \ to /
        filename = StandardisePath(filename);

        //if it is relative to ArchiveRoot then express only the subsection with "./" at start
        if (string.IsNullOrWhiteSpace(ArchiveRoot)) return filename;
        return filename.StartsWith(ArchiveRoot, StringComparison.CurrentCultureIgnoreCase) ? $"./{filename[ArchiveRoot.Length..].TrimStart('/')}" : filename;

        //otherwise return the original
    }

    private static void Add(DataTable dt, DataRow row, string header, object value)
    {
        //if it is a new header
        if (!dt.Columns.Contains(header))
            dt.Columns.Add(header); //add it

        row[header] = value;
    }

    public virtual TagElevationRequestCollection LoadElevationRequestsFile()
    {
        //if tag elevation is specified in raw XML
        if(TagElevationConfigurationXml != null && !string.IsNullOrWhiteSpace(TagElevationConfigurationXml.xml))
            return new TagElevationRequestCollection(TagElevationConfigurationXml.xml);

        //if tag elevation is specified in a file
        return TagElevationConfigurationFile != null ? new TagElevationRequestCollection(File.ReadAllText(TagElevationConfigurationFile.FullName)) : null;

        //there is no tag elevation
    }

    public abstract DataTable GetChunk(IDataLoadEventListener listener, GracefulCancellationToken cancellationToken);

    public virtual void Dispose(IDataLoadEventListener listener, Exception pipelineFailureExceptionIfAny)
    {

    }

    public virtual void Abort(IDataLoadEventListener listener)
    {

    }

    public abstract DataTable TryGetPreview();

    protected DataTable GetDataTable()
    {
        var dt = new DataTable();

        //The location of the image, all other fields are Tag discovered
        dt.Columns.Add(FilenameField, typeof(string));

        AddTableInfoColumnsIfFieldMapExists(dt);

        return dt;
    }

    private void AddTableInfoColumnsIfFieldMapExists(DataTable dt)
    {
        if (UseAllTableInfoInLoadAsFieldMap != null)
        {
            foreach (var rawColumn in UseAllTableInfoInLoadAsFieldMap.GetDistinctTableInfoList(false).SelectMany(t => t.GetColumnsAtStage(LoadStage.AdjustRaw)))
            {
                var cname = rawColumn.GetRuntimeName(LoadStage.AdjustRaw);

                if (!dt.Columns.Contains(cname))
                    dt.Columns.Add(cname);
            }
        }
        else
        if (FieldMapTableIfAny != null)
        {
            foreach (var columnInfo in FieldMapTableIfAny.ColumnInfos)
            {
                var cname = columnInfo.GetRuntimeName();

                if (!dt.Columns.Contains(cname))
                    dt.Columns.Add(cname);
            }
        }
    }

    public class TagElevationXml : ICustomUIDrivenClass
    {
        public string xml { get; set; }

        public void RestoreStateFrom([CanBeNull] string value)
        {
            xml = value;
        }

        public string SaveStateToString()
        {
            return xml;
        }
    }

}