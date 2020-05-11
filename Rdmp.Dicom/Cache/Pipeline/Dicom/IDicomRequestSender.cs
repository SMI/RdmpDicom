using Dicom.Network;
using Rdmp.Core.DataFlowPipeline;

namespace Rdmp.Dicom.Cache.Pipeline.Dicom
{
    public interface IDicomRequestSender
    {
        void ThrottleRequest(DicomRequest dicomRequest, DicomClient client,
            GracefulCancellationToken cancellationToken);

        void SendRequest(DicomRequest dicomRequest);

        void SendRequest(DicomRequest dicomRequest, DicomClient client);
    }
}