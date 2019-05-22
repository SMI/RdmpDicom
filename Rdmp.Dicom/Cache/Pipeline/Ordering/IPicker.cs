using Dicom.Network;

namespace Rdmp.Dicom.Cache.Pipeline.Ordering
{
    interface IPicker
    {
        void Fill(Item item);
        void Fill(string patId, string studyUid, string seriesUid, string sopInstanceUid);
        bool IsFilled();
        DicomCMoveRequest GetDicomCMoveRequest(string destination);
        Item NextItem();
        int Total();
        int Filled();
        int Requested();
    }
}
