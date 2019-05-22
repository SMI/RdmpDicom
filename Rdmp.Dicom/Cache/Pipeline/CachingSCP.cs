using Dicom;
using Dicom.Log;
using Dicom.Network;
using System;
using System.Text;
using System.Threading.Tasks;
using ReusableLibraryCode.Progress;
using Rdmp.Core.Logging;

namespace Rdmp.Dicom.Cache.Pipeline
{
    public class CachingSCP : DicomService, IDicomServiceProvider, IDicomCStoreProvider, IDicomCEchoProvider
    {
        #region Transfer Syntaxes
        private static readonly DicomTransferSyntax[] AcceptedTransferSyntaxes =
        {
            DicomTransferSyntax.ExplicitVRLittleEndian,
            DicomTransferSyntax.ExplicitVRBigEndian,
            DicomTransferSyntax.ImplicitVRLittleEndian
        };

        private static readonly DicomTransferSyntax[] AcceptedImageTransferSyntaxes =
        {
            // Lossless
            DicomTransferSyntax.JPEGLSLossless,
            DicomTransferSyntax.JPEG2000Lossless,
            DicomTransferSyntax.JPEGProcess14SV1,
            DicomTransferSyntax.JPEGProcess14,
            DicomTransferSyntax.RLELossless,
			
            // Uncompressed
            DicomTransferSyntax.ExplicitVRLittleEndian,
            DicomTransferSyntax.ExplicitVRBigEndian,
            DicomTransferSyntax.ImplicitVRLittleEndian
        };
        #endregion

        public ILogManager LogManager { get; set; }
        public static string LocalAet { get; set; }
        public static IDataLoadEventListener Listener { get; set; }
        public static Action<DicomCStoreRequest, DicomCStoreResponse> OnEndProcessingCStoreRequest;
        private String CalledAE = String.Empty;
        private String CallingAE = String.Empty;

        public CachingSCP(INetworkStream stream, Encoding encoding, Logger logger): base(stream, encoding, logger)
        {
            Options.LogDimseDatasets = false;
            Options.LogDataPDUs = false;
       }


        #region OnReceiveAssociationRequestAsync
        public Task OnReceiveAssociationRequestAsync(DicomAssociation association)
        {
            OnReceiveAssociationRequest(association);
            return SendAssociationAcceptAsync(association);
        }
        #endregion

        #region OnReceiveAssociationReleaseRequestAsync
        public Task OnReceiveAssociationReleaseRequestAsync()
        {
            OnReceiveAssociationReleaseRequest();
            return SendAssociationReleaseRequestAsync();
        }
        #endregion

        #region OnReceiveAbort
        public void OnReceiveAbort(DicomAbortSource source, DicomAbortReason reason)
        {
            var msg = "Received abort from " + source + "for " + reason;
            Logger.Warn(msg, new object[] {source, reason});
            Listener.OnNotify(this,new NotifyEventArgs(ProgressEventType.Warning, "Aborted: "+msg));
        }
        #endregion

        #region OnConnectionClosed
        public void OnConnectionClosed(Exception e)
        {
            var msg = "Connection closed";
            if (e != null) msg += e.Message + e.StackTrace;
            Logger.Info(msg, e);
            Listener.OnNotify(this,new NotifyEventArgs(ProgressEventType.Information, "ConnectionClosed: "+ msg)); 
        }
        #endregion

        #region OnCStoreRequest
        public DicomCStoreResponse OnCStoreRequest(DicomCStoreRequest request)
        {
            Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "Received CStore Request: " + request.SOPInstanceUID));
            DicomCStoreResponse response;
            try
            {
                response = new DicomCStoreResponse(request, DicomStatus.Success);
                OnEndProcessingCStoreRequest(request, response);
            }
            catch (Exception e)
            {
                Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, "Failed CStore: " + request.SOPInstanceUID, e));
                response = new DicomCStoreResponse(request, DicomStatus.ProcessingFailure);
            }
            Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "Sending CStore Response: " + response.Status + " from AET: " + CalledAE + " to AET:" + CallingAE));
            return response;
        }
        #endregion

        #region OnCStoreRequestException
        public void OnCStoreRequestException(string tempFileName, Exception e)
        {
            var msg = "CStore request exception";
            if (e != null) msg += e.Message + e.StackTrace;
            Logger.Info(msg, e);
            Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, "CStoreRequest failed", e));
        }
        #endregion

        #region DicomCEchoResponse
        public DicomCEchoResponse OnCEchoRequest(DicomCEchoRequest request)
        {
            return new DicomCEchoResponse(request, DicomStatus.Success);
        }
        #endregion

        #region OnReceiveAssociationRequest
        public void OnReceiveAssociationRequest(DicomAssociation association)
        {
            // Client hasn't been configured correctly
            if (string.IsNullOrWhiteSpace(LocalAet))
                throw new Exception("LocalAet cannot be null");


            if (!string.Equals(association.CalledAE, LocalAet, StringComparison.CurrentCultureIgnoreCase))
            {
                SendAssociationRejectAsync(DicomRejectResult.Permanent, DicomRejectSource.ServiceUser, DicomRejectReason.CalledAENotRecognized);
                return;
            }

            foreach (var pc in association.PresentationContexts)
            {
                if (pc.AbstractSyntax == DicomUID.Verification)
                    pc.AcceptTransferSyntaxes(AcceptedTransferSyntaxes);
                else if (pc.AbstractSyntax.StorageCategory != DicomStorageCategory.None)
                    pc.AcceptTransferSyntaxes(AcceptedImageTransferSyntaxes);
            }
            CalledAE = association.CalledAE;
            CallingAE = association.CallingAE;
            Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Trace, "Max Async OPs invocable = " + association.MaxAsyncOpsInvoked + "performable = " + association.MaxAsyncOpsPerformed));
            Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Trace, "Accepted Association Request from "+ CallingAE +" to "+ CalledAE));
        }
        #endregion

        #region OnReceiveAssociationReleaseRequest
        public void OnReceiveAssociationReleaseRequest()
        {
            Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Trace, "Released Association from " + CallingAE + " to " + CalledAE));
        }
        #endregion



    }
}