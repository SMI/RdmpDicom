using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Render;
using FellowOakDicom.Serialization;
using NPOI.HPSF;
using NPOI.OpenXmlFormats.Wordprocessing;
using NPOI.SS.Formula.Functions;
using Python.Runtime;
using Rdmp.Core.Icons.IconProvision;
using Rdmp.Core.ReusableLibraryCode.Progress;
using Rdmp.Core.Validation;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
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
using static System.Net.Mime.MediaTypeNames;

namespace Rdmp.Dicom.Extraction.PixelAnonymisation
{
    class DicomOCR
    {
        private string _language;
        private bool _useGPU;
        private IDataLoadEventListener _listener;

        public string OCREngine { get; set; }
        public string NLPEngine { get; set; }
        public bool OutputRects { get; set; }
        public bool USRegions { get; set; }
        public bool ExceptUSRegions { get; set; }

        public DicomOCR(string language, bool useGPU, string pythonDLL,  IDataLoadEventListener listener)
        {
            _language = language;
            _useGPU = useGPU;
            _listener = listener;
            Runtime.PythonDLL = pythonDLL;// "C:\\Users\\jfriel001\\AppData\\Local\\Programs\\Python\\Python313\\Python313.dll";
            PythonEngine.Initialize();
            new DicomSetupBuilder().RegisterServices(s => s.AddFellowOakDicom().AddImageManager<ImageSharpImageManager>()).Build();

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
        public List<Tuple<int, List<DicomRectangle>>> ProcessDicomFile(DicomDataset dicomDataset, string fileName)
        {
            List<Tuple<int, List<DicomRectangle>>> foundRectangles = [];
            var pixelData = DicomPixelData.Create(dicomDataset);

            for (var frameIndex = 0; frameIndex < pixelData.NumberOfFrames; frameIndex++)
            {
                var frame = pixelData.GetFrame(frameIndex);
                List<DicomRectangle> rectangles = [];
                //convert frame to image
                var frameImg = new DicomImage(dicomDataset, frameIndex);
                var path = System.IO.Path.GetTempFileName() + ".jpg";
                try
                {
                    var stream = new MemoryStream(frame.Data);
                    var img = System.Drawing.Image.FromStream(stream);
                    img.Save(path);
                }
                catch (Exception e)
                {
                    _listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning, $"Unable to Process file {fileName} from memory", e));

                    try
                    {
                        using (var renderedImage = frameImg.RenderImage(frameIndex))
                        {
                            var sharpImage = renderedImage.AsSharpImage();
                            sharpImage.SaveAsJpeg(path);
                        }
                    }
                    catch (Exception e2)
                    {
                        //too large and not supported
                        _listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning, $"Unable to Process file {fileName}", e2));
                        return foundRectangles;
                    }
                }
                //get OCR results
                List<OCRResult> results = [];
                using (Py.GIL())
                {
                    dynamic easyocr = Py.Import("easyocr");
                    dynamic reader = easyocr.Reader(new List<string>() { _language }, gpu: _useGPU, verbose: false);
                    PyTuple[] result = (PyTuple[])reader.readtext(path);
                    results = result.Select(res => new OCRResult(res)).ToList();
                }
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
                }
                if (rectangles.Count > 0)
                {
                    foundRectangles.Add(new Tuple<int, List<DicomRectangle>>(frameIndex, rectangles));
                }

            }
            return foundRectangles;
        }
    }
}
