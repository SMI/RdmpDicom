using ReusableLibraryCode.Progress;
using System.Collections.Generic;

namespace Rdmp.Dicom.Cache.Pipeline.Ordering
{
    class Series
    {
        public readonly PlacementMode PlacementMode;
        public readonly OrderLevel OrderLevel;
        public readonly IDataLoadEventListener Listener;
        
        private bool _filled;
        private bool _requested;

        public string SeriesInstanceUID { get; private set; }
        public Dictionary<string, Image> Images { get; private set; }

        public Series(string seriesUid, string sopInstance, PlacementMode placementMode, OrderLevel orderLevel, IDataLoadEventListener listener)
        {
            Images = new Dictionary<string, Image>();
            PlacementMode = placementMode;
            OrderLevel = orderLevel;
            Listener = listener;
            SeriesInstanceUID = seriesUid;
            Add(sopInstance);
        }

        //TODO public for now no package need to make assembly
        public void Add(string sopInstance)
        {

            if (Images.ContainsKey(sopInstance))
            {
                Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, "DicomRetriever.Order.Series Attempt to add duplicate _sopInstance:" + sopInstance));
            }
            else
            {
                Images.Add(sopInstance, new Image(sopInstance, PlacementMode, OrderLevel));
            }
        }

        public void Fill(string sopInstance)
        {
            if (!Images.ContainsKey(sopInstance))
            {
                if (PlacementMode == PlacementMode.PlaceThenFill)
                {
                    Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, "DicomRetriever.Order.Order Attempt to fill order prior to placement" + sopInstance));
                    return;
                }
                Images.Add(sopInstance, new Image(sopInstance, PlacementMode, OrderLevel));
            }
            Images[sopInstance].Fill();
        }

        public void Request()
        {
            foreach (var image in Images.Values)
            {
                image.Request();
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
            foreach (var image in Images.Values)
            {
                //this is not as inefficient as it looks once isFilled is false IsFilled is never called
                isFilled = isFilled && image.IsFilled;
            }
            _filled = isFilled;
            return isFilled;
        }

    }
}
