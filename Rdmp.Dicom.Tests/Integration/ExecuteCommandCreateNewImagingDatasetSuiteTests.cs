using FAnsi;
using NUnit.Framework;
using Rdmp.Core.Curation.Data.Pipelines;
using Rdmp.Dicom.CommandExecution;
using Rdmp.Dicom.PipelineComponents.DicomSources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Tests.Common;

namespace Rdmp.Dicom.Tests.Integration
{
    class ExecuteCommandCreateNewImagingDatasetSuiteTests : DatabaseTests
    {

        #region Template

        const string TemplateYaml = @"
#Last Modified: 2020-04-07
Tables:
- TableName: StudyTable
  Columns:
  - ColumnName: PatientID
  - ColumnName: StudyInstanceUID
    IsPrimaryKey: true
  - ColumnName: StudyDate
    AllowNulls: true
  - ColumnName: StudyTime
    AllowNulls: true
  - ColumnName: ModalitiesInStudy
    AllowNulls: true
  - ColumnName: StudyDescription
    AllowNulls: true
  - ColumnName: AccessionNumber
    AllowNulls: true
  - ColumnName: PatientSex
    AllowNulls: true
  - ColumnName: PatientAge
    AllowNulls: true
  - ColumnName: NumberOfStudyRelatedInstances
    AllowNulls: true
  - ColumnName: PatientBirthDate
    AllowNulls: true
- TableName: SeriesTable
  Columns:
  - ColumnName: StudyInstanceUID
  - ColumnName: SeriesInstanceUID
    IsPrimaryKey: true
  - ColumnName: Modality
    AllowNulls: true
  - ColumnName: InstitutionName
    AllowNulls: true
  - ColumnName: ProtocolName
    AllowNulls: true
  - ColumnName: ProcedureCodeSequence_CodeValue
    AllowNulls: true
    Type:
      CSharpType: System.String
      Size:
        IsEmpty: true
      Width: 16
  - ColumnName: PerformedProcedureStepDescription
    AllowNulls: true
  - ColumnName: SeriesDescription
    AllowNulls: true
  - ColumnName: SeriesDate
    AllowNulls: true
  - ColumnName: SeriesTime
    AllowNulls: true
  - ColumnName: BodyPartExamined
    AllowNulls: true
  - ColumnName: DeviceSerialNumber
    AllowNulls: true
  - ColumnName: SeriesNumber
    AllowNulls: true
- TableName: ImageTable
  Columns:
  - ColumnName: SeriesInstanceUID
  - ColumnName: SOPInstanceUID
    IsPrimaryKey: true
  - ColumnName: BurnedInAnnotation
    AllowNulls: true
  - ColumnName: RelativeFileArchiveURI
    AllowNulls: true
  - ColumnName: MessageGuid
    AllowNulls: true
  - ColumnName: SliceLocation
    AllowNulls: true
  - ColumnName: SliceThickness
    AllowNulls: true
  - ColumnName: SpacingBetweenSlices
    AllowNulls: true
  - ColumnName: SpiralPitchFactor
    AllowNulls: true
  - ColumnName: KVP
    AllowNulls: true
  - ColumnName: ExposureTime
    AllowNulls: true
  - ColumnName: Exposure
    AllowNulls: true
  - ColumnName: ImageType
    AllowNulls: true
  - ColumnName: ManufacturerModelName
    AllowNulls: true
  - ColumnName: Manufacturer
    AllowNulls: true
  - ColumnName: SoftwareVersions
    AllowNulls: true
  - ColumnName: XRayTubeCurrent
    AllowNulls: true
  - ColumnName: PhotometricInterpretation
    AllowNulls: true
  - ColumnName: ContrastBolusRoute
    AllowNulls: true
  - ColumnName: ContrastBolusAgent
    AllowNulls: true
  - ColumnName: AcquisitionNumber
    AllowNulls: true
  - ColumnName: AcquisitionDate
    AllowNulls: true
  - ColumnName: AcquisitionTime
    AllowNulls: true
  - ColumnName: ImagePositionPatient
    AllowNulls: true
  - ColumnName: PixelSpacing
    AllowNulls: true
  - ColumnName: FieldOfViewDimensions
    AllowNulls: true
  - ColumnName: FieldOfViewDimensionsInFloat
    AllowNulls: true
  - ColumnName: DerivationDescription
    AllowNulls: true
  - ColumnName: LossyImageCompression
    AllowNulls: true
  - ColumnName: LossyImageCompressionMethod
    AllowNulls: true
  - ColumnName: LossyImageCompressionRatio
    AllowNulls: true
  - ColumnName: LossyImageCompressionRetired
    AllowNulls: true
  - ColumnName: ScanOptions
    AllowNulls: true
";
        #endregion

        [TestCase(DatabaseType.MicrosoftSQLServer)]
        [TestCase(DatabaseType.MySql)]
        public void TestSuiteCreation(DatabaseType dbType)
        {
            var db = GetCleanedServer(dbType);

            var template = Path.Combine(TestContext.CurrentContext.WorkDirectory,"CT.it");
            File.WriteAllText(template,TemplateYaml);

            var cmd = new ExecuteCommandCreateNewImagingDatasetSuite(RepositoryLocator,db,
                new(TestContext.CurrentContext.WorkDirectory),
                typeof(DicomFileCollectionSource),
                "CT_",
                new(template),
                persistentRaw: false,
                createLoad: true);

            Assert.IsFalse(cmd.IsImpossible);

            cmd.Execute();

            Assert.IsNotNull(cmd.NewLoadMetadata);
            
            var pipelineCreated = RepositoryLocator.CatalogueRepository.GetAllObjects<Pipeline>().OrderByDescending(p=>p.ID).First();
            
            Assert.AreEqual(typeof(DicomFileCollectionSource),pipelineCreated.Source.GetClassAsSystemType());
            
            var argFieldMap = pipelineCreated.Source.GetAllArguments().Single(a=>a.Name.Equals(nameof(DicomSource.UseAllTableInfoInLoadAsFieldMap)));
            
            Assert.IsNotNull(argFieldMap);
            Assert.AreEqual(argFieldMap.GetValueAsSystemType(),cmd.NewLoadMetadata);
        }
    }
}
