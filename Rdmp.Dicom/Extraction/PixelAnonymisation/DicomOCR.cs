using FellowOakDicom;
using FellowOakDicom.Imaging;
using Python.Runtime;
using Rdmp.Core.ReusableLibraryCode.Progress;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Rdmp.Dicom.Extraction.PixelAnonymisation
{
    class DicomOCR
    {
        private string _language;
        private bool _useGPU;
        private IDataLoadEventListener _listener;
        private dynamic reader;

        public string OCREngine { get; set; }
        public string NLPEngine { get; set; }
        public bool OutputRects { get; set; }
        public bool USRegions { get; set; }
        public bool ExceptUSRegions { get; set; }

        private DicomFormChecker _formChecker = new();


        public DicomOCR(string language, bool useGPU, string pythonDLL, IDataLoadEventListener listener)
        {
            _language = language;
            _useGPU = useGPU;
            _listener = listener;
            new DicomSetupBuilder().RegisterServices(s => s.AddFellowOakDicom().AddImageManager<ImageSharpImageManager>()).Build();
            dynamic easyocr = Py.Import("easyocr");
            reader = easyocr.Reader(new List<string>() { _language }, gpu: _useGPU, verbose: false);
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
        public (List<Tuple<int, List<DicomRectangle>>>, List<Tuple<string, string>>) ProcessDicomFile(DicomDataset dicomDataset, string fileName, bool removeForms)
        {
            List<Tuple<int, List<DicomRectangle>>> foundRectangles = [];
            List<Tuple<string, string>> errors = [];
            var pixelData = DicomPixelData.Create(dicomDataset);
            dynamic pydicom = Py.Import("pydicom");
            dynamic np = Py.Import("numpy");
            dynamic ds = pydicom.dcmread(fileName);
            for (var frameIndex = 0; frameIndex < pixelData.NumberOfFrames; frameIndex++)
            {
                List<DicomRectangle> rectangles = [];
                dynamic arr = pydicom.pixels.pixel_array(ds, index: frameIndex);




                PyTuple[] ocrResult = (PyTuple[])reader.readtext(arr);
                List<OCRResult> results = ocrResult.Select(res => new OCRResult(res)).ToList();

                foreach (var result in results)
                {
                    if (result.Confidence > 0.0F && !IgnoreText(result.FoundText))// todo check confidence
                    {
                        var dicomRectangle = new DicomRectangle()
                        {
                            text = result.FoundText,
                            rectangle = new Rect(result.X, result.Y, result.Width, result.Height),
                            confidence = result.Confidence
                        };
                        rectangles.Add(dicomRectangle);
                    }
                    else
                    {
                        errors.Add(new Tuple<string, string>(fileName, $"Did not redact '{result.FoundText}' with confidence {result.Confidence}"));
                    }
                }

                if (removeForms && _formChecker.IsForm(rectangles))
                {
                    var dicomRectangle = new DicomRectangle()
                    {
                        text = "SUSPECTED FORM",
                        rectangle = new Rect(0, 0, (int)arr.shape[1], (int)arr.shape[0]),
                        confidence = 100
                    };
                    rectangles = [dicomRectangle];
                }
                if (rectangles.Count > 0)
                {
                    foundRectangles.Add(new Tuple<int, List<DicomRectangle>>(frameIndex, rectangles));
                }

            }
            return (foundRectangles, errors);
        }
    }
}
