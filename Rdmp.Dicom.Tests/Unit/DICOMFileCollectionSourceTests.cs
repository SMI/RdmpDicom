using System;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using BadMedicine;
using BadMedicine.Dicom;
using NUnit.Framework;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Dicom.PipelineComponents.DicomSources;
using Tests.Common;
using Rdmp.Dicom.PipelineComponents.DicomSources.Worklists;
using Rdmp.Core.ReusableLibraryCode.Progress;
using Rdmp.Core.DataFlowPipeline.Requirements;
using Rdmp.Core.DataLoad.Engine.Pipeline.Destinations;
using FAnsi;
using Rdmp.Dicom.Extraction.FoDicomBased;

namespace Rdmp.Dicom.Tests.Unit;

public class DicomFileCollectionSourceTests : DatabaseTests
{
    [TestCase(true)]
    [TestCase(false)]
    public void Test_SingleFile(bool expressRelative)
    {
        var db = GetCleanedServer(DatabaseType.MicrosoftSQLServer);

        var source = new DicomFileCollectionSource { FilenameField = "RelativeFileArchiveURI" };

        if (expressRelative)
            source.ArchiveRoot = TestContext.CurrentContext.TestDirectory;

        var f = new FlatFileToLoad(new(Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData/IM-0001-0013.dcm")));

        source.PreInitialize(new FlatFileToLoadDicomFileWorklist(f), ThrowImmediatelyDataLoadEventListener.Quiet);

        var tbl = source.GetChunk(ThrowImmediatelyDataLoadEventListener.Quiet, new());
        var destination = new DataTableUploadDestination();

        destination.PreInitialize(db, ThrowImmediatelyDataLoadEventListener.Quiet);
        destination.AllowResizingColumnsAtUploadTime = true;
        destination.ProcessPipelineData(tbl, ThrowImmediatelyDataLoadEventListener.Quiet, new());
        destination.Dispose(ThrowImmediatelyDataLoadEventListener.Quiet, null);

        var finalTable = db.ExpectTable(destination.TargetTableName);

        using (var dt = finalTable.GetDataTable())
        {
            //should be 1 row in the final table
            Assert.That(dt.Rows, Has.Count.EqualTo(1));

            //the path referenced should be the file read in relative/absolute format
            Assert.That(dt.Rows[0]["RelativeFileArchiveURI"], Is.EqualTo(expressRelative ? "./TestData/IM-0001-0013.dcm" :
                    f.File.FullName.Replace('\\', '/')
));
        }

        Assert.That(finalTable.Exists());
        finalTable.Drop();
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Test_ZipFile(bool expressRelative)
    {
        //get a clean database to upload to
        var db = GetCleanedServer(DatabaseType.MicrosoftSQLServer);

        //create a folder in which to generate some dicoms
        var dirToLoad = new DirectoryInfo(Path.Combine(TestContext.CurrentContext.TestDirectory, nameof(Test_ZipFile)));

        if (dirToLoad.Exists)
            dirToLoad.Delete(true);

        dirToLoad.Create();

        //generate some random dicoms
        var r = new Random(999);
        using DicomDataGenerator generator = new(r, dirToLoad.FullName, "CT") { MaximumImages = 5 };
        var people = new PersonCollection();
        people.GeneratePeople(1, r);
        generator.GenerateTestDataFile(people, new("./inventory.csv"), 1);

        //This generates
        // Test_ZipFile
        //      2015
        //          3
        //              18
        //                  751140 2.25.166922918107154891877498685128076062226.dcm
        //                  751140 2.25.179610809676265137473873365625829826423.dcm
        //                  751140 2.25.201969634959506849065133495434871450465.dcm
        //                  751140 2.25.237492679533001779093365416814254319890.dcm
        //                  751140 2.25.316241631782653383510844072713132248731.dcm

        var yearDir = dirToLoad.GetDirectories().Single();
        Assert.That(yearDir.Name, Does.Match("\\d{4}"));

        //zip them up
        FileInfo zip = new(Path.Combine(TestContext.CurrentContext.TestDirectory,
            $"{nameof(Test_ZipFile)}.zip"));

        if (zip.Exists)
            zip.Delete();

        ZipFile.CreateFromDirectory(dirToLoad.FullName, zip.FullName);

        //tell the source to load the zip
        var f = new FlatFileToLoad(zip);

        var source = new DicomFileCollectionSource { FilenameField = "RelativeFileArchiveURI" };

        if (expressRelative)
            source.ArchiveRoot = TestContext.CurrentContext.TestDirectory;

        source.PreInitialize(new FlatFileToLoadDicomFileWorklist(f), ThrowImmediatelyDataLoadEventListener.Quiet);

        var tbl = source.GetChunk(ThrowImmediatelyDataLoadEventListener.Quiet, new());
        var destination = new DataTableUploadDestination();

        destination.PreInitialize(db, ThrowImmediatelyDataLoadEventListener.Quiet);
        destination.AllowResizingColumnsAtUploadTime = true;
        destination.ProcessPipelineData(tbl, ThrowImmediatelyDataLoadEventListener.Quiet, new());
        destination.Dispose(ThrowImmediatelyDataLoadEventListener.Quiet, null);

        var finalTable = db.ExpectTable(destination.TargetTableName);

        using (var dt = finalTable.GetDataTable())
        {
            //should be 5 rows in the final table (5 images)
            Assert.That(dt.Rows, Has.Count.EqualTo(5));

            var pathInDbToDicomFile = (string)dt.Rows[0]["RelativeFileArchiveURI"];

            //We expect either something like:
            // E:/RdmpDicom/Rdmp.Dicom.Tests/bin/Debug/netcoreapp2.2/Test_ZipFile.zip!2015/3/18/2.25.160787663560951826149226183314694084702.dcm
            // ./Test_ZipFile.zip!2015/3/18/2.25.105592977437473375573190160334447272386.dcm

            //the path referenced should be the file read in relative/absolute format
            Assert.That(
                pathInDbToDicomFile, Does.Match(expressRelative ? $@"./{zip.Name}![\d./]*.dcm" :
                    $@"{Regex.Escape(zip.FullName.Replace('\\', '/'))}![\d./]*.dcm"));

            Assert.That(pathInDbToDicomFile, Does.Contain(yearDir.Name), "Expected zip file to have subdirectories and for them to be loaded correctly");

            //confirm we can read that out again
            var path = new AmbiguousFilePath(TestContext.CurrentContext.TestDirectory, pathInDbToDicomFile);
            Assert.That(path.GetDataset(0, 0), Is.Not.Null);
        }

        Assert.That(finalTable.Exists());
        finalTable.Drop();
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Test_ZipFileNotation(bool expressRelative)
    {
        //get a clean database to upload to
        var db = GetCleanedServer(DatabaseType.MicrosoftSQLServer);

        //create a folder in which to generate some dicoms
        var dirToLoad = new DirectoryInfo(Path.Combine(TestContext.CurrentContext.TestDirectory, nameof(Test_ZipFileNotation)));

        if (dirToLoad.Exists)
            dirToLoad.Delete(true);

        dirToLoad.Create();

        //generate some random dicoms
        var r = new Random(999);
        using DicomDataGenerator generator = new(r, dirToLoad.FullName, "CT") { MaximumImages = 5 };
        var people = new PersonCollection();
        people.GeneratePeople(1, r);
        generator.GenerateTestDataFile(people, new("./inventory.csv"), 1);

        //This generates
        // Test_ZipFile
        //      2015
        //          3
        //              18
        //                  751140 2.25.166922918107154891877498685128076062226.dcm
        //                  751140 2.25.179610809676265137473873365625829826423.dcm
        //                  751140 2.25.201969634959506849065133495434871450465.dcm
        //                  751140 2.25.237492679533001779093365416814254319890.dcm
        //                  751140 2.25.316241631782653383510844072713132248731.dcm

        var yearDir = dirToLoad.GetDirectories().Single();
        Assert.That(yearDir.Name, Does.Match("\\d{4}"));

        //should be 5 images in the zip file
        var dicomFiles = yearDir.GetFiles("*.dcm", SearchOption.AllDirectories);
        Assert.That(dicomFiles, Has.Length.EqualTo(5));

        //e.g. \2015\3\18\2.25.223398837779449245317520567111874824918.dcm
        //e.g. \2015\3\18\2.25.179610809676265137473873365625829826423.dcm
        var relativePathWithinZip1 = dicomFiles[0].FullName[dirToLoad.FullName.Length..];
        var relativePathWithinZip2 = dicomFiles[1].FullName[dirToLoad.FullName.Length..];

        //zip them up
        FileInfo zip = new(Path.Combine(TestContext.CurrentContext.TestDirectory,
            $"{nameof(Test_ZipFile)}.zip")); Path.Combine(TestContext.CurrentContext.TestDirectory,
            $"{nameof(Test_ZipFile)}.zip");

        if (zip.Exists)
            zip.Delete();

        ZipFile.CreateFromDirectory(dirToLoad.FullName, zip.FullName);

        //e.g. E:\RdmpDicom\Rdmp.Dicom.Tests\bin\Debug\netcoreapp2.2\Test_ZipFile.zip!\2015\3\18\2.25.223398837779449245317520567111874824918.dcm
        var pathToLoad1 = $"{zip.FullName}!{relativePathWithinZip1}";
        var pathToLoad2 = $"{zip.FullName}!{relativePathWithinZip2}";

        var loadMeTextFile = new FileInfo(Path.Combine(dirToLoad.FullName, "LoadMe.txt"));

        //tell the source to load the zip
        File.WriteAllText(loadMeTextFile.FullName, string.Join(Environment.NewLine, pathToLoad1, pathToLoad2));

        var f = new FlatFileToLoad(loadMeTextFile);

        //Setup source
        var source = new DicomFileCollectionSource { FilenameField = "RelativeFileArchiveURI" };

        if (expressRelative)
            source.ArchiveRoot = TestContext.CurrentContext.TestDirectory;

        var worklist = new FlatFileToLoadDicomFileWorklist(f);

        //Setup destination
        var destination = new DataTableUploadDestination { AllowResizingColumnsAtUploadTime = true };

        //setup pipeline
        var contextFactory = new DataFlowPipelineContextFactory<DataTable>();
        var context = contextFactory.Create(PipelineUsage.FixedDestination | PipelineUsage.FixedDestination);

        //run pipeline
        var pipe = new DataFlowPipelineEngine<DataTable>(context, source, destination, ThrowImmediatelyDataLoadEventListener.Quiet);
        pipe.Initialize(db, worklist);
        pipe.ExecutePipeline(new());

        var finalTable = db.ExpectTable(destination.TargetTableName);

        using (var dt = finalTable.GetDataTable())
        {
            //should be 2 rows (since we told it to only load 2 files out of the zip)
            Assert.That(dt.Rows, Has.Count.EqualTo(2));

            var pathInDbToDicomFile = (string)dt.Rows[0]["RelativeFileArchiveURI"];

            //We expect either something like:
            // E:/RdmpDicom/Rdmp.Dicom.Tests/bin/Debug/netcoreapp2.2/Test_ZipFile.zip!2015/3/18/2.25.160787663560951826149226183314694084702.dcm
            // ./Test_ZipFile.zip!2015/3/18/2.25.105592977437473375573190160334447272386.dcm

            //the path referenced should be the file read in relative/absolute format
            Assert.That(
                pathInDbToDicomFile, Does.Match(expressRelative ? $@"./{zip.Name}![\d./]*.dcm" :
                    $@"{Regex.Escape(zip.FullName.Replace('\\', '/'))}![\d./]*.dcm"));

            Assert.That(pathInDbToDicomFile, Does.Contain(yearDir.Name), "Expected zip file to have subdirectories and for them to be loaded correctly");

            //confirm we can read that out again
            var path = new AmbiguousFilePath(TestContext.CurrentContext.TestDirectory, pathInDbToDicomFile);
            Assert.That(path.GetDataset(0, 0), Is.Not.Null);
        }

        Assert.That(finalTable.Exists());
        finalTable.Drop();
    }
}