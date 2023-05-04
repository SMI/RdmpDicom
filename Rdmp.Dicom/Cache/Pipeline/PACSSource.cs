using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FellowOakDicom;
using FellowOakDicom.Network;
using Rdmp.Core.ReusableLibraryCode.Progress;
using Rdmp.Dicom.Cache.Pipeline.Dicom;
using Timer = System.Timers.Timer;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Caching.Requests;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.Curation;
using System.Collections.Concurrent;
using FellowOakDicom.Log;
using FellowOakDicom.Network.Client;

namespace Rdmp.Dicom.Cache.Pipeline;

public class PACSSource : SMICacheSource
{


    /// <summary>
    /// The maximum number of tries to fetch a given Study from the PACS.  Note that retries requests might not be issued immediately after
    /// a failure.
    /// </summary>
    [DemandsInitialization("Maximum number of times to re-request a Study when a Failure is encountered", defaultValue: 3, mandatory: true)]
    public int MaxRetries { get; set; } = 3;

    [DemandsInitialization("The timeout (in ms) to wait for an association response after sending an association release request.  Defaults to 50ms if not specified")]
    public int? AssociationLingerTimeoutInMs { get; set; }

    /// 
    [DemandsInitialization("The timeout (in ms) to wait for an association response after sending an association request.  Defaults to 10000ms if not specified")]
    public int? AssociationReleaseTimeoutInMs { get; set; }

    [DemandsInitialization("The timeout (in ms) that associations need to be held open after all requests have been processed.  Defaults to 5000ms if not specified")]
    public int? AssociationRequestTimeoutInMs { get; set; }

    [DemandsInitialization("The maximum number of DICOM requests that are allowed to be sent over one single association.  When this limit is reached, the DICOM client will wait for pending requests to complete, and then open a new association to send the remaining requests, if any.  If not provided then int.MaxValue is used (i.e. keep reusing association)")]
    public int? MaximumNumberOfRequestsPerAssociation { get; set; }

    [DemandsInitialization("The maximum number of Association related events that can be permitted per minute before the system exits", DefaultValue = 30)]
    public int MaximumAllowableAssociationEventsPerMinute { get; set; } = 30;

    [DemandsInitialization("True to log individual file fetch messages for each study requested at Info level.  False to log at Trace level.", DefaultValue = true)]
    public bool Verbose { get; set; }

    public static PressureGauge gauge = new() { ThresholdBeatsPerMinute = 30 };


    public override SMIDataChunk DoGetChunk(ICacheFetchRequest cacheRequest, IDataLoadEventListener listener, GracefulCancellationToken cancellationToken)
    {
        gauge.ThresholdBeatsPerMinute = MaximumAllowableAssociationEventsPerMinute;

        listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, $"PACSSource version is {typeof(PACSSource).Assembly.GetName().Version}.  Assembly is {typeof(PACSSource).Assembly} "));
        listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, $"Fo-Dicom version is {typeof(DicomClient).Assembly.GetName().Version}.  Assembly is {typeof(DicomClient).Assembly} "));

        var dicomConfiguration = GetConfiguration();
        var requestSender = new DicomRequestSender(dicomConfiguration, listener, Verbose);
        var dateFrom = Request.Start;
        var dateTo = Request.End;
        CachingSCP.LocalAet = LocalAETitle;
        CachingSCP.Listener = listener;
        CachingSCP.Verbose = Verbose;

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

        ConcurrentBag<StudyToFetch> studiesToOrder = new();

        CachingSCP.OnEndProcessingCStoreRequest = (storeRequest, storeResponse) =>
        {
            SaveSopInstance(storeRequest, cacheLayout, listener);
            listener.OnNotify(this,
                new NotifyEventArgs(ProgressEventType.Debug,
                    $"Stored sopInstance{storeRequest.SOPInstanceUID.UID}"));
        };

        //helps with tidying up resources if we abort or through an exception and neatly avoids ->  Access to disposed closure
        using var server = new DicomServer<CachingSCP>(new DicomServerDependencies(new DesktopNetworkManager(),new ConsoleLogManager()));
        var client = DicomClientFactory.Create(dicomConfiguration.RemoteAetUri.Host,
            dicomConfiguration.RemoteAetUri.Port, false, dicomConfiguration.LocalAetTitle,
            dicomConfiguration.RemoteAetTitle);
        client.AssociationAccepted += (s, e) => {
            gauge.Tick(listener, () => Process.GetCurrentProcess().Kill());
            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Trace, "AssociationAccepted"));
        };
        client.AssociationReleased += (s, e) => {
            gauge.Tick(listener, () => Process.GetCurrentProcess().Kill());
            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Trace, "AssociationReleased"));
        };
        client.AssociationRejected += (s, e) =>
        {
            gauge.Tick(listener, () => Process.GetCurrentProcess().Kill());
            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Trace, "AssociationRejected"));
        };

        if (AssociationLingerTimeoutInMs is > 0)
            client.ClientOptions.AssociationLingerTimeoutInMs = AssociationLingerTimeoutInMs.Value;

        if (AssociationReleaseTimeoutInMs is > 0)
            client.ClientOptions.AssociationReleaseTimeoutInMs = AssociationReleaseTimeoutInMs.Value;

        if (AssociationRequestTimeoutInMs is > 0)
            client.ClientOptions.AssociationRequestTimeoutInMs = AssociationRequestTimeoutInMs.Value;

        if (MaximumNumberOfRequestsPerAssociation is > 0)
            client.ClientOptions.MaximumNumberOfRequestsPerAssociation = MaximumNumberOfRequestsPerAssociation.Value;

        try
        {
            // Find a list of studies
            #region Query

            listener.OnNotify(this,
                new NotifyEventArgs(ProgressEventType.Information,
                    $"Requesting Studies from {dateFrom} to {dateTo}"));

            var request = CreateStudyRequestByDateRangeForModality(dateFrom, dateTo, Modality);
            request.OnResponseReceived += (req, response) =>
            {
                if (Filter(Whitelist, response))
                    studiesToOrder.Add(new StudyToFetch(response.Dataset.GetSingleValue<string>(DicomTag.StudyInstanceUID)));

            };
            requestSender.ThrottleRequest(request, client, cancellationToken.AbortToken);
            listener.OnNotify(this,
                new NotifyEventArgs(ProgressEventType.Debug,
                    $"Total filtered studies for {dateFrom} to {dateTo}is {studiesToOrder.Count}"));
            #endregion

            //go and get them
            #region Retrieval

            var transferStopwatch = new Stopwatch();

            var consecutiveFailures = 0;

            //While we have things to fetch
            while (studiesToOrder.TryTake(out var current))
            {
                transferStopwatch.Restart();
                //delay value in mills
                if (dicomConfiguration.TransferCooldownInMilliseconds != 0)
                {
                    listener.OnNotify(this,
                        new NotifyEventArgs(Verbose ? ProgressEventType.Information : ProgressEventType.Trace,
                            $"Transfers sleeping for {dicomConfiguration.TransferCooldownInMilliseconds / 1000}seconds"));
                    Task.Delay(dicomConfiguration.TransferCooldownInMilliseconds, cancellationToken.AbortToken).Wait(cancellationToken.AbortToken);
                }

                var done = false;

                //Build fetch command that Study
                var cMoveRequest = CreateCMoveByStudyUid(LocalAETitle, current.StudyUid, listener);

                //Register callbacks
                cMoveRequest.OnResponseReceived += (requ, response) =>
                {
                    listener.OnNotify(this,
                        new NotifyEventArgs(ProgressEventType.Debug,
                            $"Got {response.Status.State} response for {requ}.  Items remaining {response.Remaining}"));

                    switch (response.Status.State)
                    {
                        case DicomState.Pending:
                        case DicomState.Warning:
                            // ignore
                            break;
                        case DicomState.Cancel:
                        case DicomState.Failure:
                            consecutiveFailures++;

                            if (current.RetryCount < MaxRetries)
                            {
                                // put it back in the bag with a increased retry count
                                current.RetryCount++;
                                studiesToOrder.Add(current);
                            }

                            // final state
                            done = true;
                            break;
                        case DicomState.Success:
                            // final state
                            consecutiveFailures = 0;
                            done = true;
                            break;
                    }
                };

                //send the command to the server

                //tell user what we are sending
                listener.OnNotify(this, new NotifyEventArgs(
                    Verbose ? ProgressEventType.Information : ProgressEventType.Trace,
                    CMoveRequestToString(cMoveRequest, current.RetryCount + 1)));

                //do not use requestSender.ThrottleRequest(cMoveRequest, cancellationToken);
                //TODO is there any need to throtttle this request given its lifetime
                requestSender.ThrottleRequest(cMoveRequest, client, cancellationToken.AbortToken);


                //enforce a minimum timeout
                var swStudyTransfer = Stopwatch.StartNew();
                bool hasTransferTimedOut;

                do
                {
                    Task.Delay(Math.Max(100, dicomConfiguration.TransferPollingInMilliseconds), cancellationToken.AbortToken)
                        .Wait(cancellationToken.AbortToken);

                    hasTransferTimedOut = swStudyTransfer.ElapsedMilliseconds > dicomConfiguration.TransferTimeOutInMilliseconds;

                } while (!done && !hasTransferTimedOut);

                // Study has finished being fetched (or timed out)

                if (hasTransferTimedOut)
                    listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information,
                        $"Abandoning fetch of study {current.StudyUid}"));

                switch (consecutiveFailures)
                {
                    case > 5:
                        throw new Exception("Too many consecutive failures, giving up");
                    // 1 failure = study not available, 2 failures = system is having a bad day?
                    case <= 1:
                        continue;
                }

                //wait 4 minutes then 6 minutes then 8 minutes, eventually server will start responding again?
                var sleepFor = consecutiveFailures * 2 * 60_000;
                listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning, $"Sleeping for {sleepFor}ms due to {consecutiveFailures} consecutive failures"));

                Task.Delay(sleepFor, cancellationToken.AbortToken)
                    .Wait(cancellationToken.AbortToken);
            }

            #endregion
        }
        finally
        {
            server.Stop();
        }

        return Chunk;
    }

    #region SaveSopInstance
    protected void SaveSopInstance(DicomCStoreRequest request, SMICacheLayout cacheLayout, IDataLoadEventListener listener)
    {
        var instUid = request.SOPInstanceUID.UID;
        if (!request.HasDataset)
            return;
        // Create filepath and save
        var workingDirectory = cacheLayout.GetLoadCacheDirectory(listener);
        var filename = $"{instUid}.dcm";
        var filepath = Path.Combine(workingDirectory.FullName, filename);
        request.File.Save(filepath);
    }
    #endregion

    #region CMoveRequestToString
    private string CMoveRequestToString(DicomCMoveRequest cMoveRequest, int attempt)
    {
        var stub = $"Retrieving {cMoveRequest.Level} (attempt {attempt}) : ";
        return cMoveRequest.Level switch
        {
            DicomQueryRetrieveLevel.Patient => stub +
                                               cMoveRequest.Dataset.GetSingleValue<string>(DicomTag.PatientID),
            DicomQueryRetrieveLevel.Study => stub +
                                             cMoveRequest.Dataset.GetSingleValue<string>(DicomTag.StudyInstanceUID),
            DicomQueryRetrieveLevel.Series => stub +
                                              cMoveRequest.Dataset.GetSingleValue<string>(
                                                  DicomTag.SeriesInstanceUID),
            DicomQueryRetrieveLevel.Image => stub +
                                             cMoveRequest.Dataset.GetSingleValue<string>(DicomTag.SOPInstanceUID),
            _ => stub + DicomQueryRetrieveLevel.NotApplicable
        };
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