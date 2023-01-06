using System.Collections.Generic;
using NUnit.Framework;
using Rdmp.Dicom.Extraction.FoDicomBased;
using Rdmp.Dicom.Extraction.FoDicomBased.DirectoryDecisions;
using ReusableLibraryCode.Progress;
using System.IO;
using System.Linq;

namespace Rdmp.Dicom.Tests.Integration;

public class FoDicomAnonymiserStandaloneTests
{
    [Test]
    public void TestAnonymiseAFile()
    {
        var anon = new FoDicomAnonymiser();

        var inPath = new DirectoryInfo(Path.Combine(TestContext.CurrentContext.WorkDirectory, "in"));
        var outPath = new DirectoryInfo(Path.Combine(TestContext.CurrentContext.WorkDirectory, "out"));
            
        if (inPath.Exists)
            inPath.Delete(true);
        inPath.Create();

        if (outPath.Exists)
            outPath.Delete(true);
        outPath.Create();

        // put a dicom file in the in dir
        var testFile = new FileInfo(Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData/IM-0001-0013.dcm"));
        testFile.CopyTo(Path.Combine(inPath.FullName, "blah.dcm"),true);

        anon.Initialize(1, outPath,null /*no UID mapping*/);

        var putter = new PutInRoot();

        var blah=new List<(string, string)> { ("blah.dcm", "blah.dcm") };
        anon.ProcessFile(
            new AmbiguousFilePath(inPath.FullName, blah).GetDataset().Single().Item2,
            new ThrowImmediatelyDataLoadEventListener(),
            new ZipPool(),
            "fffff",
            putter, null);
    }
        
}