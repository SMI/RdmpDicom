using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Dicom.Network;
using ReusableLibraryCode.Progress;
using Rdmp.Dicom.PACS;
using Rdmp.Core.DataFlowPipeline;

namespace Rdmp.Dicom.Cache.Pipeline.Dicom
{
    public class DicomRequestSender : IDicomRequestSender
    {
        public int SendTimeout { get; private set; }

        private readonly DicomConfiguration _dicomConfiguration;
        private readonly IDataLoadEventListener _listener;
        private readonly Stopwatch _moveRequestTimer = new Stopwatch();
        public delegate void OnCheckExceptionDelegate(Exception ex);
        public delegate void OnCheckTimeoutDelegate();
        public delegate void OnCheckSucessDelegate();

        public OnCheckExceptionDelegate OnRequestException;
        public OnCheckTimeoutDelegate OnRequestTimeout;
        public OnCheckSucessDelegate OnRequestSucess;

        public DicomRequestSender(DicomConfiguration dicomConfiguration, IDataLoadEventListener listener)
        {
            _dicomConfiguration = dicomConfiguration;
            _listener = listener;
        }

        /// <summary>
        /// Check
        /// </summary>
        #region Check
        public void Check()
        {
            var echoRequest = new DicomCEchoRequest();
            SendRequest(echoRequest);
        }
        #endregion


        /// <summary>
        ///    Throttle requests using W(O) = mO(t) + c where W is the wait period, O is the opertaion duration, m and c are positive constants 
        ///    The request is added to the client which is unreleased at the end of this request send.
        /// </summary>
        /// 
        #region ThrottleRequest
        public void ThrottleRequest(DicomRequest dicomRequest, DicomClient client, GracefulCancellationToken cancellationToken)
        {
            client.AddRequest(dicomRequest);
            ThrottleRequest(client, cancellationToken);
        }
        #endregion

        /// <summary>
        ///    Throttle requests using W(O) = mO(t) + c where W is the wait period, O is the opertaion duration, m and c are positive constants 
        ///    Sends requests added to the client is unreleased at the end of this request send.
        /// </summary>
        /// 
        #region ThrottleRequest
        public void ThrottleRequest(DicomClient client, GracefulCancellationToken cancellationToken)
        {
            var transferTimer = new Stopwatch();
            transferTimer.Start();
            SendRequest(client);
            transferTimer.Stop();
            //valuein mills
            var delay = ((int)(_dicomConfiguration.RequestDelayFactor * (1000 * transferTimer.Elapsed.Seconds)) + _dicomConfiguration.RequestCooldownInMilliseconds);
            if (delay > 0)
            {
                _listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "Requests sleeping for " + delay / 1000 + "seconds"));
                Task.Delay(delay, cancellationToken.AbortToken).Wait(cancellationToken.AbortToken);
            }
        }
        #endregion


        /// <summary>
        ///     Blocks until the request is received so calling code doesn't have to deal with asynchrony (see the EventWaitHandle in TrySend).
        ///     Only the timeout is applied no Throtelling
        /// </summary>
        /// <param name="dicomRequest"></param>
        #region SendRequest
        public void SendRequest(DicomRequest dicomRequest)
        {
            var client = new DicomClient();
            SendRequest(dicomRequest, client);
            client.Release();

        }
        #endregion


        /// <summary>
        ///     Blocks until the request is received so calling code doesn't have to deal with asynchrony (see the EventWaitHandle in TrySend).
        ///     Only the timeout is applied no Throtelling, the client is unreleased on return 
        /// </summary>
        /// <param name="dicomRequest"></param>
        /// <param name="client"></param>

        #region SendRequest
        public void SendRequest(DicomRequest dicomRequest, DicomClient client)
        {
            client.AddRequest(dicomRequest);
            SendRequest(client);
        }
        #endregion
        /// <summary>
        ///     Blocks until the request is received so calling code doesn't have to deal with asynchrony (see the EventWaitHandle in TrySend).
        ///     Only the timeout is applied no Throtelling, the client is unreleased on return 
        /// </summary>
        /// <param name="dicomRequest"></param>
        /// <param name="client"></param>
        #region SendRequest
        public void SendRequest(DicomClient client)
        {
            _listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "Sending request to " + _dicomConfiguration.RemoteAetTitle + " at " + _dicomConfiguration.RemoteAetUri.Host + ":" + _dicomConfiguration.RemoteAetUri.Port));
            var t = new Task(() =>
            {
                try
                {
                    client.Send(_dicomConfiguration.RemoteAetUri.Host, _dicomConfiguration.RemoteAetUri.Port, false, _dicomConfiguration.LocalAetTitle, _dicomConfiguration.RemoteAetTitle);

                }
                catch (Exception ex)
                {
                    if (OnRequestException != null) OnRequestException(ex);
                    throw new Exception("Error when attempting to send DICOM request: " + ex.Message, ex);
                }
            }
                );
            //var canceller = new CancellationTokenSource();
            t.Start();
            //allow some extra time.
            var canceller = new CancellationTokenSource();
            t.Wait(_dicomConfiguration.TransferTimeOutInMilliseconds + 1000, canceller.Token);

            if (!t.IsCompleted)
            {
                if (OnRequestTimeout != null) OnRequestTimeout();
                canceller.Cancel(true);
            }
            else
                if (OnRequestSucess != null) OnRequestSucess();

        }
        #endregion


    }
}