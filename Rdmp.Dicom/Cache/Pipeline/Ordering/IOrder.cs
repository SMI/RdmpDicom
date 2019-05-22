using Dicom.Network;

namespace Rdmp.Dicom.Cache.Pipeline.Ordering
{
    interface IOrder
    {
        void Place(Item item);
        void Place(string patId, string studyUid, string seriesUid, string sopInstance);
        void Fill(Item item);
        void Fill(string patId, string studyUid, string seriesUid, string sopInstance);
        bool IsFilled(Item item);
        bool IsFilled(string patId, string studyUid, string seriesUid, string sopInstanceUid);
        bool IsRequested(Item item);
        bool IsRequested(string patId, string studyUid, string seriesUid, string sopInstanceUid);
        IPicker NextPicker();
        bool HasNextPicker();
        DicomCMoveRequest GetDicomCMoveRequest(string destination, Item item);
        void Remove(string itemKey);
        bool Any();
        Item NextItem();
        int Total();
        int Filled();
        int Requested();
    }
}
