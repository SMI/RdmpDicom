using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rdmp.Dicom.Extraction.PixelAnonymisation
{
    public  class DicomFormChecker
    {
        private readonly int _formThreshold = 2;

        //we've found a form if there are more than _formThreshold counts of redactions starting on the same X or Y value
        // i.e. there is a table header or similar
        public bool IsForm(List<DicomRectangle> rectangles)
        {
            //multiple on same x or y
            var xCounts = rectangles.CountBy(rec => rec.rectangle.X1);
            var yCounts = rectangles.CountBy(rec => rec.rectangle.X1);
            foreach (var x in xCounts) {
                if (x.Value >= _formThreshold) return true;
            }

            foreach (var y in yCounts)
            {
                if (y.Value >= _formThreshold) return true;
            }

            return false;
        }
    }
}
