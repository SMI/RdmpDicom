using System;
using FellowOakDicom.Network;
using Rdmp.Core.ReusableLibraryCode.Progress;

namespace Rdmp.Dicom.Cache.Pipeline.Dicom;

public static class DicomStateExtensions
{
    public static ProgressEventType ToProgressEventType(this DicomStatus status) =>
        status.State switch
        {
            DicomState.Success => ProgressEventType.Information,
            DicomState.Cancel => ProgressEventType.Warning,
            DicomState.Pending => ProgressEventType.Information,
            DicomState.Warning => ProgressEventType.Warning,
            DicomState.Failure => ProgressEventType.Error,
            _ => throw new ArgumentOutOfRangeException(nameof(status),$"Invalid State in {status}")
        };
}