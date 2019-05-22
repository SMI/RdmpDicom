using System;
using Dicom.Network;
using ReusableLibraryCode.Progress;

namespace Rdmp.Dicom.Cache.Pipeline.Dicom
{
    public static class DicomStateExtensions
    {
        public static ProgressEventType ToProgressEventType(this DicomStatus status)
        {
            switch (status.State)
            {
                case DicomState.Success:
                    return ProgressEventType.Information;
                case DicomState.Cancel:
                    return ProgressEventType.Warning;
                case DicomState.Pending:
                    return ProgressEventType.Information;
                case DicomState.Warning:
                    return ProgressEventType.Warning;
                case DicomState.Failure:
                    return ProgressEventType.Error;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}