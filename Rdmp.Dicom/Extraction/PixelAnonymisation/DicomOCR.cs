using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Render;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Tesseract;

namespace Rdmp.Dicom.Extraction.PixelAnonymisation
{
    class DicomOCR
    {

        private string _tesseractLocation;
        private string _language;

        public string OCREngine { get; set; }
        public string NLPEngine { get; set; }
        public bool OutputRects { get; set; }
        public bool USRegions { get; set; }
        public bool ExceptUSRegions { get; set; }

        public DicomOCR(string tesseractLocation, string language)
        {
            _tesseractLocation = tesseractLocation;
            _language = language;
        }


        private bool IsScannedForm(IPixelData pixelData)
        {
            //todo
            return false;
        }

        private bool CheckForPII(string text)
        {
            //TODO
            return false;
        }

        private bool IgnoreText(string foundText)
        {
            List<string> standardIgnoreStrings = ["\n", "\r\n", ""];
            var trimmed = foundText.Trim();
            if (standardIgnoreStrings.Contains(trimmed)) return true;
            return false;
        }

        /// <summary>
        /// Returns a list of found text and the bounding rectangle within the frame
        /// </summary>
        /// <param name="dicomDataset"></param>
        /// <returns></returns>
        public List<Tuple<int, List<DicomRectangle>>> ProcessDicomFile(DicomDataset dicomDataset)
        {
            new DicomSetupBuilder().RegisterServices(s => s.AddFellowOakDicom().AddImageManager<ImageSharpImageManager>()).Build();

            List<Tuple<int, List<DicomRectangle>>> foundRectangles = [];
            using (var engine = new TesseractEngine(_tesseractLocation, _language, EngineMode.Default))
            {
                var pixelData = DicomPixelData.Create(dicomDataset);

                for (var frameIndex = 0; frameIndex < pixelData.NumberOfFrames; frameIndex++)
                {
                    var frame = pixelData.GetFrame(frameIndex);
                    bool isSensitive = false;
                    List<DicomRectangle> rectangles = [];
                    Pix img;
                    var frameImg = new DicomImage(dicomDataset, frameIndex);
                    double scale = 1.0;
                    using (IImage renderedImage = frameImg.RenderImage())
                    {
                        SixLabors.ImageSharp.Image sharpImg = renderedImage.AsSharpImage();

                        //this works for some 
                        //sharpImg.Mutate(x => x.Invert());
                        //sharpImg.Mutate(x => x.Brightness(10.00001F));

                        //want to do some scaling as ocr works best when the image is atleast 300 dpi
                        if (sharpImg.Metadata.HorizontalResolution < 300 || sharpImg.Metadata.VerticalResolution < 300)
                        {
                            scale = Math.Max(300 / sharpImg.Metadata.HorizontalResolution, 300 / sharpImg.Metadata.VerticalResolution);
                            sharpImg.Metadata.HorizontalResolution = sharpImg.Metadata.HorizontalResolution * scale;
                            sharpImg.Metadata.VerticalResolution = sharpImg.Metadata.VerticalResolution * scale;
                        }
                        
                        // there is some real issues wit the preprocessing here

                        var path = Path.GetTempFileName() + ".jpg";
                        sharpImg.SaveAsJpeg(path);
                        img = Pix.LoadFromFile(path);
                    }
                    using (var page = engine.Process(img))
                    {
                        using (var iter = page.GetIterator())
                        {
                            iter.Begin();
                            do
                            {
                                if (iter.TryGetBoundingBox(PageIteratorLevel.Block, out Rect rect))
                                {
                                    if (iter.GetConfidence(PageIteratorLevel.Block) >= 40)//should be like 40
                                    {
                                        var curText = iter.GetText(PageIteratorLevel.Block);
                                        if (!IgnoreText(curText))
                                        {
                                            var dicomRectangle = new DicomRectangle()
                                            {
                                                text = curText,
                                                rectangle = new Rect(Convert.ToInt32(rect.X1 / scale), Convert.ToInt32(rect.Y1 / scale), Convert.ToInt32(rect.Width / scale), Convert.ToInt32(rect.Height / scale)),
                                                confidence = iter.GetConfidence(PageIteratorLevel.Block)
                                            };
                                            rectangles.Add(dicomRectangle);
                                        }
                                    }
                                }
                            } while (iter.Next(PageIteratorLevel.Block));
                        }
                    }
                    if (rectangles.Count > 0)
                    {
                        foundRectangles.Add(new Tuple<int, List<DicomRectangle>>(frameIndex, rectangles));
                    }
                }
            }
            return foundRectangles;
        }
    }
}
