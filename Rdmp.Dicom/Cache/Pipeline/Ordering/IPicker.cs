using Dicom.Network;

namespace Rdmp.Dicom.Cache.Pipeline.Ordering
{
    interface IPicker
    {
        /// <summary>
        /// The last Item that was picked during a call to <see cref="GetDicomCMoveRequest"/>
        /// </summary>
        Item LastRequested { get; set; }

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
