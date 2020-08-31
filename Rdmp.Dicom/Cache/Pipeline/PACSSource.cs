using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Dicom;
using Dicom.Network;
using Dicom.Network.Client;
using MapsDirectlyToDatabaseTable;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.DataAccess;
using ReusableLibraryCode.Progress;
using Rdmp.Dicom.Cache.Pipeline.Dicom;
using Rdmp.Dicom.PACS;
using Timer = System.Timers.Timer;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Caching.Pipeline.Sources;
using Rdmp.Core.Caching.Requests;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.Curation;
using Rdmp.Core.QueryBuilding;
using DicomClient = Dicom.Network.Client.DicomClient;
using System.Collections.Concurrent;
using System.Linq;

namespace Rdmp.Dicom.Cache.Pipeline
{
    public class PACSSource : CacheSource<SMIDataChunk>
    {
        
        [DemandsInitialization("Remote Application Entity of the PACS server",mandatory:true)]
        public string RemoteAETitle { get; set; }

        [DemandsInitialization("Where the remote PACS server is", mandatory: true)]
        public Uri RemoteAEUri { get; set; }

        [DemandsInitialization("The port to send the request to on the remote PACS server", mandatory: true)]
        public int RemoteAEPort { get; set; }
        
        [DemandsInitialization("Local Application Entity title of your computer/software", mandatory: true)]
        public string LocalAETitle { get; set; }

        [DemandsInitialization("Local AE Uri of your computer", mandatory: true)]
        public Uri LocalAEUri { get; set; }

        [DemandsInitialization("The type of imaging to be cached, using the relevant acronym from the DICOM standard. e.g. CT,MR", mandatory: true)]
        public string Modality { get; set; }

        [DemandsInitialization("The port to listen on for responses", defaultValue: 2104, mandatory: true)]
        public int LocalAEPort { get; set; }

        [DemandsInitialization("Cooldown (in seconds) after a successful request", defaultValue: 60, mandatory: true)]
        public int RequestCooldownInSeconds { get; set; }

        [DemandsInitialization("Cooldown (in seconds) after a successful transfer", defaultValue: 120, mandatory: true)]
        public int TransferCooldownInSeconds { get; set; }

        [DemandsInitialization("Polling (in seconds) for successful transfer", defaultValue: 1, mandatory: true)]
        public int TransferPollingInSeconds { get; set; }

        [DemandsInitialization("Timeout period (in seconds) for unsuccessful transfer", defaultValue: 120, mandatory: true)]
        public int TransferTimeOutInSeconds { get; set; }

        [DemandsInitialization("A column containing a whitelist of patient identifiers which are permitted to be downloaded")]
        public ColumnInfo PatientIdWhitelistColumnInfo { get; set; }

        [DemandsInitialization("Ignore whitelist of patient identifiers",defaultValue: false, mandatory: true)]
        public bool IgnoreWhiteList { get; set; }

        [DemandsInitialization("Set the DICOM priority (recommended value for NHS transfers is low)", defaultValue: DicomPriority.Low, mandatory: true)]
        public DicomPriority Priority { get; set; }

        private HashSet<string> _whitelist;

        public override SMIDataChunk DoGetChunk(ICacheFetchRequest cacheRequest, IDataLoadEventListener listener,GracefulCancellationToken cancellationToken)
        {
            listener.OnNotify(this,new NotifyEventArgs(ProgressEventType.Information,$"PACSSource version is {typeof(PACSSource).Assembly.GetName().Version}.  Assembly is {typeof(PACSSource).Assembly} " ));
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
            
            ConcurrentBag<string> studiesToOrder = new ConcurrentBag<string>();

            CachingSCP.OnEndProcessingCStoreRequest = (storeRequest, storeResponse) =>
            {                
                SaveSopInstance(storeRequest,cacheLayout,listener);
                listener.OnNotify(this,
                    new NotifyEventArgs(ProgressEventType.Debug,
                        "Stored sopInstance" + storeRequest.SOPInstanceUID.UID));
            };

            //helps with tyding up resources if we abort or through an exception and neatly avoids ->  Access to disposed closure
            using (var server = (DicomServer<CachingSCP>) DicomServer.Create<CachingSCP>(dicomConfiguration.LocalAetUri.Port))
            {
                DicomClient client = new DicomClient(dicomConfiguration.RemoteAetUri.Host, dicomConfiguration.RemoteAetUri.Port, false, dicomConfiguration.LocalAetTitle, dicomConfiguration.RemoteAetTitle);
                
                try
                {
                    // Find a list of studies
                    #region Query

                    listener.OnNotify(this,
                        new NotifyEventArgs(ProgressEventType.Information,
                            "Requesting Studies from " + dateFrom + " to " + dateTo));
                        
                    var request = CreateStudyRequestByDateRangeForModality(dateFrom, dateTo, Modality);
                    request.OnResponseReceived += (req, response) =>
                    {
                        if (Filter(_whitelist, response))
                            studiesToOrder.Add(response.Dataset.GetSingleValue<string>(DicomTag.StudyInstanceUID));

                    };
                    requestSender.ThrottleRequest(request,client, cancellationToken.AbortToken);
                    listener.OnNotify(this,
                        new NotifyEventArgs(ProgressEventType.Debug,
                            "Total filtered studies for " + dateFrom + " to " + dateTo +"is " + studiesToOrder.Count));
                    #endregion

                    //go and get them
                    #region Retrieval

                    var transferStopwatch = new Stopwatch();

                    string current;
                    int consecutiveFailures = 0;
                                            
                    //While we have things to fetch
                    while(studiesToOrder.TryTake(out current))
                    {
                        transferStopwatch.Restart();
                        //delay value in mills
                        if (dicomConfiguration.TransferCooldownInMilliseconds != 0)
                        {
                            listener.OnNotify(this,
                                new NotifyEventArgs(ProgressEventType.Information,
                                    "Transfers sleeping for " + dicomConfiguration.TransferCooldownInMilliseconds / 1000 + "seconds"));
                            Task.Delay(dicomConfiguration.TransferCooldownInMilliseconds, cancellationToken.AbortToken).Wait(cancellationToken.AbortToken);
                        }
                                                
                        bool done = false;

                        //Build fetch command that Study
                        var cMoveRequest = CreateCMoveByStudyUid(LocalAETitle,current, listener);

                        //Register callbacks
                        cMoveRequest.OnResponseReceived += (requ, response) =>
                        {
                            listener.OnNotify(this,
                                new NotifyEventArgs(ProgressEventType.Debug,
                                $"Got {response.Status.State} response for {requ}.  Items remaining {response.Remaining}"));

                            switch(response.Status.State)
                            {
                                case DicomState.Pending : 
                                case DicomState.Warning : 
                                        // ignore
                                        break;                                 
                                case DicomState.Cancel : 
                                case DicomState.Failure :
                                    consecutiveFailures++;
                                    // final state
                                    done = true;
                                    break;
                                case DicomState.Success :
                                    // final state
                                    consecutiveFailures = 0;
                                    done = true;
                                    break;
                            }
                        };
                        
                        //send the command to the server

                        //do not use requestSender.ThrottleRequest(cMoveRequest, cancellationToken);
                        //TODO is there any need to throtttle this request given its lifetime
                        requestSender.ThrottleRequest(cMoveRequest, client, cancellationToken.AbortToken);
                                                
                        
                        //enforce a minimum timeout
                        var swStudyTransfer = Stopwatch.StartNew();
                        bool hasTransferTimedOut = false;

                        do
                        {
                            Task.Delay(Math.Max(100,dicomConfiguration.TransferPollingInMilliseconds), cancellationToken.AbortToken)
                                .Wait(cancellationToken.AbortToken);
                            
                           hasTransferTimedOut = swStudyTransfer.ElapsedMilliseconds > dicomConfiguration.TransferTimeOutInMilliseconds;
                                
                        }while(!done && !hasTransferTimedOut);

                        // Study has finished being fetched (or timed out)
                        
                        if(hasTransferTimedOut)
                            listener.OnNotify(this,new NotifyEventArgs(ProgressEventType.Information,"Abandonning fetch of study " + current));
                        else
                            listener.OnNotify(this,new NotifyEventArgs(ProgressEventType.Information,CMoveRequestToString(cMoveRequest,1)));
                        
                        if(consecutiveFailures > 5)
                            throw new Exception("Too many consecutive failures, giving up");

                        // 1 failure = study not available, 2 failures = system is having a bad day?
                        if(consecutiveFailures > 1)
                        {
                            //wait 4 minutes then 6 minutes then 8 minutes, eventually server will start responding again?
                            int sleepFor = consecutiveFailures * 2 * 60_000;
                            listener.OnNotify(this,new NotifyEventArgs(ProgressEventType.Warning,$"Sleeping for {sleepFor}ms due to {consecutiveFailures} consecutive failures"));

                            Task.Delay( sleepFor, cancellationToken.AbortToken)
                                .Wait(cancellationToken.AbortToken);
                        }
                    }
                        
                    #endregion
                }
                finally
                {
                    server.Stop();
                }
            }

            return Chunk;
        }

        private DicomCMoveRequest CreateCMoveByStudyUid(string destination, string studyUid, IDataLoadEventListener listener)
        {
            var request = new DicomCMoveRequest(destination, studyUid);
            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "DicomRetriever.CreateCMoveByStudyUid created request for: " + studyUid));
            // no more dicomtags have to be set
            return request;
        }
        public override void Dispose(IDataLoadEventListener listener, Exception pipelineFailureExceptionIfAny)
        {
        }

        public override void Abort(IDataLoadEventListener listener)
        {
        }

        
        public override SMIDataChunk TryGetPreview()
        {
            return null;
        }

        #region Check
        public override void Check(ICheckNotifier notifier)
        {
            // ping configured remote PACS with a C-ECHO request
            //use a new requestSender
            var echoRequestSender = new DicomRequestSender(GetConfiguration(), new FromCheckNotifierToDataLoadEventListener(notifier));
            echoRequestSender.OnRequestException += (ex) =>
            {
                notifier.OnCheckPerformed(new CheckEventArgs("Error sending ECHO", CheckResult.Fail, ex));
            };
            echoRequestSender.OnRequestTimeout += () => 
            {
                notifier.OnCheckPerformed(new CheckEventArgs("Failed to get response from server after timeout", CheckResult.Fail));
            };
            echoRequestSender.OnRequestSucess += () =>
            {
                notifier.OnCheckPerformed(new CheckEventArgs("Successfully received C-ECHO response from remote PACS", CheckResult.Success));
            };

            try
            {
                echoRequestSender.Check();
            }
            catch (Exception e)
            {
                notifier.OnCheckPerformed(new CheckEventArgs("Error when sending C-ECHO to remote PACS",CheckResult.Fail, e));
            }
        }
        #endregion

        #region CreateStudyRequestByDateRangeForModality
        public static DicomCFindRequest CreateStudyRequestByDateRangeForModality(DateTime dateFrom, DateTime dateTo, string modality, DicomPriority priority= DicomPriority.Low)
        {

            var request = new DicomCFindRequest(DicomQueryRetrieveLevel.Study,priority);

            // always add the encoding - with agnostic encoding
            request.Dataset.AddOrUpdate(new DicomTag(0x8, 0x5), "ISO_IR 100");

            // add the dicom tags with empty values that should be included in the result of the QR Server
            request.Dataset.AddOrUpdate(DicomTag.PatientID, "");
            request.Dataset.AddOrUpdate(DicomTag.StudyInstanceUID, "");
            request.Dataset.AddOrUpdate(DicomTag.StudyDescription, "");

            string modalityToQuery = modality.Equals("all", StringComparison.OrdinalIgnoreCase) ? "" : modality;
            // add the dicom tags that contain the filter criteria
            request.Dataset.AddOrUpdate(DicomTag.ModalitiesInStudy, modalityToQuery);
            var studyDateRange = new DicomDateRange(dateFrom, dateTo);
            request.Dataset.AddOrUpdate(DicomTag.StudyDate, studyDateRange);
            request.Dataset.AddOrUpdate(DicomTag.StudyTime, studyDateRange);
            return request;
        }
        #endregion

        #region CreateSeriesRequestByStudyUID
        public static DicomCFindRequest CreateSeriesRequestByStudyUid(string studyInstanceUid, DicomPriority priority = DicomPriority.Low)
        {
            //create your own request that contains exactly those DicomTags that
            // you realy need pro process your data and not to cause unneccessary traffic and IO load:
            var request = new DicomCFindRequest(DicomQueryRetrieveLevel.Series,priority);

            request.Dataset.AddOrUpdate(new DicomTag(0x8, 0x5), "ISO_IR 100");

            // add the dicom tags with empty values that should be included in the result
            request.Dataset.AddOrUpdate(DicomTag.SeriesInstanceUID, "");
            request.Dataset.AddOrUpdate(DicomTag.SeriesDescription, "");
            request.Dataset.AddOrUpdate(DicomTag.Modality, "");
            request.Dataset.AddOrUpdate(DicomTag.NumberOfSeriesRelatedInstances, "");

            // add the dicom tags that contain the filter criteria
            request.Dataset.AddOrUpdate(DicomTag.StudyInstanceUID, studyInstanceUid);

            return request;
        }
        #endregion

        #region CreateSopRequestBySeriesUID
        public static DicomCFindRequest CreateSopRequestBySeriesUid(string seriesInstanceUid, DicomPriority priority = DicomPriority.Low)
        {
            //create your own request that contains exactly those DicomTags that
            // you realy need pro process your data and not to cause unneccessary traffic and IO load:
            var request = new DicomCFindRequest(DicomQueryRetrieveLevel.Image,priority);

            request.Dataset.AddOrUpdate(new DicomTag(0x8, 0x5), "ISO_IR 100");

            // add the dicom tags with empty values that should be included in the result
            request.Dataset.AddOrUpdate(DicomTag.PatientID, "");
            request.Dataset.AddOrUpdate(DicomTag.SOPInstanceUID, "");
            request.Dataset.AddOrUpdate(DicomTag.Modality, "");

            // add the dicom tags that contain the filter criteria
            request.Dataset.AddOrUpdate(DicomTag.SeriesInstanceUID, seriesInstanceUid);

            return request;
        }
        #endregion

        #region SaveSopInstance
        private void SaveSopInstance(DicomCStoreRequest request, SMICacheLayout cacheLayout, IDataLoadEventListener listener)
        {
            var instUid = request.SOPInstanceUID.UID;
            if (!request.HasDataset)
                return;
            // Create filepath and save
            var workingDirectory = cacheLayout.GetLoadCacheDirectory(listener);
            var filename = instUid + ".dcm";
            var filepath = Path.Combine(workingDirectory.FullName, filename);
            request.File.Save(filepath);
        }
        #endregion

        #region GetConfiguration
        private DicomConfiguration GetConfiguration()
        {
            return new DicomConfiguration
            {
                LocalAetTitle = LocalAETitle,
                LocalAetUri = DicomConfiguration.MakeUriUsePort(LocalAEUri, LocalAEPort),
                RemoteAetTitle = RemoteAETitle,
                RemoteAetUri = DicomConfiguration.MakeUriUsePort(RemoteAEUri, RemoteAEPort),
                RequestCooldownInMilliseconds = 1000 * RequestCooldownInSeconds,
                TransferCooldownInMilliseconds = 1000 * TransferCooldownInSeconds,
                TransferPollingInMilliseconds = 1000 * TransferPollingInSeconds,
                TransferTimeOutInMilliseconds = 1000 * TransferTimeOutInSeconds,
                Priority = Priority,
            };
        }
        #endregion


        #region GetWhitelist
        private void GetWhitelist(IDataLoadEventListener listener)
        {
            _whitelist = new HashSet<string>();

            var db = DataAccessPortal.GetInstance().ExpectDatabase(PatientIdWhitelistColumnInfo.TableInfo, DataAccessContext.DataLoad);
            var server = db.Server;

            var qb = new QueryBuilder("distinct", null);
            qb.AddColumn(new ColumnInfoToIColumn(new MemoryRepository(), PatientIdWhitelistColumnInfo));

            var sql = qb.SQL;

            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "Downloading Whitelist with SQL:" + sql));

            using (var con = server.GetConnection())
            {
                con.Open();
                var r = server.GetCommand(sql, con).ExecuteReader();

                while (r.Read())
                {
                    var o = r[PatientIdWhitelistColumnInfo.GetRuntimeName()];
                    if (o == null || o == DBNull.Value)
                        continue;

                    _whitelist.Add(o.ToString());
                }
            }

            listener.OnNotify(this, new NotifyEventArgs(_whitelist.Count == 0 ? ProgressEventType.Error : ProgressEventType.Information, "Whitelist contained " + _whitelist.Count + " identifiers"));
        }
        #endregion

        #region Filter
        private static bool Filter(HashSet<string> whitelistIfAny, DicomCFindResponse response)
        {
            var dataset = response.Dataset;

            //ignore responses where there are no dataset record being returned
            if (dataset == null)
                return false;

            //if there is a whitelist
            if (whitelistIfAny != null)
            {
                //get the response dataset patientId
                var patientId = dataset.GetSingleValue<string>(DicomTag.PatientID);

                //if the patientId is empty or not on our whitelist
                if (patientId == null)
                    return false;

                return whitelistIfAny.Contains(patientId.Trim());
            }
            //No WhiteList just add
            return true;
        }
        #endregion
 

        #region CMoveRequestToString
        private string CMoveRequestToString(DicomCMoveRequest cMoveRequest, int attempt)
        {
            var stub = "Retrieving " + cMoveRequest.Level.ToString() + $" (attempt {attempt}) : ";
            switch (cMoveRequest.Level)
            {
                case DicomQueryRetrieveLevel.Patient:
                    return stub + cMoveRequest.Dataset.GetSingleValue<string>(DicomTag.PatientID);
                case DicomQueryRetrieveLevel.Study:
                    return stub + cMoveRequest.Dataset.GetSingleValue<string>(DicomTag.StudyInstanceUID);
                case DicomQueryRetrieveLevel.Series:
                    return stub + cMoveRequest.Dataset.GetSingleValue<string>(DicomTag.SeriesInstanceUID);
                case DicomQueryRetrieveLevel.Image:
                    return stub + cMoveRequest.Dataset.GetSingleValue<string>(DicomTag.SOPInstanceUID);
                default:
                    return stub + DicomQueryRetrieveLevel.NotApplicable.ToString();
            }
        }

        #endregion

    }

    #region TimerExtension
    public static class TimerExtension
    {
        public static void Reset(this Timer timer)
        {
            timer.Stop();
            timer.Start();
        }
    }
    #endregion

}