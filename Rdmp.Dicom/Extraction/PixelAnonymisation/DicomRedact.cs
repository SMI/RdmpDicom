using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Reconstruction;
using FellowOakDicom.Imaging.Render;
using FellowOakDicom.IO.Buffer;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
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
        List<byte[]> newFrames = [];
        for (var frameIndex = 0; frameIndex < pixelData.NumberOfFrames; frameIndex++)
        {
            var frameRedactions = redactions.Where(r => r.Item1 == frameIndex);
            var frame = pixelData.GetFrame(frameIndex);
            var pixels = frame.Data;

            var bitMask = 0xffff << pixelData.BitsStored;


            System.Drawing.Image bmp;
            var frameImg = new DicomImage(dicomDataset, frameIndex);
            using (IImage renderedImage = frameImg.RenderImage())
            {
                SixLabors.ImageSharp.Image sharpImg = renderedImage.AsSharpImage();
                var path = Path.GetTempFileName() + ".bmp";
                sharpImg.SaveAsBmp(path);
                bmp = System.Drawing.Image.FromFile(path);
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
                        System.Drawing.Rectangle ImageSize = new(0, 0, rectangle.rectangle.Width, rectangle.rectangle.Height);
                        graph.FillRectangle(Brushes.Red, ImageSize);
                    }
                    g.DrawImage(bmpRect, rectangle.rectangle.X1, rectangle.rectangle.Y1, rectangle.rectangle.Width, rectangle.rectangle.Height);
                    g.Dispose();
                }
            }
            pixels = ImageToByte2(bmp);
            newFrames.Add(pixels);
            File.WriteAllBytes("C:\\temp\\output.jpg", pixels);
        }
        //todo this generates junk dicoms
        // maybe - https://groups.google.com/g/fo-dicom/c/rTTkSEVncEA
        //DicomDataset dataset = new DicomDataset();
        //var dicomfile = new DicomFile(dicomDataset);
        //dataset = dicomfile.Dataset.Clone();

        //dataset.AddOrUpdate(DicomTag.PhotometricInterpretation, PhotometricInterpretation.Rgb.Value);
        //dataset.AddOrUpdate(DicomTag.Rows,pixelData.Height);
        //dataset.AddOrUpdate(DicomTag.Columns, pixelData.Width);
        //dataset.AddOrUpdate(DicomTag.BitsAllocated,pixelData.BitsAllocated);

        //DicomPixelData _pixelData = DicomPixelData.Create(dataset, true);
        //_pixelData.BitsStored = pixelData.BitsStored;
        //_pixelData.SamplesPerPixel = pixelData.SamplesPerPixel;
        //_pixelData.HighBit = pixelData.HighBit;
        //_pixelData.PhotometricInterpretation = PhotometricInterpretation.Rgb;
        //_pixelData.PixelRepresentation = pixelData.PixelRepresentation;
        //_pixelData.PlanarConfiguration = pixelData.PlanarConfiguration;
        //_pixelData.Height = pixelData.Height;
        //_pixelData.Width = pixelData.Width;
        //MemoryByteBuffer buffer = new MemoryByteBuffer(newFrames[0]);
        //_pixelData.AddFrame(buffer);

        //dicomfile = new DicomFile(dataset);
        //dicomfile.Save("C:\\temp\\output.dcm");

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
