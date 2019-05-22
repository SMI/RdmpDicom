using Dicom;
using NUnit.Framework;
using Rdmp.Dicom.Extraction.FoDicomBased;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace Rdmp.Dicom.Tests.Unit
{
    public class AmbiguousFilePathTests
    {
        [Test]
        public void BasicPathsTest()
        {
            var a = new AmbiguousFilePath(@"c:\temp\my.dcm");
            Assert.AreEqual(@"c:\temp\my.dcm", a.FullPath);

            a = new AmbiguousFilePath(@"c:\temp",@"c:\temp\my.dcm");
            Assert.AreEqual(@"c:\temp\my.dcm", a.FullPath);

           a = new AmbiguousFilePath(@"c:\temp", @"c:\temp\myzip.zip!my.dcm");
           Assert.AreEqual(@"c:\temp\myzip.zip!my.dcm", a.FullPath);

           a = new AmbiguousFilePath(@"c:\temp", @"\myzip.zip!my.dcm");
           Assert.AreEqual(@"c:\temp\myzip.zip!my.dcm", a.FullPath);
        }
        

        [Test]
        public void GetDatasetFromFileTest()
        {
            FileInfo f = new FileInfo(Path.Combine(TestContext.CurrentContext.WorkDirectory,"test.dcm"));
            
            File.WriteAllBytes(f.FullName,TestDicomFiles.IM_0001_0013);

            var a = new AmbiguousFilePath(f.FullName);
            var ds = a.GetDataset();

            Assert.NotNull(ds.Dataset.GetValue<string>(DicomTag.SOPInstanceUID,0));

            f.Delete();
        }
        [Test]
        public void GetDatasetFromZipFileTest()
        {
            FileInfo fzip = new FileInfo(Path.Combine(TestContext.CurrentContext.WorkDirectory, "omgzip.zip"));

            if (fzip.Exists)
                fzip.Delete();

            using (var z = ZipFile.Open(fzip.FullName, ZipArchiveMode.Create))
            {
                var entry = z.CreateEntry("test.dcm");
                using (Stream s = entry.Open())
                    s.Write(TestDicomFiles.IM_0001_0013, 0, TestDicomFiles.IM_0001_0013.Length);
            }

            Assert.Throws<AmbiguousFilePathResolutionException>(()=>new AmbiguousFilePath(Path.Combine(TestContext.CurrentContext.WorkDirectory, "omgzip.zip")).GetDataset());
            Assert.Throws<AmbiguousFilePathResolutionException>(() => new AmbiguousFilePath(Path.Combine(TestContext.CurrentContext.WorkDirectory, "omgzip.zip!lol")).GetDataset());

            var a = new AmbiguousFilePath(Path.Combine(TestContext.CurrentContext.WorkDirectory, "omgzip.zip!test.dcm"));
            var ds = a.GetDataset();

            Assert.NotNull(ds.Dataset.GetValue<string>(DicomTag.SOPInstanceUID, 0));
            fzip.Delete();
        }

        [Test]
        public void GetDatasetFromZipFile_WithPooling_Test()
        {
            FileInfo fzip = new FileInfo(Path.Combine(TestContext.CurrentContext.WorkDirectory, "omgzip.zip"));

            if (fzip.Exists)
                fzip.Delete();

            //Create a zip file with lots of entries
            using (var z = ZipFile.Open(fzip.FullName, ZipArchiveMode.Create))
                for (int i = 0; i < 1500; i++)
                {
                    var entry = z.CreateEntry("test" + i + ".dcm");
                    using (Stream s = entry.Open())
                        s.Write(TestDicomFiles.IM_0001_0024, 0, TestDicomFiles.IM_0001_0013.Length);
                }

            //we want to read one out of the middle
            var a = new AmbiguousFilePath(Path.Combine(TestContext.CurrentContext.WorkDirectory, "omgzip.zip!test750.dcm"));

            //read the same entry lots of times without pooling
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
                a.GetDataset();

            Console.WriteLine("No Caching:" + sw.ElapsedMilliseconds + "ms");

            //read the same entry lots of times with pooling
            sw = Stopwatch.StartNew();
            using (var pool = new ZipPool())
            {
                for (int i = 0; i < 1000; i++)
                    a.GetDataset(pool);

                Console.WriteLine("With Caching:" + sw.ElapsedMilliseconds + "ms");

                Assert.AreEqual(999,pool.CacheHits);
                Assert.AreEqual(1, pool.CacheMisses);
            }

            

        }
    }
}
