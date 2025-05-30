using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Reconstruction;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rdmp.Dicom.Extraction.PixelAnonymisation;

public class DicomRedact
{

    public DicomRedact() { }

    public void Redact(DicomDataset dicomDataset, List<Tuple<int, List<DicomRectangle>>> redactions)
    {
        var pixelData = DicomPixelData.Create(dicomDataset);
        for (var frameIndex = 0; frameIndex < pixelData.NumberOfFrames; frameIndex++)
        {
            var frameRedactions = redactions.Where(r => r.Item1 == frameIndex);
            var frame = pixelData.GetFrame(frameIndex);
            var pixels = frame.Data;

            var bitMask = 0xffff << pixelData.BitsStored;


            System.Drawing.Image bmp;
            if (frame.Data.Length > 500000)
            {
                var frameImg = new DicomImage(dicomDataset, frameIndex);
                using (IImage renderedImage = frameImg.RenderImage())
                {
                    SixLabors.ImageSharp.Image sharpImg = renderedImage.AsSharpImage();
                    var path = Path.GetTempFileName() + ".jpeg";
                    sharpImg.SaveAsJpeg(path);
                    bmp = System.Drawing.Image.FromFile(path);
                }
            }
            else
            {
                using (var ms = new MemoryStream(pixels))
                {
                    bmp = new Bitmap(new Bitmap(ms));

                }
            }
            foreach (var redaction in frameRedactions)
            {
                var rects = redaction.Item2;
                foreach (var rectangle in rects)
                {
                    Graphics g = Graphics.FromImage(bmp);
                    var bmpRect = new Bitmap(rectangle.rectangle.Width, rectangle.rectangle.Height, PixelFormat.Format24bppRgb);
                    using (Graphics graph = Graphics.FromImage(bmpRect))
                    {
                        System.Drawing.Rectangle ImageSize = new (0, 0, rectangle.rectangle.Width, rectangle.rectangle.Height);
                        graph.FillRectangle(Brushes.Red, ImageSize);
                    }
                    g.DrawImage(bmpRect, rectangle.rectangle.X1, rectangle.rectangle.Y1, rectangle.rectangle.Width, rectangle.rectangle.Height);
                    g.Dispose();
                }
                //pixels[]
            }
            pixels = ImageToByte2(bmp);
            File.WriteAllBytes("C:\\temp\\output.jpg", pixels);
        }
    }

    public static byte[] ImageToByte2(System.Drawing.Image img)
    {
        using (var stream = new MemoryStream())
        {
            img.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            return stream.ToArray();
        }
    }
}
