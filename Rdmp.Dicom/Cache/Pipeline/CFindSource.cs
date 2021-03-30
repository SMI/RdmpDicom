using Dicom;
using Dicom.Network;
using ReusableLibraryCode.Progress;
using Rdmp.Dicom.Cache.Pipeline.Dicom;
using Rdmp.Core.Caching.Requests;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.Curation;
using DicomClient = Dicom.Network.Client.DicomClient;
using CsvHelper;
using System.IO;
using CsvHelper.Configuration;
using System.Globalization;
using System.Threading;
using DicomTypeTranslation;
using System;

namespace Rdmp.Dicom.Cache.Pipeline
{

    /// <summary>
    /// Queries a remote PACS and downloads metadata into CSV ready for loading to a relational database.  This component does not fetch actual images
    /// </summary>
    public class CFindSource : SMICacheSource
    {
        private DicomTag[] _tagsToWrite = new DicomTag[] {
            DicomTag.StudyInstanceUID,
            DicomTag.PatientID,
            DicomTag.StudyDate,
            DicomTag.StudyTime,
            DicomTag.StudyDescription,
            DicomTag.ModalitiesInStudy
        };

        public override SMIDataChunk DoGetChunk(ICacheFetchRequest cacheRequest, IDataLoadEventListener listener,GracefulCancellationToken cancellationToken)
        {
            listener.OnNotify(this,new NotifyEventArgs(ProgressEventType.Information,$"CFindSource version is {typeof(CFindSource).Assembly.GetName().Version}.  Assembly is {typeof(PACSSource).Assembly} " ));
            listener.OnNotify(this,new NotifyEventArgs(ProgressEventType.Information,$"Fo-Dicom version is {typeof(DicomClient).Assembly.GetName().Version}.  Assembly is {typeof(DicomClient).Assembly} " ));

            var dicomConfiguration = GetConfiguration();
            var requestSender = new DicomRequestSender(dicomConfiguration, listener);
            var dateFrom = Request.Start;
            var dateTo = Request.End;
            CachingSCP.LocalAet = LocalAETitle;
            CachingSCP.Listener = listener;

            if (PatientIdWhitelistColumnInfo != null && !IgnoreWhiteList)
                GetWhitelist(listener);

            //temp dir
            var cacheDir = new LoadDirectory(Request.CacheProgress.LoadProgress.LoadMetadata.LocationOfFlatFiles).Cache;
            var cacheLayout = new SMICacheLayout(cacheDir, new SMICachePathResolver(Modality));
            
            Chunk = new SMIDataChunk(Request)
            {
                FetchDate = dateFrom,
                Modality = Modality,
                Layout = cacheLayout
            };

            // Create filepath for the results of the C-Find
            var workingDirectory = cacheLayout.GetLoadCacheDirectory(listener);
            var filename = $"{dateFrom:yyyy-MM-dd}.csv";
            var filepath = Path.Combine(workingDirectory.FullName, filename);

            var sw = new StreamWriter(filepath);
            var writer = new CsvWriter(sw,new CsvConfiguration(CultureInfo.CurrentCulture));

            WriteHeaders(writer);
                        
            DicomClient client = new DicomClient(dicomConfiguration.RemoteAetUri.Host, dicomConfiguration.RemoteAetUri.Port, false, dicomConfiguration.LocalAetTitle, dicomConfiguration.RemoteAetTitle);
                
            try
            {
                // Find a list of studies
                #region Query

                listener.OnNotify(this,
                    new NotifyEventArgs(ProgressEventType.Information,
                        "Requesting Studies from " + dateFrom + " to " + dateTo));
                int responses = 0;

                var request = CreateStudyRequestByDateRangeForModality(dateFrom, dateTo, Modality);
                request.OnResponseReceived += (req, response) =>
                {
                    if (Filter(Whitelist, response)) {
                        Interlocked.Increment(ref responses);
                        WriteResult(writer,response);
                        }

                };
                requestSender.ThrottleRequest(request,client, cancellationToken.AbortToken);
                listener.OnNotify(this,
                    new NotifyEventArgs(ProgressEventType.Debug,
                        "Total filtered studies for " + dateFrom + " to " + dateTo +"is " + responses));
                #endregion

            }
            finally
            {
                writer.Dispose();
            }
            

            return Chunk;
        }

        private void WriteHeaders(CsvWriter writer)
        {
            foreach(var t in _tagsToWrite)
            {
                var colName = DicomTypeTranslaterReader.GetColumnNameForTag(t,false);
                writer.WriteField(colName);
            }

            writer.NextRecord();
        }

        private void WriteResult(CsvWriter writer, DicomCFindResponse response)
        {
            if (!response.HasDataset)
                return;

            foreach (var t in _tagsToWrite)
            {
                WriteValue(writer, response,t);
            }

            writer.NextRecord();
        }

        private void WriteValue(CsvWriter writer, DicomCFindResponse response, DicomTag tag)
        {
            var val = DicomTypeTranslaterReader.GetCSharpValue(response.Dataset, tag);

            if(val == null)
            {
                writer.WriteField("");
            }
            else
            {
                writer.WriteField(DicomTypeTranslater.Flatten(val));                
            }
        }
    }
}