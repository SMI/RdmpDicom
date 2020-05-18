using System.Threading;
using Dicom.Network;
using Rdmp.Core.DataFlowPipeline;
using Dicom.Network.Client;
using DicomClient = Dicom.Network.Client.DicomClient;

namespace Rdmp.Dicom.Cache.Pipeline.Dicom
{
    public interface IDicomRequestSender
    {
        void ThrottleRequest(DicomRequest dicomRequest, DicomClient client, CancellationToken cancellationToken);
        
        void SendRequest(DicomRequest dicomRequest, DicomClient client, CancellationToken cancellationToken);
    }
}