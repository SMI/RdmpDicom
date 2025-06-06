using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui.Trees;

namespace Rdmp.Dicom.Extraction.PixelAnonymisation
{
    public class Rect
    {
        public int X1 { get; set; }
        public int Y1 { get; set; }
        public int X2 { get;  }
        public int Y2 { get;  }
        public int Width { get; set; }
        public int Height { get; set; }
        public Rect(int x, int y, int width,int height)
        {
            X1 = x;
            Y1 = y;
            Width = width;
            Height = height;
            X2 = X1 + Width;
            Y2 = Y1 + Height;
        }
    }
}
