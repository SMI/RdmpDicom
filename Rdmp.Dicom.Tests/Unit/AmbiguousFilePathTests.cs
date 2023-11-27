using FellowOakDicom;
using NUnit.Framework;
using Rdmp.Dicom.Extraction.FoDicomBased;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Rdmp.Core.Startup;

namespace Rdmp.Dicom.Tests.Unit;

public class AmbiguousFilePathTests
{
    [Test]
    public void BasicPathsTest()
    {
        if (!EnvironmentInfo.IsLinux) return;

        //in linux this looks like a relative path
        var ex = Assert.Throws<ArgumentException>(() => _ = new AmbiguousFilePath(@"c:\temp\my.dcm"));
        Assert.That(ex?.Message, Does.StartWith("Relative path was encountered without specifying a root"));


        ex = Assert.Throws<ArgumentException>(() => _ = new AmbiguousFilePath(@"c:\temp", @"c:\temp\my.dcm"));
        Assert.That(ex?.Message, Does.Match("Specified root path '.*' was not IsAbsolute"));

    }


    [Test]
    public void GetDatasetFromFileTest()
    {
        FileInfo f = new(Path.Combine(TestContext.CurrentContext.WorkDirectory, "test.dcm"));
        File.Copy(
          Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "IM-0001-0013.dcm"),
          f.FullName, true);

        var a = new AmbiguousFilePath(f.FullName);
        var ds = a.GetDataset().Single().Item2;

        Assert.That(ds.Dataset.GetValue<string>(DicomTag.SOPInstanceUID, 0), Is.Not.Null);

        f.Delete();
    }
    [Test]
    public void GetDatasetFromZipFileTest()
    {
        FileInfo fzip = new(Path.Combine(TestContext.CurrentContext.WorkDirectory, "omgzip.zip"));

        if (fzip.Exists)
            fzip.Delete();

        var bytes = File.ReadAllBytes(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "IM-0001-0013.dcm"));

        using (var z = ZipFile.Open(fzip.FullName, ZipArchiveMode.Create))
        {
            var entry = z.CreateEntry("test.dcm");
            using var s = entry.Open();
            s.Write(bytes, 0, bytes.Length);
        }

        Assert.Throws<AmbiguousFilePathResolutionException>(() => _ = new AmbiguousFilePath(Path.Combine(TestContext.CurrentContext.WorkDirectory, "omgzip.zip!lol")).GetDataset().ToList());

        var a = new AmbiguousFilePath(Path.Combine(TestContext.CurrentContext.WorkDirectory, "omgzip.zip!test.dcm"));
        var ds = a.GetDataset().Single().Item2;

        Assert.That(ds.Dataset.GetValue<string>(DicomTag.SOPInstanceUID, 0), Is.Not.Null);
        fzip.Delete();
    }

    [Test]
    public void GetDatasetFromZipFile_WithPooling_Test()
    {
        FileInfo fzip = new(Path.Combine(TestContext.CurrentContext.WorkDirectory, "omgzip.zip"));

        if (fzip.Exists)
            fzip.Delete();

        var bytes = File.ReadAllBytes(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "IM-0001-0024.dcm"));

        //Create a zip file with lots of entries
        using (var z = ZipFile.Open(fzip.FullName, ZipArchiveMode.Create))
            for (var i = 0; i < 1500; i++)
            {
                var entry = z.CreateEntry($"test{i}.dcm");
                using var s = entry.Open();
                s.Write(bytes, 0, bytes.Length);
            }

        Stopwatch sw = new();
        sw.Start();

        //we want to read one out of the middle
        var a = new AmbiguousFilePath(Path.Combine(TestContext.CurrentContext.WorkDirectory, "omgzip.zip!test750.dcm"));
        a.GetDataset();

        Console.WriteLine($"No Caching:{sw.ElapsedMilliseconds}ms");
    }

    [Test]
    public void TestZipEntry_Exists()
    {
        var zipFile = new FileInfo(Path.Combine(TestContext.CurrentContext.WorkDirectory, "my.zip"));
        var rootDir = Directory.CreateDirectory(Path.Combine(TestContext.CurrentContext.WorkDirectory, nameof(TestZipEntry_Exists)));
        var subDirectory = rootDir.CreateSubdirectory("subdir");
        var sourceFile = new FileInfo(Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData/IM-0001-0013.dcm"));

        sourceFile.CopyTo(Path.Combine(rootDir.FullName, "file1.dcm"), true);
        sourceFile.CopyTo(Path.Combine(subDirectory.FullName, "file2.dcm"), true);

        if (zipFile.Exists)
            zipFile.Delete();
        ZipFile.CreateFromDirectory(rootDir.FullName, zipFile.FullName);
        FileAssert.Exists(zipFile.FullName);

        var exists = new AmbiguousFilePath($"{zipFile.FullName}!file1.dcm");
        Assert.That(exists.GetDataset(), Is.Not.Null);

        var notexists = new AmbiguousFilePath($"{zipFile.FullName}!file2.dcm");
        var ex = Assert.Throws<AmbiguousFilePathResolutionException>(() => notexists.GetDataset().ToList());

        Assert.That(ex.Message, Does.Contain("Could not find path 'file2.dcm' within zip archive"));

        var existsRelative = new AmbiguousFilePath(zipFile.DirectoryName, "my.zip!file1.dcm");
        Assert.That(existsRelative.GetDataset(), Is.Not.Null);

        var existsRelativeWithLeadingSlash = new AmbiguousFilePath(zipFile.DirectoryName, "my.zip!/file1.dcm");
        Assert.That(existsRelativeWithLeadingSlash.GetDataset(), Is.Not.Null);

        var existsRelativeWithLeadingSlashInSubdir = new AmbiguousFilePath(zipFile.DirectoryName, "my.zip!/subdir/file2.dcm");
        Assert.That(existsRelativeWithLeadingSlashInSubdir.GetDataset(), Is.Not.Null);

        var existsRelativeWithLeadingBackSlashInSubdir = new AmbiguousFilePath(zipFile.DirectoryName, "my.zip!\\subdir\\file2.dcm");
        Assert.That(existsRelativeWithLeadingBackSlashInSubdir.GetDataset(), Is.Not.Null);
    }

    [TestCase(@"c:\temp\fff.dcm", true)]
    [TestCase(@"c:\temp\fff", true)]
    [TestCase(@"c:\temp\12.123.213.4214.15.dcm", true)]
    [TestCase(@"c:\temp\12.123.213.4214.15", true)]
    [TestCase(@"c:\temp\ff.zip", false)]
    [TestCase(@"c:\temp\ff.tar", false)]
    public void TestIsDicomReference(string input, bool expected)
    {
        Assert.That(AmbiguousFilePath.IsDicomReference(input), Is.EqualTo(expected));
    }
}