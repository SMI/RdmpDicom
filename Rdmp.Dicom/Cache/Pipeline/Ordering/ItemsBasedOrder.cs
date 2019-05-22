using System;
using System.Collections.Generic;
using System.Linq;
using Dicom.Network;
using ReusableLibraryCode.Progress;

namespace Rdmp.Dicom.Cache.Pipeline.Ordering
{
    class ItemsBasedOrder : IOrder
    {
        private readonly Object _oPickersLock = new Object();
        public readonly PlacementMode PlacementMode;
        public readonly OrderLevel OrderLevel;
        public readonly IDataLoadEventListener Listener;
        private readonly SortedDictionary<string, Item> _items = new SortedDictionary<string, Item>();
        private readonly DateTime _dateFrom;
        private readonly DateTime _dateTo;
        private Queue<ItemsBasedPicker> _pickers;

        public ItemsBasedOrder(DateTime dateFrom, DateTime dateTo, PlacementMode placementMode, OrderLevel orderLevel, IDataLoadEventListener listener)
        {
            _dateFrom = dateFrom;
            _dateTo = dateTo;
            PlacementMode = placementMode;
            OrderLevel = orderLevel;
            Listener = listener;
        }

        public ItemsBasedOrder(ItemsBasedOrder order)
        {
            _dateFrom = order._dateFrom;
            _dateTo = order._dateTo;
            PlacementMode = order.PlacementMode;
            OrderLevel = order.OrderLevel;
            Listener = order.Listener;
        }



        public void Place(Item item)
        {
            Place(item.PatientId, item.StudyInstanceUID, item.SeriesInstanceUID, item.SOPInstanceUID);
        }

        public void Place(string patId, string studyUid, string seriesUid, string sopInstance)
        {
            var key = Item.MakeItemKey(patId, studyUid, seriesUid, sopInstance);
            _items[key]=new Item(patId, studyUid, seriesUid, sopInstance);
        }

        #region Fill
        public void Fill(Item item)
        {
            Fill(item.PatientId, item.StudyInstanceUID, item.SeriesInstanceUID, item.SOPInstanceUID);
        }

        public void Fill(string patId, string studyUid, string seriesUid, string sopInstance)
        {
            var key = Item.MakeItemKey(patId, studyUid, seriesUid, sopInstance);
            if (!_items.ContainsKey(key))
            {
                if (PlacementMode == PlacementMode.PlaceThenFill)
                {
                    Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, "DicomRetriever.Order.Order Attempt to fill order prior to placement" + patId + "-" + studyUid + "-" + seriesUid + "-" + sopInstance));
                    return;
                }
                _items[key] = new Item(patId, studyUid, seriesUid, sopInstance);
            }
            _items[key].Fill();
        }
        #endregion

        #region IsFilled
        public bool IsFilled(Item item)
        {
            return IsFilled(item.PatientId, item.StudyInstanceUID, item.SeriesInstanceUID, item.SOPInstanceUID);
        }

        public bool IsFilled(string patId, string studyUid, string seriesUid, string sopInstanceUid)
        {
            var key = Item.MakeItemKey(patId, studyUid, seriesUid, sopInstanceUid);
            if (!_items.ContainsKey(key))
            {
                return _items[key].IsFilled;
            }
            else
            {
                Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, "DicomRetriever.Order.IsFilled Attempt to request IsFilled for unordered image: " + sopInstanceUid));
            }
            return false;
        }
        #endregion

        #region IsRequested
        public bool IsRequested(Item item)
        {
            return IsRequested(item.PatientId, item.StudyInstanceUID, item.SeriesInstanceUID, item.SOPInstanceUID);
        }

        public bool IsRequested(string patId, string studyUid, string seriesUid, string sopInstanceUid)
        {
            var key = Item.MakeItemKey(patId, studyUid, seriesUid, sopInstanceUid);
            if (!_items.ContainsKey(key))
            {
                return _items[key].IsRequested;
            }
            else
            {
                Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, "DicomRetriever.Order.IsFilled Attempt to request IsRequested for unordered image: " + sopInstanceUid));
            }
            return false;
        }
        #endregion

        public IPicker NextPicker()
        {
            lock (_oPickersLock)
            {
                if (_pickers == null)
                {
                    InitialisePickers();
                }
                return _pickers.Dequeue();
            }
        }

        public bool HasNextPicker()
        {
            lock (_oPickersLock)
            {
                if (_pickers == null)
                {
                    InitialisePickers();
                }
                return _pickers.Any();
            }
        }


        #region GetDicomCMoveRequest
        public DicomCMoveRequest GetDicomCMoveRequest(string destination, Item item)
        {
            foreach (var keyValuePair in GetList(Item.MakeKeyFilter(item, OrderLevel)))
            {
                keyValuePair.Value.Request();
            }
            switch (OrderLevel)
            {
                case OrderLevel.Patient:
                    return CreateCMoveByPatientId(destination, item.PatientId);
                //break;
                case OrderLevel.Study:
                    return CreateCMoveByStudyUid(destination, item.StudyInstanceUID);
                //break;
                case OrderLevel.Series:
                    return CreateCMoveBySeriesUid(destination, item.StudyInstanceUID, item.SeriesInstanceUID);
                //break;
                case OrderLevel.Image:
                    return CreateCMoveBySopInstanceUid(destination, item.StudyInstanceUID, item.SeriesInstanceUID, item.SOPInstanceUID);
                default:
                    Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, "DicomRetriever.getDicomCMoveRequest Attempt to request item at unsupported level: " + OrderLevel));
                    return null;

            }
        }
        #endregion

        public void Remove(string itemKey)
        {
            _items.Remove(itemKey);
        }

        public bool Any()
        {
            return _items.Any();
        }

        public Item NextItem()
        {
            return _items.FirstOrDefault().Value;
        }

        public int Total()
        {
            return _items.Count();
        }

        public int Filled()
        {
            return _items.Values.Count(item => item.IsFilled);
        }

        public int Requested()
        {
            return _items.Values.Count(item => item.IsRequested);
        }

        #region CreateCMoveByPatientId
        private DicomCMoveRequest CreateCMoveByPatientId(string destination, string patientId, DicomPriority priority = DicomPriority.Low)
        {

            throw new NotImplementedException();
            //var request = new DicomCMoveRequest(destination, _patientId);
            //// no more dicomtags have to be set
            //return request;
        }
        #endregion

        #region CreateCMoveByStudyUid
        private DicomCMoveRequest CreateCMoveByStudyUid(string destination, string studyUid, DicomPriority priority = DicomPriority.Low)
        {
            var request = new DicomCMoveRequest(destination, studyUid,priority);
            Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "DicomRetriever.CreateCMoveByStudyUid created request for: " + studyUid));
            // no more dicomtags have to be set
            return request;
        }
        #endregion

        #region CreateCMoveBySeriesUid
        private DicomCMoveRequest CreateCMoveBySeriesUid(string destination, string studyUid, string seriesUid, DicomPriority priority = DicomPriority.Low)
        {
            var request = new DicomCMoveRequest(destination, studyUid, seriesUid,priority);
            Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "DicomRetriever.CreateCMoveBySEriesUid created request for: " + seriesUid));
            // no more dicomtags have to be set
            return request;
        }
        #endregion

        #region CreateCMoveBySopInstanceUid
        private DicomCMoveRequest CreateCMoveBySopInstanceUid(string destination, string studyUid, string seriesUid, string sopInstanceUid, DicomPriority priority = DicomPriority.Low)
        {
            var request = new DicomCMoveRequest(destination, studyUid, seriesUid, sopInstanceUid,priority);
            Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "DicomRetriever.CreateCMoveBySopInstanceUid created request for: " + sopInstanceUid));
            // no more dicomtags have to be set
            return request;

        }
        #endregion

        #region pickers
        private Queue<ItemsBasedPicker> Split()
        {

            switch (OrderLevel)
            {
                case OrderLevel.Patient:
                case OrderLevel.Study:
                case OrderLevel.Series:
                case OrderLevel.Image:
                    var pickers = new Queue<ItemsBasedPicker>();
                    var order = new ItemsBasedOrder(this);
                    if (!Any()) return pickers;
                    ItemsBasedPicker picker;
                    var first = NextItem();
                    var currentKey = Item.MakeKeyFilter(first, OrderLevel);
                    foreach (var item in _items.Values)
                    {
                        var nextKey = Item.MakeKeyFilter(item, OrderLevel);
                        if (!nextKey.Equals(currentKey))
                        {
                            order = new ItemsBasedOrder(this);
                            picker = new ItemsBasedPicker(order);
                            pickers.Enqueue(picker);
                            currentKey = nextKey;
                        }
                        order.CopyRef(item);
                    }
                    return pickers;
                        default:
                        Listener.OnNotify(this,
                            new NotifyEventArgs(ProgressEventType.Error,
                                "DicomRetriever.getDicomCMoveRequest Attempt to request item at unsupported level: " +
                                OrderLevel));
                        return null;
                    
            }

        }

        private void CopyRef(Item item)
        {
            var key = Item.MakeItemKey(item);
            _items[key] = item;
        }

        private void InitialisePickers()
        {
            lock (_oPickersLock)
            {
                if (_pickers == null)
                {
                    _pickers = Split();
                }
            }

        }
        #endregion

        private IEnumerable<KeyValuePair<string,Item>>  GetList(string keyMatch)
        {
            return _items.Where(kvp => kvp.Key.StartsWith(keyMatch)).OrderBy(entry => entry.Key);
        }

    }

}
