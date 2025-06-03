using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Reconstruction;
using FellowOakDicom.Imaging.Render;
using FellowOakDicom.IO.Buffer;
using NPOI.Util;
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

    public void Redact(DicomDataset dicomDataset, List<Tuple<int, List<DicomRectangle>>> redactions)
    {
        var existingPixelData = DicomPixelData.Create(dicomDataset);
        List<byte[]> newFrames = [];

        for(var frameIndex=0; frameIndex < existingPixelData.NumberOfFrames; frameIndex++)
        {
            var frame = existingPixelData.GetFrame(frameIndex);
            var frameImage = new DicomImage(dicomDataset, frameIndex);
            var frameRedactions = redactions.Where(r => r.Item1 == frameIndex);
            var pixelData = frame.Data;
            
            //do the transform here

            newFrames.Add(pixelData);
        }

        var newPixelData = DicomPixelData.Create(dicomDataset, true);
        foreach(var frame in newFrames)
        {
            newPixelData.AddFrame(new MemoryByteBuffer(frame));
        }
        var dicomFile = new DicomFile(dicomDataset);

        dicomFile.Save("C:\\temp\\output.dcm");
    }
}
