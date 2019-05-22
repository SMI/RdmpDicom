using System;
using FAnsi.Discovery.TypeTranslation;

namespace Rdmp.Dicom.TagPromotionSchema
{
    [Obsolete("Part of the old tag promotion stuff")]
    public class TagLoadedColumnPair
    {
        public static DatabaseTypeRequest LoadedColumnDataType =  new DatabaseTypeRequest(typeof (string), 50);
        
        public enum States
        {
            None,
            Loaded,
            Requested,
            Corrupted
        }
    }
}
