using Rdmp.Core.DataExport.DataExtraction.Commands;
using Rdmp.Core.DataFlowPipeline.Requirements;
using Rdmp.Core.DataFlowPipeline;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rdmp.Core.ReusableLibraryCode.Progress;
using Rdmp.Core.ReusableLibraryCode.Checks;
using Rdmp.Core.Curation.Data;
using Rdmp.Dicom.Extraction.FoDicomBased.DirectoryDecisions;
using System.IO;
using Rdmp.Core.Repositories.Construction;
using FellowOakDicom;
using Rdmp.Dicom.Extraction.FoDicomBased;
using Amazon.S3.Model;

namespace Rdmp.Dicom.Extraction.PixelAnonymisation
{
    public class DicomPixelAnonymiser : IPluginDataFlowComponent<DataTable>, IPipelineRequirement<IExtractCommand>
    {



        [DemandsInitialization("If the path filename contains relative file uris to images then this is the root directory")]
        public string ArchiveRootIfAny { get; set; }

        [DemandsInitialization("The column name in the extracted dataset which contains the location of the dicom files", Mandatory = true)]
        public string RelativeArchiveColumnName { get; set; }

        [DemandsInitialization("Determines how dicom files are written to the project output directory", TypeOf = typeof(IPutDicomFilesInExtractionDirectories), Mandatory = true)]
        public Type PutterType { get; set; }

        [DemandsInitialization("Does a previous Extraction step extract the images? i.e. FODicomAnonymiser", Mandatory = false, DefaultValue = false)]
        public bool ImagesAlreadyInDestination { get; set; }

        [DemandsInitialization("How many tries to allow for fetching the file. This setting may be useful on network drives or oversubscribed resources", DefaultValue = 0)]

        public int FileFetchRetryLimit { get; set; }

        [DemandsInitialization("How long to wait between file fetch retries in milliseconds.", DefaultValue = 100)]

        public int FileFetchRetryTimeout { get; set; }

        [DemandsInitialization("Expected text language ", DefaultValue = "en")]
        public string Language { get; set; }

        [DemandsInitialization("Use GPU", DefaultValue = false)]
        public bool UseGPU { get; set; }

        [DemandsInitialization("Location of python DLL or .so")]
        public string PythonLocation { get; set; }

        private IExtractDatasetCommand _extractCommand;
        private IPutDicomFilesInExtractionDirectories _putter;
        private DirectoryInfo _destinationDirectory;

        public void Abort(IDataLoadEventListener listener)
        {
        }

        public void Check(ICheckNotifier notifier)
        {
            //check python is set up etc
            //todo
        }

        public void Dispose(IDataLoadEventListener listener, Exception pipelineFailureExceptionIfAny)
        {
        }

        public void PreInitialize(IExtractCommand value, IDataLoadEventListener listener)
        {
            _extractCommand = value as IExtractDatasetCommand;

        }

        public DataTable ProcessPipelineData(DataTable toProcess, IDataLoadEventListener listener, GracefulCancellationToken cancellationToken)
        {
            if (_extractCommand == null)
            {
                listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "Ignoring non dataset command "));
                return toProcess;
            }

            _putter ??= (IPutDicomFilesInExtractionDirectories)ObjectConstructor.Construct(PutterType);
            _destinationDirectory = new DirectoryInfo(Path.Combine(_extractCommand.GetExtractionDirectory().FullName, "Images"));
            var releaseIdentifierColumn = _extractCommand.QueryBuilder.SelectColumns.Select(c => c.IColumn).Single(c => c.IsExtractionIdentifier);
            var ocr = new DicomOCR(Language, UseGPU, PythonLocation, listener);
            var redact = new DicomRedact();
            foreach (DataRow processRow in toProcess.Rows)
            {
                var file = (string)processRow[RelativeArchiveColumnName];
                var releaseId = processRow[releaseIdentifierColumn.GetRuntimeName()].ToString();
                var dicomFile = new AmbiguousFilePath(ArchiveRootIfAny, file).GetDataset(FileFetchRetryLimit, FileFetchRetryTimeout, listener);
                DicomDataset ds = dicomFile.First().Item2.Dataset;
                string newPath;
                if (!ImagesAlreadyInDestination)
                {
                    newPath = _putter.WriteOutDataset(_destinationDirectory, releaseId, ds);
                    if (processRow != null)
                    {
                        processRow[RelativeArchiveColumnName] = newPath;
                    }
                }
                else
                {
                    newPath = processRow[RelativeArchiveColumnName].ToString();
                }
                try
                {
                    var recrangles = ocr.ProcessDicomFile(ds, file);
                    redact.Redact(ds, file, recrangles, newPath);
                }catch(Exception e)
                {
                    listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, e.Message));
                }
            }
            return toProcess;
        }
    }
}
