using DicomTypeTranslation.TableCreation;
using FAnsi.Discovery;
using Moq;
using NUnit.Framework;
using Rdmp.Core.Curation;
using Rdmp.Core.Curation.Data.Pipelines;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.DataLoad.Engine.DatabaseManagement.EntityNaming;
using Rdmp.Core.DataLoad.Engine.Job;
using Rdmp.Core.DataLoad.Engine.Pipeline.Destinations;
using Rdmp.Core.Logging;
using Rdmp.Dicom.Attachers.Routing;
using Rdmp.Dicom.CommandExecution;
using Rdmp.Dicom.PipelineComponents.DicomSources;
using Rdmp.Dicom.TagPromotionSchema;
using ReusableLibraryCode.Checks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Tests.Common;

namespace Rdmp.Dicom.Tests
{
    class AutoRoutingAttacherTests : DatabaseTests
    {
        [Test]
        public void TestNormalUse()
        {
            int numberOfIterations = 50;

            var db = GetCleanedServer(FAnsi.DatabaseType.MicrosoftSQLServer);
            var attacher = new AutoRoutingAttacher();
            
            
            // Create a nice template with lots of columns
            var template = new ImageTableTemplate
            {
                TableName = "Fish",
                Columns = new[]
                {
                    new ImageColumnTemplate
                    {
                         AllowNulls = true, ColumnName = "RelativeFileArchiveURI"
                    },
                    new ImageColumnTemplate
                    {
                        AllowNulls = true, ColumnName = "SeriesInstanceUID"
                    },
                    new ImageColumnTemplate {IsPrimaryKey = false, AllowNulls = true, ColumnName = "StudyDate"},
                }
            };
            
            // create the table we want to load
            var tbl = db.ExpectTable(template.TableName);
            var cmd = new ExecuteCommandCreateNewImagingDataset(RepositoryLocator,tbl , template);
            Assert.IsFalse(cmd.IsImpossible);
            cmd.Execute();

            var importer = new TableInfoImporter(CatalogueRepository,tbl);
            importer.DoImport(out var tableInfo,out var _);

            var tt = tbl.Database.Server.GetQuerySyntaxHelper().TypeTranslater;

            // add loads more tags
            foreach(string tag in Tags)
            {
                var adder = new TagColumnAdder(tag,TagColumnAdder.GetDataTypeForTag(tag,tt),tableInfo,new ThrowImmediatelyCheckNotifier());
                adder.SkipChecksAndSynchronization = true;
                adder.Execute();
            }

            new TableInfoSynchronizer(tableInfo).Synchronize(new AcceptAllCheckNotifier());

            // create a mock job that treats our database as RAW
            var job = new ThrowImmediatelyDataLoadJob(new HICDatabaseConfiguration(tbl.Database.Server,RdmpMockFactory.Mock_INameDatabasesAndTablesDuringLoads(tbl.Database,tbl.GetRuntimeName())));
            
            // Create folder where dicom files are 
            var dir = new TestLoadDirectory();
            dir.ForLoading = new DirectoryInfo(Path.Combine(TestContext.CurrentContext.WorkDirectory,"LoadMe"));
            dir.Cache = new DirectoryInfo(Path.Combine(TestContext.CurrentContext.WorkDirectory,"Cache"));

            dir.ForLoading.Create();
            dir.Cache.Create();

            //tell the job to look for files here
            job.LoadDirectory = dir;
            job.RegularTablesToLoad = new List<Core.Curation.Data.ITableInfo>{ tableInfo};
            

            // create a dicom file
            var fileOrig = new FileInfo(Path.Combine(TestContext.CurrentContext.TestDirectory,"TestData/FileWithLotsOfTags.dcm"));
            var fileCopy = fileOrig.CopyTo(Path.Combine(dir.ForLoading.FullName,"test.dcm"),true);

            StringBuilder sb = new StringBuilder();
            
            //create a text file that says to load that file lots of times
            for(int i=0;i <numberOfIterations;i++)
                sb.AppendLine(fileCopy.FullName);

            File.WriteAllText(Path.Combine(dir.ForLoading.FullName,"loadme.txt"),sb.ToString());

            attacher.ListPattern = "*.txt";
            attacher.LoadPipeline = CreatePipeline();
            attacher.Initialize(dir,tbl.Database);

            var lm = new LogManager(new DiscoveredServer(UnitTestLoggingConnectionString));
            lm.CreateNewLoggingTaskIfNotExists("test loading");
            var dli = lm.CreateDataLoadInfo("test loading","mee","doing stuff",null,true);
            job.DataLoadInfo = dli;

            attacher.Attach(job,new GracefulCancellationToken());
            attacher.Dispose(job,null);

            Assert.AreEqual(numberOfIterations,tbl.GetRowCount());
        }

        private Pipeline CreatePipeline()
        {
            var pipe = new Pipeline(CatalogueRepository,"TestImagingPipeline");

            var source = new PipelineComponent(CatalogueRepository,pipe,typeof(DicomFileCollectionSource),0);
            pipe.SourcePipelineComponent_ID = source.ID;


            return pipe;
        }

        string[] Tags = new []{
            "InstanceCreationDate",
"InstanceCreationTime",
"InstanceCreatorUID",
"SOPClassUID",
"SOPInstanceUID",
"SeriesDate",
"AcquisitionDate",
"ContentDate",
"StudyTime",
"SeriesTime",
"AcquisitionTime",
"ContentTime",
"AccessionNumber",
"Modality",
"ModalitiesInStudy",
"ConversionType",
"PresentationIntentType",
"Manufacturer",
"InstitutionName",
"InstitutionAddress",
"ReferringPhysicianName",
"StationName",
"StudyDescription",
"ProcedureCodeSequence",
"SeriesDescription",
"InstitutionalDepartmentName",
"PerformingPhysicianName",
"NameOfPhysiciansReadingStudy",
"OperatorsName",
"AdmittingDiagnosesDescription",
"ManufacturerModelName",
"ReferencedPerformedProcedureStepSequence",
"ReferencedImageSequence",
"ReferencedSOPInstanceUID",
"DerivationDescription",
"SourceImageSequence",
"StageName",
"StageNumber",
"NumberOfStages",
"ViewNumber",
"NumberOfViewsInStage",
"AnatomicRegionSequence",
"CodeValue",
"CodingSchemeDesignator",
"CodeMeaning",
"PixelPresentation",
"VolumetricProperties",
"VolumeBasedCalculationTechnique",
"PatientName",
"PatientID",
"PatientBirthDate",
"PatientBirthTime",
"PatientSex",
"PatientBirthName",
"PatientAge",
"PatientSize",
"PatientWeight",
"MedicalAlerts",
"EthnicGroup",
"AdditionalPatientHistory",
"PregnancyStatus",
"PatientComments",
"ClinicalTrialSponsorName",
"ClinicalTrialProtocolID",
"ClinicalTrialProtocolName",
"ClinicalTrialSubjectReadingID",
"ClinicalTrialTimePointID",
"ClinicalTrialTimePointDescription",
"PatientIdentityRemoved" };
    }
}
