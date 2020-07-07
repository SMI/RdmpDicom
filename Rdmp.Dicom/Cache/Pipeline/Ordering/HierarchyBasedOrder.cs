using Dicom.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using ReusableLibraryCode.Progress;
using System.Collections.Concurrent;

namespace Rdmp.Dicom.Cache.Pipeline.Ordering
{
    public enum PlacementMode { PlaceThenFill, PlaceAndFill };
    public enum OrderLevel { Patient, Study, Series, Image };

    class HierarchyBasedOrder : IOrder
    {

        //TODO make interfaces for items Add and Fill
        private readonly Object _oPickersLock = new Object();
        public readonly PlacementMode PlacementMode;
        public readonly OrderLevel OrderLevel;
        public readonly IDataLoadEventListener Listener;
        private readonly Dictionary<string, Patient> _patients = new Dictionary<string, Patient>();
        private readonly DateTime _dateFrom;
        private readonly DateTime _dateTo;
        private readonly HierarchyBasedOrder parent;
        private Queue<HierarchyBasedPicker> _pickers;
        private readonly ConcurrentDictionary<Item,int> _retried = new ConcurrentDictionary<Item,int>();

        public const int MaxAttempts = 2;

        public HierarchyBasedOrder(HierarchyBasedOrder order)
        {
            _dateFrom = order._dateFrom;
            _dateTo = order._dateTo;
            PlacementMode = order.PlacementMode;
            OrderLevel = order.OrderLevel;
            Listener = order.Listener;
            parent = order;
        }

        public HierarchyBasedOrder(DateTime dateFrom, DateTime dateTo, PlacementMode placementMode, OrderLevel orderLevel, IDataLoadEventListener listener)
        {
            _dateFrom = dateFrom;
            _dateTo = dateTo;
            PlacementMode = placementMode;
            OrderLevel = orderLevel;
            Listener = listener;
            parent = null;
        }

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

        /// <summary>
        /// Returns a count of how often we have attempted to fetch the given <paramref name="lastRequested"/> via CMove
        /// </summary>
        /// <param name="lastRequested"></param>
        /// <returns></returns>
        public int GetAttemptCount(Item lastRequested)
        {
            return _retried.GetOrAdd(lastRequested ,1);
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

        public void InitialisePickers()
        {
            lock (_oPickersLock)
            {
                if (_pickers == null)
                {
                   _pickers = Split();
                }
            }

        }

        internal void Retry(Item item)
        {
            if (parent is null)
            {                
                var attempts = _retried.AddOrUpdate(item ,(k)=>2,(k,v)=>v++);

                if(attempts > MaxAttempts)
                    return;
                
                HierarchyBasedOrder order = null;
                HierarchyBasedPicker picker = null;
                order = new HierarchyBasedOrder(this);
                picker = new HierarchyBasedPicker(order);
                order.Place(item);

                var items = _pickers.ToArray();
                _pickers.Clear();
                _pickers.Enqueue(picker);
                foreach (var p in items)
                    _pickers.Enqueue(p);
            }
            else
                parent.Retry(item);
        }

        //TODO Non-Elegant
        private Queue<HierarchyBasedPicker> Split()
        {
            var pickers = new Queue<HierarchyBasedPicker>();
            HierarchyBasedOrder order = null;
            HierarchyBasedPicker picker = null;
            foreach (var patient in _patients.Values)
            {
                if(OrderLevel == OrderLevel.Patient)
                {
                    order = new HierarchyBasedOrder(this);
                    picker = new HierarchyBasedPicker(order);
                }
                foreach (var study in patient.Studies.Values)
                {
                    if (OrderLevel == OrderLevel.Study)
                    {
                        order = new HierarchyBasedOrder(this);
                        picker = new HierarchyBasedPicker(order);
                    }
                    foreach (var series in study.Series.Values)
                    {
                        if (OrderLevel == OrderLevel.Series)
                        {
                            order = new HierarchyBasedOrder(this);
                            picker = new HierarchyBasedPicker(order);
                        }
                        foreach (var image in series.Images.Values)
                        {
                            if (OrderLevel == OrderLevel.Image)
                            {
                                order = new HierarchyBasedOrder(this);
                                picker = new HierarchyBasedPicker(order);
                            }
                            if (order != null)
                            {
                                order.Place(patient.PatientId, study.StudyInstanceUID, series.SeriesInstanceUID, image.SOPInstanceUID);
                            }
                            else
                            {
                                Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, "Order.Split order is null"));
                            }
                            if (OrderLevel == OrderLevel.Image)
                            {
                                pickers.Enqueue(picker);
                            }
                        }
                        if (OrderLevel == OrderLevel.Series)
                        {
                            pickers.Enqueue(picker);
                        }
                    }
                    if (OrderLevel == OrderLevel.Study)
                    {
                        pickers.Enqueue(picker);
                    }
                }
                if (OrderLevel == OrderLevel.Patient)
                {
                    pickers.Enqueue(picker);
                }
            }
            return pickers;
        }



        //TODO remove new Item
        public Dictionary<string, Item> GetPickerList()
        {
            var items = new Dictionary<string, Item>();
            foreach (var patient in _patients.Values)
            {
                foreach (var study in patient.Studies.Values)
                {
                    foreach (var series in study.Series.Values)
                    {
                        foreach (var image in series.Images.Values)
                        {
                            var item = new Item(patient.PatientId, study.StudyInstanceUID, series.SeriesInstanceUID, image.SOPInstanceUID);
                            items.Add(MakeItemKey(item), item);
                        }
                    }

                }

            }
            return items;
        }

        public void Place(Item item)
        {
            Place(item.PatientId,item.StudyInstanceUID,item.SeriesInstanceUID,item.SOPInstanceUID);
        }

        public void Place(string patId, string studyUid, string seriesUid, string sopInstance)
        {
            if (_patients.ContainsKey(patId))
            {
                _patients[patId].Add(studyUid, seriesUid, sopInstance);
            }
            else
            {
                _patients.Add(patId, new Patient(patId,studyUid, seriesUid, sopInstance, PlacementMode, OrderLevel,Listener));
            }
        }

        public void Fill(Item item)
        {
            Fill(item.PatientId, item.StudyInstanceUID, item.SeriesInstanceUID, item.SOPInstanceUID);
        }

        public void Fill(string patId, string studyUid, string seriesUid, string sopInstance)
        {
            if (!_patients.ContainsKey(patId))
            {
                if (PlacementMode == PlacementMode.PlaceThenFill)
                {
                    Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, "DicomRetriever.Order.Order Attempt to fill order prior to placement" + patId + "-" + studyUid + "-" + seriesUid + "-" + sopInstance));
                    return;
                }
                _patients.Add(patId, new Patient(patId, studyUid, seriesUid, sopInstance, PlacementMode, OrderLevel, Listener));
                if(parent != null) parent._patients.Add(patId, new Patient(patId, studyUid, seriesUid, sopInstance, PlacementMode, OrderLevel, Listener));
            }
            _patients[patId].Fill(studyUid, seriesUid, sopInstance);
        }

        public bool IsFilled(Item item)
        {
            return IsFilled(item.PatientId, item.StudyInstanceUID, item.SeriesInstanceUID, item.SOPInstanceUID);
        }

        //TODO Non-Elegant
        public bool IsFilled(string patId, string studyUid, string seriesUid, string sopInstanceUid)
        {
            if (_patients.ContainsKey(patId))
            {
                var patient = _patients[patId];
                if (OrderLevel == OrderLevel.Patient) return patient.IsFilled();
                if (patient.Studies.ContainsKey(studyUid))
                {
                    var study = patient.Studies[studyUid];
                    if (OrderLevel == OrderLevel.Study) return study.IsFilled();
                    if (study.Series.ContainsKey(seriesUid))
                    {
                        var series = study.Series[seriesUid];
                        if (OrderLevel == OrderLevel.Series) return series.IsFilled();
                        if (series.Images.ContainsKey(sopInstanceUid))
                        {
                            return series.Images[sopInstanceUid].IsFilled;
                        }
                        else
                        {
                            Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, "DicomRetriever.Order.IsFilled Attempt to request IsFilled for unordered image: " + sopInstanceUid));
                        }
                    }
                    else
                    {
                        Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, "DicomRetriever.Order.IsFilled Attempt to request IsFilled for unordered series: " + seriesUid));
                    }
                }
                else
                {
                    Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, "DicomRetriever.Order.IsFilled Attempt to request IsFilled for unordered study: " + studyUid));
                }
            }
            Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, "DicomRetriever.Order.IsFilled Attempt to request IsFilled for unordered patient: " + patId));
            return false;
        }


        public bool IsRequested(Item item)
        {
            return IsRequested(item.PatientId, item.StudyInstanceUID, item.SeriesInstanceUID, item.SOPInstanceUID);
        }
        //TODO Non-Elegant
        public bool IsRequested(string patId, string studyUid, string seriesUid, string sopInstanceUid)
        {
            if (_patients.ContainsKey(patId))
            {
                var patient = _patients[patId];
                if (OrderLevel == OrderLevel.Patient) return patient.IsRequested();
                if (patient.Studies.ContainsKey(studyUid))
                {
                    var study = patient.Studies[studyUid];
                    if (OrderLevel == OrderLevel.Study) return study.IsRequested();
                    if (study.Series.ContainsKey(seriesUid))
                    {
                        var series = study.Series[seriesUid];
                        if (OrderLevel == OrderLevel.Series) return series.IsRequested();
                        if (series.Images.ContainsKey(sopInstanceUid))
                        {
                            return series.Images[sopInstanceUid].IsRequested;
                        }
                        Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, "DicomRetriever.Order.IsFilled Attempt to request IsFilled for unordered image: " + sopInstanceUid));
                    }
                    else
                    {
                        Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, "DicomRetriever.Order.IsFilled Attempt to request IsFilled for unordered series: " + seriesUid));
                    }
                }
                else
                {
                    Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, "DicomRetriever.Order.IsFilled Attempt to request IsFilled for unordered study: " + studyUid));
                }
            }
            Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, "DicomRetriever.Order.IsFilled Attempt to request IsFilled for unordered patient: " + patId));
            return false;
        }

        public string MakeItemKey(Item item)
        {
            return MakeItemKey(item.PatientId, item.StudyInstanceUID, item.SeriesInstanceUID, item.SOPInstanceUID);
        }

        public string MakeItemKey(string patId, string studyUid, string seriesUid, string sopInstance)
        {
            return patId + "-" + studyUid + "-" + seriesUid + "-" + "-" + sopInstance;

        }


        //parent.x... reflect modified state in parent from picker
        public DicomCMoveRequest GetDicomCMoveRequest(string destination, Item item)
        {
            switch (OrderLevel)
            {
                case OrderLevel.Patient:
                    _patients[item.PatientId].Request();
                    if(parent != null) parent._patients[item.PatientId].Request();
                    return CreateCMoveByPatientId(destination, item.PatientId);
                //break;
                case OrderLevel.Study:
                    _patients[item.PatientId].Studies[item.StudyInstanceUID].Request();
                    if (parent != null) parent._patients[item.PatientId].Studies[item.StudyInstanceUID].Request(); 
                    return CreateCMoveByStudyUid(destination, item.StudyInstanceUID);
                //break;
                case OrderLevel.Series:
                    _patients[item.PatientId].Studies[item.StudyInstanceUID].Series[item.SeriesInstanceUID].Request();
                    if (parent != null) parent._patients[item.PatientId].Studies[item.StudyInstanceUID].Series[item.SeriesInstanceUID].Request();
                    return CreateCMoveBySeriesUid(destination, item.StudyInstanceUID, item.SeriesInstanceUID);
                //break;
                case OrderLevel.Image:
                    _patients[item.PatientId].Studies[item.StudyInstanceUID].Series[item.SeriesInstanceUID].Images[item.SOPInstanceUID].Request();
                    if (parent != null) parent._patients[item.PatientId].Studies[item.StudyInstanceUID].Series[item.SeriesInstanceUID].Images[item.SOPInstanceUID].Request();
                    return CreateCMoveBySopInstanceUid(destination, item.StudyInstanceUID, item.SeriesInstanceUID, item.SOPInstanceUID);
                default:
                    Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, "DicomRetriever.getDicomCMoveRequest Attempt to request item at unsupported level: " + OrderLevel));
                    return null;

            }
        }


        //we don't want to remove from the parent only the pickers list.
        public void Remove(string itemKey)
        {
            var keys = itemKey.Split(Item.separator);
            var patient = _patients[keys[0]];
            var study = patient.Studies[keys[1]];
            var series = study.Series[keys[2]];
            if (series.Images.ContainsKey(keys[3]))
            {
                series.Images.Remove(keys[3]);
            }
            if(!series.Images.Any()) study.Series.Remove(keys[2]);
            if(!study.Series.Any()) patient.Studies.Remove(keys[1]);
            if (!patient.Studies.Any()) _patients.Remove(keys[0]);
        }

        public bool Any()
        {
            return _patients.Any();
        }

        public Item NextItem()
        {
            var patient = _patients.FirstOrDefault().Value;
            var study = patient.Studies.FirstOrDefault().Value;
            var series = study.Series.FirstOrDefault().Value;
            var image = series.Images.FirstOrDefault().Value;
            return new Item(patient.PatientId,study.StudyInstanceUID,series.SeriesInstanceUID,image.SOPInstanceUID);
        }

        public int Total()
        {
            var count = 0;
            foreach (var patient in _patients.Values)
            {
                foreach (var study in patient.Studies.Values)
                {
                    foreach (var series in study.Series.Values)
                    {
                        foreach (var image in series.Images.Values)
                        {
                            count++;
                        }
                    }
                }
            }
            return count;
        }

        public int Filled()
        {
            var count = 0;
            foreach (var patient in _patients.Values)
            {
                foreach (var study in patient.Studies.Values)
                {
                    foreach (var series in study.Series.Values)
                    {
                        foreach (var image in series.Images.Values)
                        {
                            if (image.IsFilled) count++;
                        }
                    }
                }
            }
            return count;
        }

        public int Requested()
        {
            var count = 0;
            foreach (var patient in _patients.Values)
            {
                foreach (var study in patient.Studies.Values)
                {
                    foreach (var series in study.Series.Values)
                    {
                        foreach (var image in series.Images.Values)
                        {
                            if (image.IsRequested) count++;
                        }
                    }
                }
            }
            return count;
        }

        #region CreateCMoveByPatientId
        private DicomCMoveRequest CreateCMoveByPatientId(string destination, string patientId)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region CreateCMoveByStudyUid
        private DicomCMoveRequest CreateCMoveByStudyUid(string destination, string studyUid)
        {
            var request = new DicomCMoveRequest(destination, studyUid);
            Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "DicomRetriever.CreateCMoveByStudyUid created request for: " + studyUid));
            // no more dicomtags have to be set
            return request;
        }
        #endregion

        #region CreateCMoveBySeriesUid
        private DicomCMoveRequest CreateCMoveBySeriesUid(string destination, string studyUid, string seriesUid)
        {
            var request = new DicomCMoveRequest(destination, studyUid, seriesUid);
            Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "DicomRetriever.CreateCMoveBySEriesUid created request for: " + seriesUid));
            // no more dicomtags have to be set
            return request;
        }
        #endregion

        #region CreateCMoveBySopInstanceUid
        private DicomCMoveRequest CreateCMoveBySopInstanceUid(string destination, string studyUid, string seriesUid, string sopInstanceUid)
        {
            var request = new DicomCMoveRequest(destination, studyUid, seriesUid, sopInstanceUid);
            Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "DicomRetriever.CreateCMoveBySopInstanceUid created request for: " + sopInstanceUid));
            // no more dicomtags have to be set
            return request;

        }
        #endregion
    }



}
