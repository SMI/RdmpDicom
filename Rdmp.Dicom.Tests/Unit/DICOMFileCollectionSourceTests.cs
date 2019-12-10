using System;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using BadMedicine;
using BadMedicine.Dicom;
using NUnit.Framework;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Dicom.PipelineComponents.DicomSources;
using Tests.Common;
using Rdmp.Dicom.PipelineComponents.DicomSources.Worklists;
using ReusableLibraryCode.Progress;
using Rdmp.Core.DataFlowPipeline.Requirements;
using Rdmp.Core.DataLoad.Engine.Pipeline.Destinations;
using FAnsi;
using Rdmp.Core.Startup;

namespace Rdmp.Dicom.Tests.Unit
{
    public class DicomFileCollectionSourceTests : DatabaseTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void Test_SingleFile(bool expressRelative)
        {
            var db = GetCleanedServer(DatabaseType.MicrosoftSQLServer);

            var source = new DicomFileCollectionSource();
            source.FilenameField = "RelativeFileArchiveURI";

            if (expressRelative)
                source.ArchiveRoot = TestContext.CurrentContext.TestDirectory;

            var f = new FlatFileToLoad(new FileInfo(Path.Combine(TestContext.CurrentContext.TestDirectory,@"TestData/IM-0001-0013.dcm")));
            
            source.PreInitialize(new FlatFileToLoadDicomFileWorklist(f), new ThrowImmediatelyDataLoadEventListener());

            var tbl = source.GetChunk(new ThrowImmediatelyDataLoadEventListener(), new GracefulCancellationToken());
            var destination = new DataTableUploadDestination();
            
            destination.PreInitialize(db,new ThrowImmediatelyDataLoadEventListener());
            destination.AllowResizingColumnsAtUploadTime = true;
            destination.ProcessPipelineData(tbl, new ThrowImmediatelyDataLoadEventListener(), new GracefulCancellationToken());
            destination.Dispose(new ThrowImmediatelyDataLoadEventListener(), null);

            var finalTable = db.ExpectTable(destination.TargetTableName);

            using (var dt = finalTable.GetDataTable())
            {
                //should be 1 row in the final table
                Assert.AreEqual(1,dt.Rows.Count);
                
                //the path referenced should be the file read in relative/absolute format
                Assert.AreEqual(expressRelative ? "./TestData/IM-0001-0013.dcm":
                    f.File.FullName.Replace('\\','/')
                    ,dt.Rows[0]["RelativeFileArchiveURI"]);
            }

            Assert.IsTrue(finalTable.Exists());
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

            if(dirToLoad.Exists)
                dirToLoad.Delete(true);
            
            dirToLoad.Create();

            //generate some random dicoms
            var r = new Random(999);
            DicomDataGenerator generator = new DicomDataGenerator(r,dirToLoad,"CT");
            generator.MaximumImages = 5;
            var people = new PersonCollection();
            people.GeneratePeople(1,r);
            generator.GenerateTestDataFile(people,new FileInfo("./inventory.csv"),1);

            //zip them up
            FileInfo zip = new FileInfo(Path.Combine(TestContext.CurrentContext.TestDirectory, nameof(Test_ZipFile) + ".zip"));Path.Combine(TestContext.CurrentContext.TestDirectory, nameof(Test_ZipFile) + ".zip");

            if(zip.Exists)
                zip.Delete();

            ZipFile.CreateFromDirectory(dirToLoad.FullName,zip.FullName);

            //tell the source to load the zip
            var f = new FlatFileToLoad(zip);

            var source = new DicomFileCollectionSource();
            source.FilenameField = "RelativeFileArchiveURI";

            if (expressRelative)
                source.ArchiveRoot = TestContext.CurrentContext.TestDirectory;

            source.PreInitialize(new FlatFileToLoadDicomFileWorklist(f), new ThrowImmediatelyDataLoadEventListener());

            var tbl = source.GetChunk(new ThrowImmediatelyDataLoadEventListener(), new GracefulCancellationToken());
            var destination = new DataTableUploadDestination();
            
            destination.PreInitialize(db,new ThrowImmediatelyDataLoadEventListener());
            destination.AllowResizingColumnsAtUploadTime = true;
            destination.ProcessPipelineData(tbl, new ThrowImmediatelyDataLoadEventListener(), new GracefulCancellationToken());
            destination.Dispose(new ThrowImmediatelyDataLoadEventListener(), null);

            var finalTable = db.ExpectTable(destination.TargetTableName);
            
            using (var dt = finalTable.GetDataTable())
            {
                //should be 5 rows in the final table (5 images)
                Assert.AreEqual(5,dt.Rows.Count);

                
                //the path referenced should be the file read in relative/absolute format
                StringAssert.IsMatch(
                    
                    expressRelative ? $@"./{zip.Name}![\d.]*.dcm":
                        $@"{Regex.Escape(zip.FullName.Replace('\\','/'))}![\d.]*.dcm",
                    (string)dt.Rows[0]["RelativeFileArchiveURI"]);
            }

            Assert.IsTrue(finalTable.Exists());
            finalTable.Drop();
        }
    }
}
