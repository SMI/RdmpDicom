using Dicom.Network;
using System;

namespace Rdmp.Dicom.Cache.Pipeline.Ordering
{
    class ItemsBasedPicker : IPicker
    {

        private readonly IOrder _order;
        private readonly Object _oItemsLock = new Object();

        /// <inheritdoc />
        public Item LastRequested { get; set; }

        public ItemsBasedPicker(IOrder order)
        {
            lock (_oItemsLock)
            {
                _order = order;
            }
        }

        public void Fill(Item item)
        {
            Fill(item.PatientId, item.StudyInstanceUID, item.SeriesInstanceUID, item.SOPInstanceUID);
        }

        public void Fill(string patId, string studyUid, string seriesUid, string sopInstanceUid)
        {
            lock (_oItemsLock)
            {
                _order.Fill(patId, studyUid, seriesUid, sopInstanceUid);
                _order.Remove(Item.MakeItemKey(patId, studyUid, seriesUid, sopInstanceUid));
            }
        }


        public bool IsFilled()
        {
            lock (_oItemsLock)
            {
                return _order.Any();
            }
        }

        public DicomCMoveRequest GetDicomCMoveRequest(string destination)
        {
            return _order.GetDicomCMoveRequest(destination, LastRequested = _order.NextItem());
        }

        public Item NextItem()
        {
            lock (_oItemsLock)
            {
                return _order.NextItem(); 
            }
        }
        public int Total()
        {
            return _order.Total();
        }

        public int Filled()
        {
            return _order.Filled();
        }

        public int Requested()
        {
            return _order.Requested();
        }

    }
}
