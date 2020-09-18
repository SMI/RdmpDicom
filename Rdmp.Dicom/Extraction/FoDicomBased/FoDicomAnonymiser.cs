using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Dicom;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.Progress;
using Rdmp.Dicom.Extraction.FoDicomBased.DirectoryDecisions;
using Rdmp.Core.DataExport.DataExtraction.Commands;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.DataFlowPipeline.Requirements;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.Repositories.Construction;

namespace Rdmp.Dicom.Extraction.FoDicomBased
{
    /// <summary>
    /// Goes directly to the referenced file locations (which cannot be in zip files) and runs DicomAnonymizer on the files that are referenced
    /// in  <see cref="RelativeArchiveColumnName"/>.
    /// </summary>
    public class FoDicomAnonymiser: IPluginDataFlowComponent<DataTable>,IPipelineRequirement<IExtractCommand>
    {
        private IExtractDatasetCommand _extractCommand;

        [DemandsInitialization("If the path filename contains relative file uris to images then this is the root directory")]
        public string ArchiveRootIfAny { get; set; }

        [DemandsInitialization("The column name in the extracted dataset which contains the location of the dicom files",Mandatory = true)]
        public string RelativeArchiveColumnName { get; set; }

        [DemandsInitialization("The mapping database for UID fields", Mandatory=true)]
        public ExternalDatabaseServer UIDMappingServer { get; set; }

        [DemandsInitialization("Determines how dicom files are written to the project ouput directory",TypeOf = typeof(IPutDicomFilesInExtractionDirectories),Mandatory=true)]
        public Type PutterType { get; set; }

        [DemandsInitialization("Retain Full Dates in dicom tags during anonymisation")]
        public bool RetainDates { get; set; }

        [DemandsInitialization("The number of errors (e.g. failed to find/anonymise file) to allow before abandoning the extraction",DefaultValue = 100)]
        public int ErrorThreshold {get; set; }

        private IPutDicomFilesInExtractionDirectories _putter;

        private int _anonymisedImagesCount = 0;
        readonly Stopwatch _sw = new Stopwatch();

        private int _errors = 0;

        public DataTable ProcessPipelineData(DataTable toProcess, IDataLoadEventListener listener,GracefulCancellationToken cancellationToken)
        {
            //Things we ignore, Lookups, SupportingSql etc
            if (_extractCommand == null)
            {
                listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "Ignoring non dataset command "));
                return toProcess;
            }
            
            //if it isn't a dicom dataset don't process it
            if (!toProcess.Columns.Contains(RelativeArchiveColumnName))
            {
                listener.OnNotify(this,new NotifyEventArgs(ProgressEventType.Warning, "Dataset " + _extractCommand.DatasetBundle.DataSet + " did not contain field '" + RelativeArchiveColumnName + "' so we will not attempt to extract images"));
                return toProcess;
            }

            if(_putter == null)
                _putter = (IPutDicomFilesInExtractionDirectories)  new ObjectConstructor().Construct(PutterType);

            var projectNumber = _extractCommand.Configuration.Project.ProjectNumber.Value;

            var mappingServer = new MappingRepository(UIDMappingServer);
            var destinationDirectory = new DirectoryInfo(Path.Combine(_extractCommand.GetExtractionDirectory().FullName, "Images"));

            var releaseCol = _extractCommand.QueryBuilder.SelectColumns.Select(c=>c.IColumn).Single(c=>c.IsExtractionIdentifier);

            // See: ftp://medical.nema.org/medical/dicom/2011/11_15pu.pdf

            var flags = DicomAnonymizer.SecurityProfileOptions.BasicProfile |
                        DicomAnonymizer.SecurityProfileOptions.CleanStructdCont |
                        DicomAnonymizer.SecurityProfileOptions.CleanDesc |
                        DicomAnonymizer.SecurityProfileOptions.RetainUIDs;

            if (RetainDates)
              flags |= DicomAnonymizer.SecurityProfileOptions.RetainLongFullDates;

            var profile = DicomAnonymizer.SecurityProfile.LoadProfile(null,flags);
            
            var anonymiser = new DicomAnonymizer(profile);

            using (var pool = new ZipPool())
            {
                _sw.Start();
                
                foreach (DataRow row in toProcess.Rows)
                {
                    if(_errors > 0 && _errors > ErrorThreshold)
                        throw new Exception($"Number of errors reported ({_errors}) reached the threshold ({ErrorThreshold})");

                    cancellationToken.ThrowIfAbortRequested();

                    var path = new AmbiguousFilePath(ArchiveRootIfAny, (string)row[RelativeArchiveColumnName]);

                    DicomFile dicomFile;
                    
                    try
                    {
                        dicomFile = path.GetDataset(pool);
                    }
                    catch (Exception e)
                    {
                        listener.OnNotify(this,new NotifyEventArgs(ProgressEventType.Error,$"Failed to get image at path '{path.FullPath}'",e));
                        _errors++;
                        continue;
                    }

                    //get the new patient ID
                    var releaseId = row[releaseCol.GetRuntimeName()].ToString();
                    
                    DicomDataset ds;

                    try
                    {
                        ds = anonymiser.Anonymize(dicomFile.Dataset);
                    }
                    catch (Exception e)
                    {
                        listener.OnNotify(this,new NotifyEventArgs(ProgressEventType.Error,$"Failed to anonymize image at path '{path.FullPath}'",e));
                        _errors++;
                        continue;
                    }

                    //now we want to explicitly use our own release Id regardless of what FoDicom said
                    ds.AddOrUpdate(DicomTag.PatientID, releaseId);

                    //rewrite the UIDs
                    foreach (var kvp in UIDMapping.SupportedTags)
                    {
                        if(!ds.Contains(kvp.Key))
                            continue;
                        
                        var value = ds.GetValue<string>(kvp.Key, 0);

                        //if it has a value for this UID
                        if (value == null) continue;
                        var releaseValue = mappingServer.GetOrAllocateMapping(value, projectNumber, kvp.Value);

                        //change value in dataset
                        ds.AddOrUpdate(kvp.Key, releaseValue);

                        //and change value in DataTable
                        if (toProcess.Columns.Contains(kvp.Key.DictionaryEntry.Keyword))
                            row[kvp.Key.DictionaryEntry.Keyword] = releaseValue;
                    }
                    
                    var newPath = _putter.WriteOutDataset(destinationDirectory,releaseId,ds);
                    row[RelativeArchiveColumnName] = newPath;

                    _anonymisedImagesCount++;

                    listener.OnProgress(this, new ProgressEventArgs("Writing ANO images", new ProgressMeasurement(_anonymisedImagesCount, ProgressType.Records), _sw.Elapsed));
                }

                _sw.Stop();

            }
            
            return toProcess;
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

        public void Check(ICheckNotifier notifier)
        {
            
        }
    }
}
