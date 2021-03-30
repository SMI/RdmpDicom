using System;
using Rdmp.Core.Caching.Layouts;
using Rdmp.Core.Caching.Pipeline.Destinations;
using Rdmp.Core.Caching.Requests;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.DataFlowPipeline;
using ReusableLibraryCode.Progress;

namespace Rdmp.Dicom.Cache
{
    public class SMICacheDestination : CacheFilesystemDestination
    {
        public bool DEBUG_DoNotUpdateCacheProgress;

        [DemandsInitialization("The modality of the dicom images e.g. CT, this must match the source components modality",Mandatory = true)]
        public string Modality { get; set; }

        [DemandsInitialization("The file extension to look for in fetched data", Mandatory = true, DefaultValue = "*.dcm")]
        public string Extension { get; set; } 

        public SMIDataChunk ProcessPipelineData(SMIDataChunk toProcess, IDataLoadEventListener listener, GracefulCancellationToken cancellationToken)
        {
            if(!Modality.Equals(toProcess.Modality))
                throw new Exception("Modality '" + Modality + "' of destination component did not match ICacheChunk toProcess Modality which was '" + toProcess.Modality +"'");


            // next archive the files and remove the temporary dicom files
            // todo: skip this and stream directly into an archive
            var archiveDate = toProcess.Request.Start;
            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "Creating archive: " + toProcess.Layout.GetArchiveFileInfoForDate(archiveDate, listener).FullName));
            toProcess.Layout.CreateArchive(archiveDate,listener, Extension);

            // remove all the .dcm/.csv files
            var workingDirectory = toProcess.Layout.GetLoadCacheDirectory(listener);
            foreach (var file in workingDirectory.EnumerateFiles(Extension))
                file.Delete();



            //save cache fill progress to the database
            if(!DEBUG_DoNotUpdateCacheProgress)
                toProcess.Request.SaveCacheFillProgress(toProcess.Request.End);
             

            return toProcess;
        }

        public override ICacheChunk ProcessPipelineData(ICacheChunk toProcess, IDataLoadEventListener listener,GracefulCancellationToken cancellationToken)
        {
            return ProcessPipelineData((SMIDataChunk) toProcess, listener, cancellationToken);
        }

        public override ICacheLayout CreateCacheLayout()
        {
            return new SMICacheLayout(CacheDirectory, new SMICachePathResolver(Modality));
        }

        public override void Abort(IDataLoadEventListener listener)
        {
        }

    }
}