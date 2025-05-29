using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Render;
using FellowOakDicom.Log;
using NLog;
using NPOI.SS.Formula.Functions;
using Rdmp.Core.Icons.IconProvision;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            List<Tuple<int, List<DicomRectangle>>> foundRectangles = [];
            using (var engine = new TesseractEngine(_tesseractLocation, _language, EngineMode.Default))
            {
                var pixelData = DicomPixelData.Create(dicomDataset);
                for (var frameIndex = 0; frameIndex < pixelData.NumberOfFrames; frameIndex++)
                {
                    var frame = pixelData.GetFrame(frameIndex);
                    bool isSensitive = false;
                    List<DicomRectangle> rectangles = [];

                    using (var img = Pix.LoadFromMemory(frame.Data))
                    {
                        using (var page = engine.Process(img))
                        {
                            using (var iter = page.GetIterator())
                            {
                                iter.Begin();
                                do
                                {
                                    if (iter.TryGetBoundingBox(PageIteratorLevel.Block, out Rect rect))
                                    {
                                        if (iter.GetConfidence(PageIteratorLevel.Block) > 40)
                                        {
                                            var curText = iter.GetText(PageIteratorLevel.Block);
                                            if (!IgnoreText(curText))
                                            {
                                                var dicomRectangle = new DicomRectangle()
                                                {
                                                    text = curText,
                                                    rectangle = rect,
                                                    confidence = iter.GetConfidence(PageIteratorLevel.Block)
                                                };
                                                rectangles.Add(dicomRectangle);
                                            }
                                        }
                                    }
                                } while (iter.Next(PageIteratorLevel.Block));
                            }
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
