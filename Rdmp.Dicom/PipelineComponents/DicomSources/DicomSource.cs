using Dicom;
using DicomTypeTranslation;
using DicomTypeTranslation.Elevation.Exceptions;
using DicomTypeTranslation.Elevation.Serialization;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.Progress;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.Curation.Data.DataLoad;
using ReusableLibraryCode.Annotations;
using TypeGuesser;

namespace Rdmp.Dicom.PipelineComponents.DicomSources
{
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

        [DemandsInitialization("Optional - Lets you specify deeply burried tags which should be fetched out and put into columns of the DataTable being generated")]
        public FileInfo TagElevationConfigurationFile { get; set; }

        [DemandsInitialization("Optional - Alternative to TagElevationConfigurationFile property.  Use this property to store the elevation XML directly in RDMP instead of a file on disk")]
        public TagElevationXml TagElevationConfigurationXml { get; set; }

        [DemandsInitialization("Optional - The root directory of the images you are trying to load.  If you set this then any image paths loaded from this directory will be expressed as relative subdirectories e.g. c:\\MyImages\\1\\image1.dcm could be expressed \\1\\image1.dcm")]
        public string ArchiveRoot
        {
            get => _archiveRoot;
            set
            {
                //trim leading and ending whitespace and normalize slashes
                value = value?.TrimStart().TrimEnd(' ', '\t', '\r', '\n').Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                //if it has a trailing slash (but isn't just '/') then trim the end
                if(value != null)
                    _archiveRoot = value.Length != 1 ? value.TrimEnd('\\', '/') : value;
                else
                    _archiveRoot = null;
            }
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

        private readonly object _oDataTableLock = new object();
        private string _archiveRoot;

        /// <summary>
        /// The maximum length supported by the VR / Multiplicity of the tag (this is likely to be the size of the database columns - we would hope!)
        /// </summary>
        private readonly Dictionary<DicomTag, int> _maxTagLengths = new Dictionary<DicomTag, int>();

        private readonly object _oDictLock = new object();


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

            if (!string.IsNullOrWhiteSpace(ArchiveRoot))
                if (!Path.IsPathRooted(ArchiveRoot))
                    notifier.OnCheckPerformed(new CheckEventArgs("ArchiveRoot is not rooted, it must be an absolute path e.g. c:\\temp\\MyImages\\", CheckResult.Fail));

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
        protected void ProcessDataset(string filename, DicomDataset ds, DataTable dt, IDataLoadEventListener listener, Dictionary<string, string> otherValuesToStoreInRow = null)
        {
            if (_elevationRequests == null)
                _elevationRequests = LoadElevationRequestsFile();

            filename = ApplyArchiveRootToMakeRelativePath(filename);

            var rowValues = new Dictionary<string, object>();

            foreach (DicomItem item in ds)
            {
                //get the tag name (human readable)
                var entry = item.Tag.DictionaryEntry;

                string header = entry.Keyword;

                if (ShouldSkip(dt, item.Tag))
                    continue;

                object value;

                switch (InvalidDataHandlingStrategy)
                {
                    case InvalidDataHandling.ThrowException:

                        //enforce types and leave any Exceptions uncaught
                        value = DicomTypeTranslater.Flatten(DicomTypeTranslaterReader.GetCSharpValue(ds, item));
                        break;
                    case InvalidDataHandling.MarkCorrupt:
                        
                        try
                        {
                            //try to enforce types
                            value = DicomTypeTranslater.Flatten(DicomTypeTranslaterReader.GetCSharpValue(ds, item));
                        }
                        catch (Exception ex)
                        {
                            //something went wrong pulling out the value
                            
                            //mark it as corrupt
                            MarkCorrupt(ds);
                            
                            //but make sure to warn people listening
                            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning, "Could not GetCSharpValue for DicomItem " + item.Tag + "(" + entry.Keyword + ") for " + GetProblemFileDescription(filename, otherValuesToStoreInRow), ex));

                            //do not add the row to the table
                            return;
                        }
                        break;
                    case InvalidDataHandling.ConvertToNullAndWarn:

                        //try to enforce types
                        try
                        {
                            value = DicomTypeTranslater.Flatten(DicomTypeTranslaterReader.GetCSharpValue(ds, item));
                        }
                        catch (Exception ex)
                        {

                            //but make sure to warn people listening
                            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning, "Could not GetCSharpValue for DicomItem " + item.Tag + "(" + entry.Keyword + ") for " + GetProblemFileDescription(filename, otherValuesToStoreInRow), ex));

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
                        throw new ArgumentOutOfRangeException();
                }

                if (value is string && DataTooLongHandlingStrategy != DataTooWideHandling.None)
                    if (!IsValidLength(listener, item.Tag, (string)value))
                    {

                        //the string is too long!
                        switch (DataTooLongHandlingStrategy)
                        {
                            case DataTooWideHandling.TruncateAndWarn:
                                lock (_oDictLock)
                                {
                                    value = ((string)value).Substring(0, _maxTagLengths[item.Tag]);
                                }
                                break;
                            case DataTooWideHandling.MarkCorrupt:
                                MarkCorrupt(ds);
                                continue;
                            case DataTooWideHandling.ConvertToNullAndWarn:
                                value = DBNull.Value;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }

                try
                {
                    rowValues.Add(header, value);
                }
                catch (Exception e)
                {
                    listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning, "Error getting Tag '" + header + "' item.ValueRepresentation is (" + item.ValueRepresentation + ") " + GetProblemFileDescription(filename, otherValuesToStoreInRow), e));
                }

            }

            //now run elevation requests
            if (_elevationRequests != null)
            {
                foreach (TagElevationRequest request in _elevationRequests.Requests)
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

                                    listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning, "Error getting Tag for ElevationRequest '" + request.ColumnName + "' for " + GetProblemFileDescription(filename, otherValuesToStoreInRow), e));
                                    value = DBNull.Value;
                                }

                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        rowValues.Add(request.ColumnName, value);
                    }
                    catch (TagNavigationException e)
                    {
                        listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning, "Rule for column " + request.ColumnName + " failed to resolve GetValue for " + GetProblemFileDescription(filename, otherValuesToStoreInRow), e));
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
                    foreach (KeyValuePair<string, string> kvp in otherValuesToStoreInRow)
                    {
                        if (!dt.Columns.Contains(kvp.Key))
                            dt.Columns.Add(kvp.Key);
                        row[kvp.Key] = kvp.Value;
                    }

                foreach (KeyValuePair<string, object> keyValuePair in rowValues)
                    Add(dt, row, keyValuePair.Key, keyValuePair.Value);
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
            if (FieldMapTableIfAny != null || UseAllTableInfoInLoadAsFieldMap != null)
            {
                //if we don't have the tag in our schema ignore it
                if (!dt.Columns.Contains(tag.DictionaryEntry.Keyword))
                    return true;
            }

            return false;
        }

        private bool IsValidLength(IDataLoadEventListener listener, DicomTag tag, string value)
        {
            lock (_oDictLock)
            {
                if (!_maxTagLengths.ContainsKey(tag))
                {
                    DatabaseTypeRequest type = DicomTypeTranslater.GetNaturalTypeForVr(
                        tag.DictionaryEntry.ValueRepresentations, tag.DictionaryEntry.ValueMultiplicity);
                    _maxTagLengths.Add(tag, type.Width ?? -1);
                }

                //we don't think it's a string or the string is a fine length
                if (_maxTagLengths[tag] <= 0 || value.Length <= _maxTagLengths[tag])
                    return true;

                listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning,
                    "Found value '" + value + "' that was too long for it's VR (" + tag + ").  Max length was " +
                        _maxTagLengths[tag] + " supplied value was length " + value.Length));
            }

            return false;
        }

        protected virtual void MarkCorrupt(DicomDataset ds)
        {

        }

        private string GetProblemFileDescription(string filename, Dictionary<string, string> otherValuesToStoreInRow)
        {
            string s = "Problem File:" + Environment.NewLine + filename;

            if (otherValuesToStoreInRow != null)
                s += Environment.NewLine + string.Join(Environment.NewLine, otherValuesToStoreInRow.Select(kvp => kvp.Key + ":" + kvp.Value));

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

            //standardise directory separator character e.g. change \ to /
            filename = filename.Replace('\\','/');

            //if it is relative to ArchiveRoot then express only the subsection with "./" at start
            if (!string.IsNullOrWhiteSpace(ArchiveRoot))
                if (filename.StartsWith(ArchiveRoot, StringComparison.CurrentCultureIgnoreCase))
                    return "./" + filename.Substring(ArchiveRoot.Length).TrimStart('/');

            //otherwise return the original
            return filename;
        }

        private void Add(DataTable dt, DataRow row, string header, object value)
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
            if (TagElevationConfigurationFile != null)
                return new TagElevationRequestCollection(File.ReadAllText(TagElevationConfigurationFile.FullName));
            
            //there is no tag elevation
            return null;
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
                foreach (IHasStageSpecificRuntimeName rawColumn in UseAllTableInfoInLoadAsFieldMap.GetDistinctTableInfoList(false).SelectMany(t => t.GetColumnsAtStage(LoadStage.AdjustRaw)))
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
}