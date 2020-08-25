using ReusableLibraryCode.Progress;
using System.Collections.Generic;

namespace Rdmp.Dicom.Cache.Pipeline.Ordering
{
    class Patient
    {
        public readonly PlacementMode PlacementMode;
        public readonly OrderLevel OrderLevel;
        public readonly IDataLoadEventListener Listener;
        public string PatientId { get; private set; }
        public Dictionary<string, Study> Studies { get; private set; }
        private bool _filled;
        private bool _requested;

        public Patient(string patId, string studyUid, string seriesUid, string sopInstance, PlacementMode placementMode, OrderLevel orderLevel, IDataLoadEventListener listener)
        {
            Studies = new Dictionary<string, Study>();
            PatientId = patId;
            PlacementMode = placementMode;
            OrderLevel = orderLevel;
            Listener = listener;
            Add(studyUid, seriesUid, sopInstance);
        }

        //TODO public for now no package need to make assembly
        public void Add(string studyUid, string seriesUid, string sopInstance)
        {
            if (Studies.ContainsKey(studyUid))
            {
                Studies[studyUid].Add(seriesUid, sopInstance);
            }
            else
            {
                Studies.Add(studyUid, new Study(studyUid, seriesUid, sopInstance, PlacementMode, OrderLevel,Listener));
            }
        }

        public void Fill(string studyUid, string seriesUid, string sopInstance) { 
            if (!Studies.ContainsKey(studyUid))
            {
                if (PlacementMode == PlacementMode.PlaceThenFill)
                    Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning, "DicomRetriever.Order.Order Attempt to fill order prior to placement" + studyUid + "-" + seriesUid + "-" + sopInstance));
                Studies.Add(studyUid, new Study(studyUid, seriesUid, sopInstance, PlacementMode, OrderLevel,Listener));
            }
            Studies[studyUid].Fill(seriesUid, sopInstance);
        }

        public bool IsFilled()
        {
            if (_filled) return _filled;
            var isFilled = true;
            foreach (var study in Studies.Values)
            {
                //this is not as inefficient as it looks once isFilled is false IsFilled is never called
                isFilled = isFilled && study.IsFilled();
            }
            _filled = isFilled;
            return isFilled;
        }

        public void Request()
        {
            foreach(var study in Studies.Values)
            {
                study.Request();
            }
            _requested = true;

        }

        public bool IsRequested()
        {
            return _requested;
        }
    }
}
