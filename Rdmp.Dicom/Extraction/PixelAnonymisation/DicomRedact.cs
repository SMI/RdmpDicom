using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Reconstruction;
using FellowOakDicom.Imaging.Render;
using FellowOakDicom.IO.Buffer;
using NPOI.SS.Formula.Functions;
using NPOI.Util;
using Python.Runtime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Rdmp.Dicom.Extraction.PixelAnonymisation;

public class DicomRedact
{

    public DicomRedact() { }

    public void Redact(DicomDataset dicomDataset, string dicomFileLocation, List<Tuple<int, List<DicomRectangle>>> redactions,string outputLocation)
    {
        using (Py.GIL())
        {
            dynamic pydicom = Py.Import("pydicom");
            dynamic np = Py.Import("numpy");
            dynamic builtins = Py.Import("builtins");
            dynamic slice = builtins.slice;

            dynamic ds = pydicom.dcmread(dicomFileLocation);

            var existingPixelData = DicomPixelData.Create(dicomDataset);
            var samples = existingPixelData.SamplesPerPixel;
            var photometric = existingPixelData.PhotometricInterpretation;
            var bitsStored = existingPixelData.BitsStored;
            for (var frameIndex = 0; frameIndex < existingPixelData.NumberOfFrames; frameIndex++)
            {
                var frameRedactions = redactions.Where(r => r.Item1 == frameIndex);

                dynamic pixel_data = ds.pixel_array;

                dynamic bit_mask = np.array(0xffff << bitsStored).astype(np.uint16);

                dynamic bit_mask_arr = np.array(new List<PyObject>() { bit_mask }, dtype: pixel_data.dtype);
                foreach (var redaction in frameRedactions)
                {
                    var rectangles = redaction.Item2;
                    foreach (var rectangle in rectangles)
                    {
                        PyObject ySlice = slice(rectangle.rectangle.Y1, rectangle.rectangle.Y2); // Equivalent to y0:y1
                        PyObject xSlice = slice(rectangle.rectangle.X1, rectangle.rectangle.X2); // Equivalent to x0:x1
                        PyObject fullSlice = slice(null, null);   // Equivalent to ":"
                        if (rectangle.rectangle.X1 < 0 || rectangle.rectangle.Y1 < 0 || rectangle.rectangle.Width < 0 || rectangle.rectangle.Height < 0)
                        {
                            continue;
                        }
                        if (pixel_data.ndim == 2)
                        {
                            //pixel_data[y0:y1, x0:x1] &= bit_mask_arr
                            PyTuple indices = new PyTuple(new PyObject[] { ySlice, xSlice, fullSlice });
                            pixel_data[indices] &= bit_mask_arr;
                        }
                        else if (pixel_data.ndim == 3 && (samples == 3 || photometric == PhotometricInterpretation.Rgb))
                        {
                            //pixel_data[y0:y1, x0:x1, :] &= bit_mask_arr
                            PyTuple indices = new PyTuple(new PyObject[] { ySlice, xSlice, fullSlice });
                            pixel_data[indices] &= bit_mask_arr;
                        }
                        else if (pixel_data.ndim == 3)
                        {
                            // pixel_data[frame, y0:y1, x0:x1] &= bit_mask_arr
                            PyTuple indices = new PyTuple(new PyObject[] { new PyInt(frameIndex), ySlice, xSlice });
                            pixel_data[indices] &= bit_mask_arr;
                        }
                        else if (pixel_data.ndim == 4)
                        {
                            // pixel_data[frame, y0:y1, x0:x1, :] &= bit_mask_arr
                            PyTuple indices = new PyTuple(new PyObject[] { new PyInt(frameIndex), ySlice, xSlice, fullSlice });
                            pixel_data[indices] &= bit_mask_arr;
                        }
                    }
                }
                pydicom.pixels.set_pixel_data(ds, pixel_data, "RGB", bitsStored);
            }

            ds.save_as($"{outputLocation}");
        }
    }
}
