using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using BadMedicine;
using BadMedicine.Dicom;
using Dicom;
using DicomTypeTranslation;
using NUnit.Framework;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.DataFlowPipeline.Requirements;
using Rdmp.Core.DataLoad.Engine.Pipeline.Destinations;
using Rdmp.Dicom.PipelineComponents.DicomSources;
using Rdmp.Dicom.PipelineComponents.DicomSources.Worklists;
using ReusableLibraryCode.Progress;
using Tests.Common;
using DatabaseType = FAnsi.DatabaseType;

namespace Rdmp.Dicom.Tests.Unit
{
    class DicomDatasetCollectionSourceTests:DatabaseTests 
    {
        [Test]
        public void Test_GetChunk_PrivateTags()
        {
            var db = GetCleanedServer(DatabaseType.MicrosoftSQLServer);

            var source = new DicomDatasetCollectionSource();
            source.FilenameField = "RelativeFileArchiveURI";
            source.ArchiveRoot = TestContext.CurrentContext.TestDirectory;
            
            var r = new Random(500000);
            List<DicomDataset> datasets = new List<DicomDataset>();
            using (var g = new DicomDataGenerator(r, new DirectoryInfo(TestContext.CurrentContext.WorkDirectory), "CT"))
            {
                var ds = g.GenerateTestDataset(new Person(r), r);
                // By using the .Add(DicomTag, ...) method, private tags get automatically updated so that a private
                // creator group number is generated (if private creator is new) and inserted into the tag element.

                var aTag = new DicomTag(0x3001, 0x08, "PRIVATE");

                ds.Add<int>(aTag, 99);
                ds.Add<double>(new DicomTag(0x3001, 0x12, "PRIVATE"), 3.14);
                ds.Add<string>(new DicomTag(0x3001, 0x08, "ALSOPRIVATE"), "COOL");

                datasets.Add(ds);
            }
                

            source.PreInitialize(new ExplicitListDicomDatasetWorklist(datasets.ToArray(),"bob.dcm"), new ThrowImmediatelyDataLoadEventListener());

            var tbl = source.GetChunk(new ThrowImmediatelyDataLoadEventListener(){ThrowOnWarning = true}, new GracefulCancellationToken());
            tbl.TableName = nameof(Test_GetChunk_PrivateTags);
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

                Assert.Contains("3001_1008:PRIVATE",dt.Columns.Cast<DataColumn>().Select(c=>c.ColumnName).ToArray());
                Assert.AreEqual(99,dt.Rows[0]["3001_1008:PRIVATE"]);
            }

            Assert.IsTrue(finalTable.Exists());
            finalTable.Drop();
        }
    }
}
