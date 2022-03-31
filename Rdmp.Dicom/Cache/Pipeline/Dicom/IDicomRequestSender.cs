using System.Threading;
using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;

namespace Rdmp.Dicom.Cache.Pipeline.Dicom;

public interface IDicomRequestSender
{
    void ThrottleRequest(DicomRequest dicomRequest, DicomClient client, CancellationToken cancellationToken);

    void SendRequest(DicomRequest dicomRequest, DicomClient client, CancellationToken cancellationToken);
}
