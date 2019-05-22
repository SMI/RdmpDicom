namespace Rdmp.Dicom.Cache.Pipeline.Ordering
{
    class Image
    {
        public readonly PlacementMode PlacementMode;
        public readonly OrderLevel OrderLevel;
        public string SOPInstanceUID { get;private set; }
        
        public bool IsFilled { get; private set; }
        public bool IsRequested { get; private set; }

        public Image(string sopInstance, PlacementMode placementMode = PlacementMode.PlaceThenFill, OrderLevel orderLevel = OrderLevel.Series)
        {
            IsFilled = false;
            IsRequested = false;
            SOPInstanceUID = sopInstance;
            PlacementMode = placementMode;
            OrderLevel = orderLevel;
        }

        public void Fill()
        {
            IsFilled = true;
        }

        public void Request()
        {
            IsRequested = true;
        }
    }
}
