using FellowOakDicom;
using FellowOakDicom.Network;
using System;
using System.Text;
using System.Threading.Tasks;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.Memory;
using Rdmp.Core.ReusableLibraryCode.Progress;
using Microsoft.Extensions.Logging;
using FellowOakDicom.Log;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Rdmp.Dicom.Cache.Pipeline;

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

        // Lossy - if that's all the PACS has, that's all it can give us
        DicomTransferSyntax.JPEGLSNearLossless,
        DicomTransferSyntax.JPEG2000Lossy,
        DicomTransferSyntax.JPEGProcess1,
        DicomTransferSyntax.JPEGProcess2_4,

        // Also allow video files, just in case
        DicomTransferSyntax.HEVCH265Main10ProfileLevel51,
        DicomTransferSyntax.HEVCH265MainProfileLevel51,
        DicomTransferSyntax.MPEG2,
        DicomTransferSyntax.MPEG2MainProfileHighLevel,
        DicomTransferSyntax.MPEG4AVCH264BDCompatibleHighProfileLevel41,
        DicomTransferSyntax.MPEG4AVCH264HighProfileLevel41,
        DicomTransferSyntax.MPEG4AVCH264HighProfileLevel42For2DVideo,
        DicomTransferSyntax.MPEG4AVCH264HighProfileLevel42For3DVideo,
        DicomTransferSyntax.MPEG4AVCH264StereoHighProfileLevel42,

        // Uncompressed
        DicomTransferSyntax.ExplicitVRLittleEndian,
        DicomTransferSyntax.ExplicitVRBigEndian,
        DicomTransferSyntax.ImplicitVRLittleEndian
    };
    #endregion

    public static string LocalAet { get; set; }
    public static IDataLoadEventListener Listener { get; set; }
    public static bool Verbose { get; set; } = true;
    public static Action<DicomCStoreRequest, DicomCStoreResponse> OnEndProcessingCStoreRequest;
    private string CalledAE = string.Empty;
    private string CallingAE = string.Empty;

    private static readonly DicomServiceDependencies Dependencies =new(new LoggerFactory(),new DesktopNetworkManager(),new DefaultTranscoderManager(),new ArrayPoolMemoryProvider());
    public CachingSCP(INetworkStream stream, Encoding encoding, ILogger logger): base(stream, encoding, logger, Dependencies)
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
        return SendAssociationReleaseResponseAsync();
    }
    #endregion

    #region OnReceiveAbort
    public void OnReceiveAbort(DicomAbortSource source, DicomAbortReason reason)
    {
        var msg = $"Received abort from {source}for {reason}";
        Logger.LogWarning(msg, source, reason);
        Listener.OnNotify(this,new NotifyEventArgs(ProgressEventType.Warning, $"Aborted: {msg}"));
    }
    #endregion

    #region OnConnectionClosed
    public void OnConnectionClosed(Exception e)
    {
        var msg = "Connection closed";
        if (e != null) msg += e.Message + e.StackTrace;
        Logger.LogInformation(msg, e);
        Listener.OnNotify(this,new NotifyEventArgs(Verbose ? ProgressEventType.Information : ProgressEventType.Trace,
            $"ConnectionClosed: {msg}"));
    }
    #endregion

    #region OnCStoreRequest
    public Task<DicomCStoreResponse> OnCStoreRequestAsync(DicomCStoreRequest request)
    {
        Listener.OnNotify(this, new NotifyEventArgs(Verbose ? ProgressEventType.Information : ProgressEventType.Trace,
            $"Received CStore Request: {request.SOPInstanceUID}"));
        DicomCStoreResponse response;
        try
        {
            response = new DicomCStoreResponse(request, DicomStatus.Success);
            OnEndProcessingCStoreRequest(request, response);
        }
        catch (Exception e)
        {
            Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error,
                $"Failed CStore: {request.SOPInstanceUID}", e));
            response = new DicomCStoreResponse(request, DicomStatus.ProcessingFailure);
        }
        Listener.OnNotify(this, new NotifyEventArgs(Verbose ? ProgressEventType.Information : ProgressEventType.Trace,
            $"Sending CStore Response: {response.Status} from AET: {CalledAE} to AET:{CallingAE}"));
        return Task.FromResult(response);
    }
    #endregion

    #region OnCStoreRequestException
    public async Task OnCStoreRequestExceptionAsync(string tempFileName, Exception e)
    {
        var msg = "CStore request exception";
        if (e != null) msg += e.Message + e.StackTrace;
        Logger.LogInformation(msg, e);
        await Task.Run(()=>Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, "CStoreRequest failed", e)));
    }
    #endregion

    #region DicomCEchoRequest
    public Task<DicomCEchoResponse> OnCEchoRequestAsync(DicomCEchoRequest request)
    {
        return Task.FromResult(new DicomCEchoResponse(request, DicomStatus.Success));
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
        Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Trace,
            $"Max Async OPs invocable = {association.MaxAsyncOpsInvoked}performable = {association.MaxAsyncOpsPerformed}"));
        Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Trace,
            $"Accepted Association Request from {CallingAE} to {CalledAE}"));
    }
    #endregion

    #region OnReceiveAssociationReleaseRequest
    public void OnReceiveAssociationReleaseRequest()
    {
        Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Trace,
            $"Released Association from {CallingAE} to {CalledAE}"));
    }
    #endregion



}