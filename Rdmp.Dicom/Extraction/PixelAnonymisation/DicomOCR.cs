using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Render;
using NPOI.OpenXmlFormats.Wordprocessing;
using Python.Runtime;
using Rdmp.Core.Validation;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Normalization;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using Tesseract;
using static System.Net.Mime.MediaTypeNames;

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
            Runtime.PythonDLL = "C:\\Users\\jfriel001\\AppData\\Local\\Programs\\Python\\Python313\\Python313.dll";
            PythonEngine.Initialize();
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

        private class OCRResult
        {
            public float Confidence { get; set; }
            public string FoundText { get; set; }

            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }


            public OCRResult(PyTuple tuple)
            {
                Confidence = tuple[2].As<float>();
                FoundText = tuple[1].As<string>();
                var points = tuple[0].As<PyList>();
                List<int> x_coords = [];
                List<int> y_coords = [];
                foreach (var point in points)
                {
                    var p = point.As<PyList>();
                    Int32.TryParse(p[0].ToString(), out int _x);
                    Int32.TryParse(p[1].ToString(), out int _y);
                    x_coords.Add(_x);
                    y_coords.Add(_y);
                }
                x_coords = x_coords.Distinct().ToList();
                y_coords = y_coords.Distinct().ToList();
                X = x_coords.Min();
                Y = y_coords.Min();
                Width = x_coords.Max() - x_coords.Min();
                Height = y_coords.Max() - y_coords.Min();

            }

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
                    var path = Path.GetTempFileName() + ".jpg";
                    using (IImage renderedImage = frameImg.RenderImage())
                    {
                        SixLabors.ImageSharp.Image sharpImg = renderedImage.AsSharpImage();

                        sharpImg.SaveAsJpeg(path);
                        img = Pix.LoadFromFile(path);
                    }
                    List<OCRResult> results = [];
                    using (Py.GIL())
                    {
                        dynamic np = Py.Import("sys");
                        dynamic easyocr = Py.Import("easyocr");
                        dynamic reader = easyocr.Reader(new List<string>() { "en" }, gpu: false, verbose: false);
                        PyTuple[] result = (PyTuple[])reader.readtext(path);
                        results = result.Select(res => new OCRResult(res)).ToList();
                    }
                    foreach (var result in results)
                    {
                        if (result.Confidence > 0.0F && !IgnoreText(result.FoundText))
                        {
                            var dicomRectangle = new DicomRectangle()
                            {
                                text = result.FoundText,
                                rectangle = new Rect(result.X, result.Y, result.Width, result.Height),
                                confidence = result.Confidence
                            };
                            rectangles.Add(dicomRectangle);
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
