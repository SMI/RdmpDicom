using Dicom.Network;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Rdmp.Dicom.Cache.Pipeline.Ordering
{
    class HierarchyBasedPicker : IPicker
    {
        private readonly HierarchyBasedOrder _order;
        private Dictionary<string,Item> _items;
        private readonly Object _oItemsLock = new Object();
        private DicomCMoveRequest _dicomCMoveRequest;

        /// <inheritdoc />
        public Item LastRequested { get; set; }

        public HierarchyBasedPicker(HierarchyBasedOrder order)
        {
            _order = order;
        }

        public void Fill(Item item)
        {
            Fill(item.PatientId, item.StudyInstanceUID, item.SeriesInstanceUID, item.SOPInstanceUID);
        }

        public void Fill(string patId, string studyUid, string seriesUid, string sopInstanceUid)
        {
            _order.Fill(patId, studyUid, seriesUid, sopInstanceUid);
            lock (_oItemsLock)
            {
                _items.Remove(_order.MakeItemKey(patId, studyUid, seriesUid, sopInstanceUid));
            }
        }


        public bool IsFilled()
        {
            lock (_oItemsLock)
            {
                return _items.Count == 0;
            }
        }

        public DicomCMoveRequest GetDicomCMoveRequest(string destination, out int attempt)
        {
            if(_items == null)
            {
                lock (_oItemsLock)
                {
                    _items = _order.GetPickerList();
                }
            }
            if(_dicomCMoveRequest == null)
            {
                _dicomCMoveRequest = _order.GetDicomCMoveRequest(destination, LastRequested = _items.Values.FirstOrDefault());
            }
            attempt = _order.attemptcount(LastRequested);
            return _dicomCMoveRequest;
        }

        public Item NextItem()
        {
            lock (_oItemsLock)
            {
                return _items.Values.FirstOrDefault();
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

        public void RetryOnce()
        {
            lock(_oItemsLock)
            {
                _order.Retry(LastRequested);
                _items.Clear();
            }
        }
    }
}
