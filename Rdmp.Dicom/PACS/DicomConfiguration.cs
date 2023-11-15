using System;
using FellowOakDicom.Network;

namespace Rdmp.Dicom.PACS;

public class DicomConfiguration
{
    public string RemoteAetHost { get; set; }
    public ushort RemoteAetPort { get; set; }
    public string RemoteAetTitle { get; set; }
    public string LocalAetHost { get; set; }
    public ushort LocalAetPort { get; set; }
    public string LocalAetTitle { get; set; }
    public int RequestCooldownInMilliseconds { get; set; }
    public int TransferCooldownInMilliseconds { get; set; }
    public int TransferPollingInMilliseconds { get; set; }
    public int TransferTimeOutInMilliseconds { get; set; }
    public DicomPriority Priority { get; set; }

}