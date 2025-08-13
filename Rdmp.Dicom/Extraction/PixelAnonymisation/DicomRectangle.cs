using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rdmp.Dicom.Extraction.PixelAnonymisation
{
    public class DicomRectangle()
    {
        public string text { get; set; }
        public float confidence { get; set; }
        public Rect rectangle { get; set; }
    }
}
