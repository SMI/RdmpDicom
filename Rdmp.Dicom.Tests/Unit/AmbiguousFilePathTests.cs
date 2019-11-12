using Dicom;
using NUnit.Framework;
using Rdmp.Dicom.Extraction.FoDicomBased;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using Rdmp.Core.Startup;

namespace Rdmp.Dicom.Tests.Unit
{
    public class AmbiguousFilePathTests
    {
        [Test]
        public void BasicPathsTest()
        {
            bool isLinux = EnvironmentInfo.IsLinux;


            if (isLinux)
            {
                //in linux this looks like a relative path
                var ex = Assert.Throws<ArgumentException>(()=>new AmbiguousFilePath(@"c:\temp\my.dcm"));
                StringAssert.StartsWith("Relative path was encountered without specifying a root",ex.Message);


                ex = Assert.Throws<ArgumentException>(()=>new AmbiguousFilePath(@"c:\temp",@"c:\temp\my.dcm"));
                StringAssert.IsMatch("Specified root path '.*'' was not IsAbsolute",ex.Message);
            }
            else
            {
                var a = new AmbiguousFilePath(@"c:\temp\my.dcm");
                Assert.AreEqual(@"c:\temp\my.dcm", a.FullPath);

                a = new AmbiguousFilePath(@"c:\temp",@"c:\temp\my.dcm");
                Assert.AreEqual(@"c:\temp\my.dcm", a.FullPath);

                a = new AmbiguousFilePath(@"c:\temp", @"c:\temp\myzip.zip!my.dcm");
                Assert.AreEqual(@"c:\temp\myzip.zip!my.dcm", a.FullPath);

                a = new AmbiguousFilePath(@"c:\temp", @"myzip.zip!my.dcm");
                Assert.AreEqual(@"c:\temp\myzip.zip!my.dcm", a.FullPath);
            }
            
            

           //give it some linux style paths
           var b = new AmbiguousFilePath(@"/temp/my.dcm");
           Assert.AreEqual(@"/temp/my.dcm", b.FullPath);

           b = new AmbiguousFilePath(@"/temp",@"/temp/my.dcm");
           Assert.AreEqual(@"/temp/my.dcm", b.FullPath);

           b = new AmbiguousFilePath(@"/temp", @"/temp/myzip.zip!my.dcm");
           Assert.AreEqual(@"/temp/myzip.zip!my.dcm", b.FullPath);

           b = new AmbiguousFilePath(@"/temp/", @"./myzip.zip!my.dcm");
           Assert.AreEqual(@"/temp/./myzip.zip!my.dcm", b.FullPath);
        }
        

        [Test]
        public void GetDatasetFromFileTest()
        {
            FileInfo f = new FileInfo(Path.Combine(TestContext.CurrentContext.WorkDirectory,"test.dcm"));
            
            File.Copy(
                Path.Combine(TestContext.CurrentContext.TestDirectory,"TestData","IM-0001-0013.dcm"),
                f.FullName);

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

            var bytes = File.ReadAllBytes(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "IM-0001-0013.dcm"));

            using (var z = ZipFile.Open(fzip.FullName, ZipArchiveMode.Create))
            {
                var entry = z.CreateEntry("test.dcm");
                using (Stream s = entry.Open())
                    s.Write(bytes, 0, bytes.Length);
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

            var bytes = File.ReadAllBytes(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "IM-0001-0024.dcm"));

            //Create a zip file with lots of entries
            using (var z = ZipFile.Open(fzip.FullName, ZipArchiveMode.Create))
                for (int i = 0; i < 1500; i++)
                {
                    var entry = z.CreateEntry("test" + i + ".dcm");
                    using (Stream s = entry.Open())
                        s.Write(bytes, 0, bytes.Length);
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
