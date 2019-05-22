using ReusableLibraryCode.Progress;
using System.Collections.Generic;

namespace Rdmp.Dicom.Cache.Pipeline.Ordering
{
    class Study
    {
        public readonly PlacementMode PlacementMode;
        public readonly OrderLevel OrderLevel;
        public readonly IDataLoadEventListener Listener;
        public string StudyInstanceUID { get; private set; }
        private bool _filled;
        private bool _requested;

        public Dictionary<string, Series> Series = new Dictionary<string, Series>();

        public Study(string studyUid, string seriesUid, string sopInstance, PlacementMode placementMode, OrderLevel orderLevel, IDataLoadEventListener listener)
        {
            Series = new Dictionary<string, Series>();
            StudyInstanceUID = studyUid;
            PlacementMode = placementMode;
            OrderLevel = orderLevel;
            Listener = listener;
            Add(seriesUid, sopInstance);
        }

        //TODO public for now no package need to make assembly
        public void Add(string seriesUid, string sopInstance)
        {
            if (Series.ContainsKey(seriesUid))
            {
                Series[seriesUid].Add(sopInstance);
            }
            else
            {
                Series.Add(seriesUid, new Series(seriesUid, sopInstance, PlacementMode, OrderLevel, Listener));
            }
        }

        public void Fill(string seriesUid, string sopInstance)
        {
            if (!Series.ContainsKey(seriesUid))
            {
                if (PlacementMode == PlacementMode.PlaceThenFill)
                {
                    Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, "DicomRetriever.Order.Order Attempt to fill order prior to placement" + seriesUid + "-" + sopInstance));
                    return;
                }
                Series.Add(seriesUid, new Series(seriesUid, sopInstance, PlacementMode, OrderLevel, Listener));
            }
            Series[seriesUid].Fill(sopInstance);
        }

        public void Request()
        {
            foreach (var series in Series.Values)
            {
                series.Request();
            }
            _requested = true;
        }

        public bool IsRequested()
        {
            return _requested;
        }

        public bool IsFilled()
        {
            if (_filled) return _filled;
            var isFilled = true;
            foreach (var series in Series.Values)
            {
                isFilled = isFilled && series.IsFilled();
            }
            _filled = isFilled;
            return isFilled;
        }
    }
}
