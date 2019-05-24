using System.Linq;
using System;
using System.IO;
using ZipFile = System.IO.Compression.ZipFile;
using NUnit.Framework;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.DataLoad.Engine.Job;
using Rdmp.Dicom.PipelineComponents.DicomSources;
using Tests.Common;
using Rdmp.Dicom.PipelineComponents.DicomSources.Worklists;
using ReusableLibraryCode.Progress;
using Rdmp.Core.DataFlowPipeline.Requirements;
using Rdmp.Core.DataLoad.Engine.Pipeline.Destinations;

namespace Rdmp.Dicom.Tests.Unit
{
    public class DICOMFileCollectionSourceTests : DatabaseTests
    {
        [Test]
        public void AssembleDataTableFromFile()
        {
            var source = new DicomFileCollectionSource();
            source.FilenameField = "RelativeFileArchiveURI";

            var f = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData\IM-0001-0013.dcm");

            source.PreInitialize(new FlatFileToLoadDicomFileWorklist(new FlatFileToLoad(new FileInfo(f))), new ThrowImmediatelyDataLoadEventListener());
            var result = source.GetChunk(new ThrowImmediatelyDataLoadEventListener(), new GracefulCancellationToken());

            Assert.AreEqual("IM00010013",result.TableName);
            Assert.Greater(result.Columns.Count,0);
            
            Assert.IsNull(source.GetChunk(new ThrowImmediatelyDataLoadJob(), new GracefulCancellationToken()));
        }
        [Test]
        public void AssembleDataTableFromFileArchive()
        {
            var zip = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData.zip");
            var dir = Path.Combine(TestContext.CurrentContext.TestDirectory,"TestData");
          
            if(File.Exists(zip))
                File.Delete(zip);
            
            ZipFile.CreateFromDirectory(dir,zip);

            var fileCount = Directory.GetFiles(dir,"*.dcm").Count();

            var source = new DicomFileCollectionSource();
            source.FilenameField = "RelativeFileArchiveURI";
            source.PreInitialize(new FlatFileToLoadDicomFileWorklist(new FlatFileToLoad(new FileInfo(zip))), new ThrowImmediatelyDataLoadEventListener());
            var toMemory = new ToMemoryDataLoadEventListener(true);
            var result = source.GetChunk(toMemory, new GracefulCancellationToken());

            //processed every file once
            Assert.AreEqual(fileCount, toMemory.LastProgressRecieivedByTaskName.Single().Value.Progress.Value);

            Assert.Greater(result.Columns.Count, 0);
        }
        [Test]
        public void AssembleDataTableFromFolder()
        {

            var file1 = new FileInfo(Path.Combine(TestContext.CurrentContext.TestDirectory,"TestData/FileWithLotsOfTags.dcm"));
            var file2 = new FileInfo(Path.Combine(TestContext.CurrentContext.TestDirectory,"TestData/IM-0001-0013.dcm"));

            var controlFile = new FileInfo(Path.Combine(TestContext.CurrentContext.TestDirectory,"list.txt"));
            File.WriteAllText(controlFile.FullName,file1.FullName + Environment.NewLine + file2.FullName);

            var source = new DicomFileCollectionSource();
            source.FilenameField = "RelativeFileArchiveURI";
            source.PreInitialize(new FlatFileToLoadDicomFileWorklist(new FlatFileToLoad(controlFile)), new ThrowImmediatelyDataLoadEventListener());
            
            var toMemory = new ToMemoryDataLoadEventListener(true);
            var result = source.GetChunk(toMemory, new GracefulCancellationToken());
            Assert.AreEqual(1,result.Rows.Count);

            result = source.GetChunk(toMemory, new GracefulCancellationToken());
            Assert.AreEqual(1, result.Rows.Count);

            Assert.AreEqual(null, source.GetChunk(toMemory, new GracefulCancellationToken()));
        }

        [Test]
        public void PipelineTest()
        {
            var source = new DicomFileCollectionSource();
            source.FilenameField = "RelativeFileArchiveURI";

            var f = new FlatFileToLoad(new FileInfo(Path.Combine(TestContext.CurrentContext.TestDirectory,@"TestData\IM-0001-0013.dcm")));
            
            source.PreInitialize(new FlatFileToLoadDicomFileWorklist(f), new ThrowImmediatelyDataLoadEventListener());

            var tbl = source.GetChunk(new ThrowImmediatelyDataLoadEventListener(), new GracefulCancellationToken());
            var destination = new DataTableUploadDestination();
            
            destination.PreInitialize(DiscoveredDatabaseICanCreateRandomTablesIn,new ThrowImmediatelyDataLoadEventListener());
            destination.AllowResizingColumnsAtUploadTime = true;
            destination.ProcessPipelineData(tbl, new ThrowImmediatelyDataLoadEventListener(), new GracefulCancellationToken());
            destination.Dispose(new ThrowImmediatelyDataLoadEventListener(), null);

            var finalTable = DiscoveredDatabaseICanCreateRandomTablesIn.ExpectTable(destination.TargetTableName);

            Assert.IsTrue(finalTable.Exists());
            finalTable.Drop();

        }

        [TestCase(@"C:\bob\")]
        [TestCase(@"C:\bob")]
        [TestCase(@"C:\BOb")] //capitalisation
        [TestCase(@"C:/bob/")] //mixed slash directions!
        public void TestRelativeUri_RootedInput(string root)
        {
            var source = new DicomFileCollectionSource();
            source.ArchiveRoot = root;

            var result = source.ApplyArchiveRootToMakeRelativePath(@"C:\bob\fish\1.dcm");

            Assert.AreEqual(@"\fish\1.dcm",result);

            result = source.ApplyArchiveRootToMakeRelativePath(@"C:/bob/fish\1.dcm");

            Assert.AreEqual(@"\fish\1.dcm", result);
        }

        [TestCase(@"C:\bob\")]
        [TestCase(@"C:\bob")]
        public void TestRelativeUri_RelativeInput(string root)
        {
            var source = new DicomFileCollectionSource();
            source.ArchiveRoot = root;

            var result = source.ApplyArchiveRootToMakeRelativePath(@"\fish\1.dcm");

            Assert.AreEqual(@"\fish\1.dcm", result);

            result = source.ApplyArchiveRootToMakeRelativePath(@"fish\1.dcm");

            Assert.AreEqual(@"fish\1.dcm", result);
        }
    }
}
