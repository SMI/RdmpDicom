using Dicom;
using MapsDirectlyToDatabaseTable;
using MapsDirectlyToDatabaseTable.Versioning;
using Moq;
using NUnit.Framework;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.Spontaneous;
using Rdmp.Core.DataExport.Data;
using Rdmp.Core.DataExport.DataExtraction.Commands;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.QueryBuilding;
using Rdmp.Core.Repositories.Construction;
using Rdmp.Dicom.Extraction.FoDicomBased;
using Rdmp.Dicom.Extraction.FoDicomBased.DirectoryDecisions;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.Progress;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Tests.Common;
using DatabaseType = FAnsi.DatabaseType;

namespace Rdmp.Dicom.Tests.Integration
{
    public class FoDicomAnonymiserTests:DatabaseTests
    {
        [OneTimeSetUp]
        public void Init()
        {
            TidyUpImages();
        }

        [TearDown]
        public void Dispose()
        {
            TidyUpImages();
        }

        private void TidyUpImages()
        {
            var imagesDir = new DirectoryInfo(Path.Combine(TestContext.CurrentContext.WorkDirectory, "Images"));
            if (imagesDir.Exists)
                imagesDir.Delete(true);
        }

        // The following commented tests will fail due to underlying system limits on paths
        // there is no reliable method to get maximum path length (apparently?)
        //        [TestCase(typeof(PutInUidStudySeriesFolders))]
        [TestCase(typeof(PutInUidSeriesFolders),true)]
        [TestCase(typeof(PutInUidSeriesFolders),false)]
        [TestCase(typeof(PutInReleaseIdentifierSubfolders),true)]
        [TestCase(typeof(PutInReleaseIdentifierSubfolders),false)]
        [TestCase(typeof(PutInRoot),true)]
        [TestCase(typeof(PutInRoot),true)]
        public void TestAnonymisingDataset(Type putterType,bool keepDates)
        {
            var uidMapDb = GetCleanedServer(DatabaseType.MicrosoftSQLServer, "TESTUIDMapp");

            MasterDatabaseScriptExecutor e = new MasterDatabaseScriptExecutor(uidMapDb);
            var patcher = new SMIDatabasePatcher();
            e.CreateAndPatchDatabase(patcher,new AcceptAllCheckNotifier());

            var eds = new ExternalDatabaseServer(CatalogueRepository, "eds", patcher);
            eds.SetProperties(uidMapDb);
            
            Dictionary<DicomTag,string> thingThatShouldDisappear = new Dictionary<DicomTag, string>
            {
                //Things we would want to disappear
                {DicomTag.PatientName,"Moscow"},
                {DicomTag.PatientBirthDate,"20010101"},
                {DicomTag.StudyDescription,"Frank has lots of problems, he lives at 60 Pancake road"},
                {DicomTag.SeriesDescription,"Coconuts"},
                {DicomTag.StudyDate,"20020101"},
            };

            Dictionary<DicomTag,string> thingsThatShouldRemain = new Dictionary<DicomTag, string>
            {
                //Things we would want to remain
                //{DicomTag.SmokingStatus,"YES"},
            };
            
            var dicom = new DicomDataset
            {
                {DicomTag.SOPInstanceUID, "123.4.4"},
                {DicomTag.SeriesInstanceUID, "123.4.5"},
                {DicomTag.StudyInstanceUID, "123.4.6"},
                {DicomTag.SOPClassUID,"1"},
            };

            foreach (var (key, value) in thingThatShouldDisappear)
                dicom.AddOrUpdate(key, value);
            
            foreach (var (key, value) in thingsThatShouldRemain)
                dicom.AddOrUpdate(key, value);

            dicom.AddOrUpdate(DicomTag.StudyDate, new DateTime(2002 , 01 , 01));

            var fi = new FileInfo(Path.Combine(TestContext.CurrentContext.WorkDirectory, "madness.dcm"));

            DicomFile df = new DicomFile(dicom);
            df.Save(fi.FullName);

            var dt = new DataTable();
            dt.Columns.Add("Filepath");
            dt.Columns.Add("SOPInstanceUID");
            dt.Columns.Add("SeriesInstanceUID");
            dt.Columns.Add("StudyInstanceUID");
            dt.Columns.Add("Pat");
            //note we don't have series

            dt.Rows.Add(fi.Name, "123.4.4", "123.4.5", "123.4.6", "Hank");

            var anonymiser = new FoDicomAnonymiser();

            IExtractCommand cmd = MockExtractionCommand();

            //give the mock to anonymiser
            anonymiser.PreInitialize(cmd,new ThrowImmediatelyDataLoadEventListener());

            anonymiser.PutterType = putterType;
            anonymiser.ArchiveRootIfAny = TestContext.CurrentContext.WorkDirectory;
            anonymiser.RelativeArchiveColumnName = "Filepath";
            anonymiser.UIDMappingServer = eds;
            anonymiser.RetainDates = keepDates;
            
            var anoDt = anonymiser.ProcessPipelineData(dt,new ThrowImmediatelyDataLoadEventListener(),new GracefulCancellationToken());

            Assert.AreEqual(1,anoDt.Rows.Count);
            
            //Data table should contain new UIDs
            Assert.AreNotEqual("123.4.4", anoDt.Rows[0]["SOPInstanceUID"]);
            Assert.AreEqual(56, anoDt.Rows[0]["SOPInstanceUID"].ToString().Length);

            Assert.AreNotEqual("123.4.6", anoDt.Rows[0]["StudyInstanceUID"]);
            Assert.AreEqual(56, anoDt.Rows[0]["StudyInstanceUID"].ToString().Length);

            FileInfo expectedFile = null;
            if(putterType == typeof(PutInRoot))
                expectedFile = new FileInfo(Path.Combine(TestContext.CurrentContext.WorkDirectory,"Images", anoDt.Rows[0]["SOPInstanceUID"] + ".dcm"));

            if (putterType == typeof(PutInReleaseIdentifierSubfolders))
                expectedFile = new FileInfo(Path.Combine(TestContext.CurrentContext.WorkDirectory, "Images","Hank", anoDt.Rows[0]["SOPInstanceUID"] + ".dcm"));

            if (putterType == typeof(PutInUidSeriesFolders))
                expectedFile = new FileInfo(Path.Combine(TestContext.CurrentContext.WorkDirectory, "Images", "Hank", anoDt.Rows[0]["SeriesInstanceUID"].ToString(), anoDt.Rows[0]["SOPInstanceUID"] + ".dcm"));

            if (putterType == typeof(PutInUidStudySeriesFolders))
                expectedFile = new FileInfo(Path.Combine(TestContext.CurrentContext.WorkDirectory, "Images", "Hank", anoDt.Rows[0]["StudyInstanceUID"].ToString(), anoDt.Rows[0]["SeriesInstanceUID"].ToString(), anoDt.Rows[0]["SOPInstanceUID"] + ".dcm"));

            Assert.IsTrue(expectedFile.Exists);
            var anoDicom = DicomFile.Open(expectedFile.FullName);
            
            Assert.AreEqual("Hank",anoDicom.Dataset.GetValue<string>(DicomTag.PatientID,0));

            Assert.AreEqual(anoDt.Rows[0]["SOPInstanceUID"], anoDicom.Dataset.GetValue<string>(DicomTag.SOPInstanceUID, 0));
            Assert.AreEqual(56, anoDicom.Dataset.GetValue<string>(DicomTag.SeriesInstanceUID, 0).Length);

            Assert.AreEqual(anoDt.Rows[0]["StudyInstanceUID"], anoDicom.Dataset.GetValue<string>(DicomTag.StudyInstanceUID, 0));


            foreach (var (key, _) in thingThatShouldDisappear)
            {
                //if it chopped out the entire tag
                if(!anoDicom.Dataset.Contains(key))
                    continue;
                
                if (anoDicom.Dataset.GetValueCount(key) == 0)
                    continue;
                
                var value = anoDicom.Dataset.GetSingleValue<string>(key);
                switch (value)
                {
                    //allowed values
                    case "ANONYMOUS":continue;

                    //anonymous date
                    case "00010101":  Assert.IsFalse(keepDates);
                        continue;
                    case "20020101":    Assert.IsTrue(keepDates);
                        continue;


                    default: Assert.Fail("Unexpected value for " + key + ":" + value);
                        break;
                }
            }

            foreach (var (key, value) in thingsThatShouldRemain)
                Assert.AreEqual(value, anoDicom.Dataset.GetValue<string>(key, 0));
        }

        // The following commented tests will fail due to underlying system limits on paths
        // there is no reliable method to get maximum path length (apparently?)
        //        [TestCase(typeof(PutInUidStudySeriesFolders))]
        [TestCase(typeof(PutInReleaseIdentifierSubfolders))]
        [TestCase(typeof(PutInUidSeriesFolders))]
        [TestCase(typeof(PutInRoot))]
        public void TestPutDicomFilesInExtractionDirectories(Type putterType)
        {
            var outputDirectory = new DirectoryInfo(Path.Combine(TestContext.CurrentContext.WorkDirectory, "Images"));
            const string releaseIdentifier = "Hank";
            var putter = (IPutDicomFilesInExtractionDirectories) new ObjectConstructor().Construct(putterType);
            
            var dicomDataset = new DicomDataset
            {
                {DicomTag.SOPInstanceUID, "123.4.4"},
                {DicomTag.SeriesInstanceUID, "123.4.5"},
                {DicomTag.StudyInstanceUID, "123.4.6"},
                {DicomTag.SOPClassUID,"1"},
                {DicomTag.StudyDate,"20020101"},
            };
            //the actual work
            putter.WriteOutDataset(outputDirectory, releaseIdentifier, dicomDataset);

            FileInfo expectedFile = null;
            if (putterType == typeof(PutInRoot))
               expectedFile = new FileInfo(Path.Combine(outputDirectory.FullName,
                   dicomDataset.GetValue<string>(DicomTag.SOPInstanceUID, 0) + ".dcm"));

            if (putterType == typeof(PutInReleaseIdentifierSubfolders))
               expectedFile = new FileInfo(Path.Combine(outputDirectory.FullName, releaseIdentifier, 
                    dicomDataset.GetValue<string>(DicomTag.SOPInstanceUID, 0) + ".dcm"));

            if (putterType == typeof(PutInUidSeriesFolders))
                expectedFile = new FileInfo(Path.Combine(outputDirectory.FullName, releaseIdentifier,
                    dicomDataset.GetValue<string>(DicomTag.SeriesInstanceUID, 0),
                    dicomDataset.GetValue<string>(DicomTag.SOPInstanceUID, 0) + ".dcm"));

            if (putterType == typeof(PutInUidStudySeriesFolders))
                expectedFile = new FileInfo(Path.Combine(outputDirectory.FullName, releaseIdentifier,
                    dicomDataset.GetValue<string>(DicomTag.StudyInstanceUID, 0), 
                    dicomDataset.GetValue<string>(DicomTag.SeriesInstanceUID, 0),
                    dicomDataset.GetValue<string>(DicomTag.SOPInstanceUID, 0) + ".dcm"));
            

            Assert.IsTrue(expectedFile.Exists);

        }

        
        [Test]
        public void TestUIDTableExists()
        {
            var db = GetCleanedServer(DatabaseType.MicrosoftSQLServer);

            // set it to an empty database
            var eds = new ExternalDatabaseServer(CatalogueRepository,"UID server",null);
            eds.SetProperties(db);

            var anon = new FoDicomAnonymiser();
            anon.UIDMappingServer = eds;

            var ex = Assert.Throws<Exception>(()=>anon.Check(new ThrowImmediatelyCheckNotifier() { ThrowOnWarning = true }));

            StringAssert.AreEqualIgnoringCase("UIDMappingServer is not set up yet", ex.Message);

            anon.Check(new AcceptAllCheckNotifier());

            // no warnings after it has been created
            Assert.DoesNotThrow(() => anon.Check(new ThrowImmediatelyCheckNotifier() { ThrowOnWarning = true }));

        }

        private IExtractDatasetCommand MockExtractionCommand()
        {
            //Setup Mocks
            var project = Mock.Of<IProject>(p=>p.ProjectNumber == 100);

            //mock project number
            var config = Mock.Of<IExtractionConfiguration>(c=>c.Project == project);
            
            //mock the prochi/release id columnvar cohort = MockRepository.GenerateMock<IExtractableCohort>();
            var queryBuilder = Mock.Of<ISqlQueryBuilder>(q=>
            q.SelectColumns == new List<QueryTimeColumn> { new QueryTimeColumn(new SpontaneouslyInventedColumn(new MemoryRepository(), "Pat","[db]..[tb].[Pat]"){IsExtractionIdentifier = true}) });
                       
            
            //mock the extraction directory
            var cmd = Mock.Of<IExtractDatasetCommand>(c=>
                c.GetExtractionDirectory() == new DirectoryInfo(TestContext.CurrentContext.WorkDirectory) &&
                c.Configuration == config && 
                c.QueryBuilder == queryBuilder
                );

            return cmd;
        }

    }
}
