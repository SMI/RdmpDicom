using Dicom;
using NUnit.Framework;
using System;
using System.IO;


namespace Rdmp.Dicom.Tests.Unit
{
    class DicomPerformanceTests
    {
        [Test]
        public void ReadLargeFile()
        {
            var file = new FileInfo(@"H:\DICOMImages\LargeSize\i150.MGDC.2.dcm");
            if (!file.Exists)
            {
                Assert.Inconclusive("File does not exist'");
            }

            var stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();
            DicomFile dicomFile = DicomFile.Open(file.FullName);
            if (dicomFile == null) throw new Exception("File may not be DICOM: " + file);

            var sopInstanceUID = dicomFile.Dataset.GetValue<string>(DicomTag.SOPInstanceUID, 0);

            Console.WriteLine("Time spend" + stopWatch.Elapsed);
        }
    }
}
