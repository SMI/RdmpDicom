using FellowOakDicom;
using Rdmp.Core.MapsDirectlyToDatabaseTable.Versioning;
using NUnit.Framework;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.Spontaneous;
using Rdmp.Core.DataExport.Data;
using Rdmp.Core.DataExport.DataExtraction.Commands;
using Rdmp.Core.QueryBuilding;
using Rdmp.Core.Repositories.Construction;
using Rdmp.Dicom.Extraction.FoDicomBased;
using Rdmp.Dicom.Extraction.FoDicomBased.DirectoryDecisions;
using Rdmp.Core.ReusableLibraryCode.Checks;
using Rdmp.Core.ReusableLibraryCode.Progress;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using FAnsi.Discovery;
using FAnsi.Discovery.QuerySyntax;
using Rdmp.Core.DataExport.DataExtraction;
using Rdmp.Core.DataExport.DataExtraction.UserPicks;
using Rdmp.Core.DataExport.DataRelease.Audit;
using Rdmp.Core.Logging.PastEvents;
using Rdmp.Core.MapsDirectlyToDatabaseTable;
using Rdmp.Core.MapsDirectlyToDatabaseTable.Revertable;
using Rdmp.Core.Providers;
using Rdmp.Core.QueryBuilding.Parameters;
using Rdmp.Core.Repositories;
using Rdmp.Core.ReusableLibraryCode;
using Tests.Common;
using DatabaseType = FAnsi.DatabaseType;
using IContainer = Rdmp.Core.Curation.Data.IContainer;

namespace Rdmp.Dicom.Tests.Integration;

public class FoDicomAnonymiserTests : DatabaseTests
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

    private static void TidyUpImages()
    {
        var imagesDir = new DirectoryInfo(Path.Combine(TestContext.CurrentContext.WorkDirectory, "Images"));
        if (imagesDir.Exists)
            imagesDir.Delete(true);
    }

    // The following commented tests will fail due to underlying system limits on paths
    // there is no reliable method to get maximum path length (apparently?)
    //        [TestCase(typeof(PutInUidStudySeriesFolders))]
    [TestCase(typeof(PutInUidSeriesFolders), true)]
    [TestCase(typeof(PutInUidSeriesFolders), false)]
    [TestCase(typeof(PutInReleaseIdentifierSubfolders), true)]
    [TestCase(typeof(PutInReleaseIdentifierSubfolders), false)]
    [TestCase(typeof(PutInRoot), true)]
    [TestCase(typeof(PutInRoot), true)]
    public void TestAnonymisingDataset(Type putterType, bool keepDates)
    {
        var uidMapDb = GetCleanedServer(DatabaseType.MicrosoftSQLServer, "TESTUIDMapp");

        MasterDatabaseScriptExecutor e = new(uidMapDb);
        var patcher = new SMIDatabasePatcher();
        e.CreateAndPatchDatabase(patcher, new AcceptAllCheckNotifier());

        var eds = new ExternalDatabaseServer(CatalogueRepository, "eds", patcher);
        eds.SetProperties(uidMapDb);

        Dictionary<DicomTag, string> thingThatShouldDisappear = new()
        {
            //Things we would want to disappear
            {DicomTag.PatientName,"Moscow"},
            {DicomTag.PatientBirthDate,"20010101"},
            {DicomTag.StudyDescription,"Frank has lots of problems, he lives at 60 Pancake road"},
            {DicomTag.SeriesDescription,"Coconuts"},
            {DicomTag.AlgorithmName,"Chessnuts"}, // would not normally be dropped by anonymisation
            {DicomTag.StudyDate,"20020101"}
        };

        Dictionary<DicomTag, string> thingsThatShouldRemain = new()
        {
            //Things we would want to remain
            //{DicomTag.SmokingStatus,"YES"},
        };

        var dicom = new DicomDataset
        {
            {DicomTag.SOPInstanceUID, "123.4.4"},
            {DicomTag.SeriesInstanceUID, "123.4.5"},
            {DicomTag.StudyInstanceUID, "123.4.6"},
            {DicomTag.SOPClassUID,"1"}
        };

        foreach (var (key, value) in thingThatShouldDisappear)
            dicom.AddOrUpdate(key, value);

        foreach (var (key, value) in thingsThatShouldRemain)
            dicom.AddOrUpdate(key, value);

        dicom.AddOrUpdate(DicomTag.StudyDate, new DateTime(2002, 01, 01));

        var fi = new FileInfo(Path.Combine(TestContext.CurrentContext.WorkDirectory, "madness.dcm"));

        DicomFile df = new(dicom);
        df.Save(fi.FullName);

        using var dt = new DataTable();
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
        anonymiser.PreInitialize(cmd, ThrowImmediatelyDataLoadEventListener.Quiet);

        anonymiser.PutterType = putterType;
        anonymiser.ArchiveRootIfAny = TestContext.CurrentContext.WorkDirectory;
        anonymiser.RelativeArchiveColumnName = "Filepath";
        anonymiser.UIDMappingServer = eds;
        anonymiser.RetainDates = keepDates;
        anonymiser.DeleteTags = "AlgorithmName";

        using var anoDt = anonymiser.ProcessPipelineData(dt, ThrowImmediatelyDataLoadEventListener.Quiet, new());

        Assert.That(anoDt.Rows, Has.Count.EqualTo(1));

        //Data table should contain new UIDs
        Assert.That(anoDt.Rows[0]["SOPInstanceUID"], Is.Not.EqualTo("123.4.4"));
        Assert.Multiple(() =>
        {
            Assert.That(anoDt.Rows[0]["SOPInstanceUID"].ToString(), Has.Length.EqualTo(56));

            Assert.That(anoDt.Rows[0]["StudyInstanceUID"], Is.Not.EqualTo("123.4.6"));
        });
        Assert.That(anoDt.Rows[0]["StudyInstanceUID"].ToString(), Has.Length.EqualTo(56));

        FileInfo expectedFile = null;
        if (putterType == typeof(PutInRoot))
            expectedFile = new(Path.Combine(TestContext.CurrentContext.WorkDirectory, "Images",
                $"{anoDt.Rows[0]["SOPInstanceUID"]}.dcm"));

        if (putterType == typeof(PutInReleaseIdentifierSubfolders))
            expectedFile = new(Path.Combine(TestContext.CurrentContext.WorkDirectory, "Images", "Hank",
                $"{anoDt.Rows[0]["SOPInstanceUID"]}.dcm"));

        if (putterType == typeof(PutInUidSeriesFolders))
            expectedFile = new(Path.Combine(TestContext.CurrentContext.WorkDirectory, "Images", "Hank", anoDt.Rows[0]["SeriesInstanceUID"].ToString(),
                $"{anoDt.Rows[0]["SOPInstanceUID"]}.dcm"));

        if (putterType == typeof(PutInUidStudySeriesFolders))
            expectedFile = new(Path.Combine(TestContext.CurrentContext.WorkDirectory, "Images", "Hank", anoDt.Rows[0]["StudyInstanceUID"].ToString(), anoDt.Rows[0]["SeriesInstanceUID"].ToString(),
                $"{anoDt.Rows[0]["SOPInstanceUID"]}.dcm"));

        Assert.That(expectedFile?.Exists, Is.EqualTo(true));
        var anoDicom = DicomFile.Open(expectedFile.FullName);

        Assert.Multiple(() =>
        {
            Assert.That(anoDicom.Dataset.GetValue<string>(DicomTag.PatientID, 0), Is.EqualTo("Hank"));

            Assert.That(anoDicom.Dataset.GetValue<string>(DicomTag.SOPInstanceUID, 0), Is.EqualTo(anoDt.Rows[0]["SOPInstanceUID"]));
            Assert.That(anoDicom.Dataset.GetValue<string>(DicomTag.SeriesInstanceUID, 0), Has.Length.EqualTo(56));

            Assert.That(anoDicom.Dataset.GetValue<string>(DicomTag.StudyInstanceUID, 0), Is.EqualTo(anoDt.Rows[0]["StudyInstanceUID"]));
        });


        foreach (var (key, _) in thingThatShouldDisappear)
        {
            //if it chopped out the entire tag
            if (!anoDicom.Dataset.Contains(key))
                continue;

            if (anoDicom.Dataset.GetValueCount(key) == 0)
                continue;

            var value = anoDicom.Dataset.GetSingleValue<string>(key);
            switch (value)
            {
                //allowed values
                case "ANONYMOUS": continue;

                //anonymous date
                case "00010101":
                    Assert.That(keepDates, Is.False);
                    continue;
                case "20020101":
                    Assert.That(keepDates);
                    continue;


                default:
                    Assert.Fail($"Unexpected value for {key}:{value}");
                    break;
            }
        }

        foreach (var (key, value) in thingsThatShouldRemain)
            Assert.That(anoDicom.Dataset.GetValue<string>(key, 0), Is.EqualTo(value));
    }

    [TestCase()]
    public void TestSkipAnonymisationOnStructuredReports()
    {
        var uidMapDb = GetCleanedServer(DatabaseType.MicrosoftSQLServer, "TESTUIDMapp");

        MasterDatabaseScriptExecutor e = new(uidMapDb);
        var patcher = new SMIDatabasePatcher();
        e.CreateAndPatchDatabase(patcher, new AcceptAllCheckNotifier());

        var eds = new ExternalDatabaseServer(CatalogueRepository, "eds", patcher);
        eds.SetProperties(uidMapDb);

        Dictionary<DicomTag, string> thingThatShouldDisappear = new()
        {
            //Things we would want to disappear
            {DicomTag.PatientName,"Moscow"},
            {DicomTag.PatientBirthDate,"20010101"},
        };

        Dictionary<DicomTag, string> thingsThatShouldRemain = new()
        {
            //Things we would want to remain
            {DicomTag.StudyDescription,"Frank has lots of problems, he lives at 60 Pancake road"},
            {DicomTag.SeriesDescription,"Coconuts"},
            {DicomTag.AlgorithmName,"Chessnuts"}, // would not normally be dropped by anonymisation
            {DicomTag.StudyDate,"20020101"},
        };

        var dicom = new DicomDataset
        {
            {DicomTag.SOPInstanceUID, "123.4.4"},
            {DicomTag.SeriesInstanceUID, "123.4.5"},
            {DicomTag.StudyInstanceUID, "123.4.6"},
            {DicomTag.SOPClassUID,"1"},
            {DicomTag.Modality,"SR" } // its a structured report
        };

        foreach (var (key, value) in thingThatShouldDisappear)
            dicom.AddOrUpdate(key, value);

        foreach (var (key, value) in thingsThatShouldRemain)
            dicom.AddOrUpdate(key, value);

        dicom.AddOrUpdate(DicomTag.StudyDate, new DateTime(2002, 01, 01));

        var fi = new FileInfo(Path.Combine(TestContext.CurrentContext.WorkDirectory, "madness.dcm"));

        DicomFile df = new(dicom);
        df.Save(fi.FullName);

        using var dt = new DataTable();
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
        anonymiser.PreInitialize(cmd, ThrowImmediatelyDataLoadEventListener.Quiet);

        anonymiser.PutterType = typeof(PutInRoot);
        anonymiser.ArchiveRootIfAny = TestContext.CurrentContext.WorkDirectory;
        anonymiser.RelativeArchiveColumnName = "Filepath";
        anonymiser.UIDMappingServer = eds;
        anonymiser.RetainDates = false;
        anonymiser.SkipAnonymisationOnStructuredReports = true; // <- the thing we are testing

        using var anoDt = anonymiser.ProcessPipelineData(dt, ThrowImmediatelyDataLoadEventListener.Quiet, new());

        Assert.That(anoDt.Rows, Has.Count.EqualTo(1));

        //Data table should contain new UIDs
        Assert.That(anoDt.Rows[0]["SOPInstanceUID"], Is.Not.EqualTo("123.4.4"));
        Assert.Multiple(() =>
        {
            Assert.That(anoDt.Rows[0]["SOPInstanceUID"].ToString()?.Length, Is.EqualTo(56));

            Assert.That(anoDt.Rows[0]["StudyInstanceUID"], Is.Not.EqualTo("123.4.6"));
        });
        Assert.That(anoDt.Rows[0]["StudyInstanceUID"].ToString()?.Length, Is.EqualTo(56));

        var expectedFile = new FileInfo(Path.Combine(TestContext.CurrentContext.WorkDirectory, "Images",
            $"{anoDt.Rows[0]["SOPInstanceUID"]}.dcm"));

        Assert.That(expectedFile.Exists);
        var anoDicom = DicomFile.Open(expectedFile.FullName);

        Assert.Multiple(() =>
        {
            Assert.That(anoDicom.Dataset.GetValue<string>(DicomTag.PatientID, 0), Is.EqualTo("Hank"));

            Assert.That(anoDicom.Dataset.GetValue<string>(DicomTag.SOPInstanceUID, 0), Is.EqualTo(anoDt.Rows[0]["SOPInstanceUID"]));
            Assert.That(anoDicom.Dataset.GetValue<string>(DicomTag.SeriesInstanceUID, 0), Has.Length.EqualTo(56));

            Assert.That(anoDicom.Dataset.GetValue<string>(DicomTag.StudyInstanceUID, 0), Is.EqualTo(anoDt.Rows[0]["StudyInstanceUID"]));
        });


        foreach (var (key, _) in thingThatShouldDisappear)
        {
            //if it chopped out the entire tag
            if (!anoDicom.Dataset.Contains(key))
                continue;

            if (anoDicom.Dataset.GetValueCount(key) == 0)
                continue;

            var value = anoDicom.Dataset.GetSingleValue<string>(key);
            switch (value)
            {
                //allowed values
                case "ANONYMOUS": continue;

                default:
                    Assert.Fail($"Unexpected value for {key}:{value}");
                    break;
            }
        }

        foreach (var (key, value) in thingsThatShouldRemain)
            Assert.That(anoDicom.Dataset.GetValue<string>(key, 0), Is.EqualTo(value), $"Expected tag {key} to remain");
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
        var putter = (IPutDicomFilesInExtractionDirectories)ObjectConstructor.Construct(putterType);

        var dicomDataset = new DicomDataset
        {
            {DicomTag.SOPInstanceUID, "123.4.4"},
            {DicomTag.SeriesInstanceUID, "123.4.5"},
            {DicomTag.StudyInstanceUID, "123.4.6"},
            {DicomTag.SOPClassUID,"1"},
            {DicomTag.StudyDate,"20020101"}
        };
        //the actual work
        putter.WriteOutDataset(outputDirectory, releaseIdentifier, dicomDataset);

        FileInfo expectedFile = null;
        if (putterType == typeof(PutInRoot))
            expectedFile = new(Path.Combine(outputDirectory.FullName,
                $"{dicomDataset.GetValue<string>(DicomTag.SOPInstanceUID, 0)}.dcm"));

        if (putterType == typeof(PutInReleaseIdentifierSubfolders))
            expectedFile = new(Path.Combine(outputDirectory.FullName, releaseIdentifier,
                $"{dicomDataset.GetValue<string>(DicomTag.SOPInstanceUID, 0)}.dcm"));

        if (putterType == typeof(PutInUidSeriesFolders))
            expectedFile = new(Path.Combine(outputDirectory.FullName, releaseIdentifier,
                dicomDataset.GetValue<string>(DicomTag.SeriesInstanceUID, 0),
                $"{dicomDataset.GetValue<string>(DicomTag.SOPInstanceUID, 0)}.dcm"));

        if (putterType == typeof(PutInUidStudySeriesFolders))
            expectedFile = new(Path.Combine(outputDirectory.FullName, releaseIdentifier,
                dicomDataset.GetValue<string>(DicomTag.StudyInstanceUID, 0),
                dicomDataset.GetValue<string>(DicomTag.SeriesInstanceUID, 0),
                $"{dicomDataset.GetValue<string>(DicomTag.SOPInstanceUID, 0)}.dcm"));


        Assert.That(expectedFile?.Exists, Is.EqualTo(true));

    }


    [Test]
    public void TestUIDTableExists()
    {
        var db = GetCleanedServer(DatabaseType.MicrosoftSQLServer);

        // set it to an empty database
        var eds = new ExternalDatabaseServer(CatalogueRepository, "UID server", null);
        eds.SetProperties(db);

        var anon = new FoDicomAnonymiser
        {
            UIDMappingServer = eds
        };

        var ex = Assert.Throws<Exception>(() => anon.Check(ThrowImmediatelyCheckNotifier.QuietPicky));

        Assert.That(ex?.Message, Is.EqualTo("UIDMappingServer is not set up yet").IgnoreCase);

        anon.Check(new AcceptAllCheckNotifier());

        // no warnings after it has been created
        Assert.DoesNotThrow(() => anon.Check(ThrowImmediatelyCheckNotifier.QuietPicky));

    }

    [TestCase(typeof(PutInReleaseIdentifierSubfolders))]
    [TestCase(typeof(PutInUidSeriesFolders))]
    [TestCase(typeof(PutInRoot))]
    public void TestAnonymisingDataset_MetadataOnlyVsReal(Type putterType)
    {
        var uidMapDb = GetCleanedServer(DatabaseType.MicrosoftSQLServer, "TESTUIDMapp");

        MasterDatabaseScriptExecutor e = new(uidMapDb);
        var patcher = new SMIDatabasePatcher();
        e.CreateAndPatchDatabase(patcher, new AcceptAllCheckNotifier());

        var eds = new ExternalDatabaseServer(CatalogueRepository, "eds", patcher);
        eds.SetProperties(uidMapDb);

        Dictionary<DicomTag, string> thingThatShouldDisappear = new()
        {
            //Things we would want to disappear
            {DicomTag.PatientName,"Moscow"},
            {DicomTag.PatientBirthDate,"20010101"},
            {DicomTag.StudyDescription,"Frank has lots of problems, he lives at 60 Pancake road"},
            {DicomTag.SeriesDescription,"Coconuts"},
            {DicomTag.AlgorithmName,"Chessnuts"}, // would not normally be dropped by anonymisation
            {DicomTag.StudyDate,"20020101"}
        };

        Dictionary<DicomTag, string> thingsThatShouldRemain = new()
        {
            //Things we would want to remain
            //{DicomTag.SmokingStatus,"YES"},
        };

        var dicom = new DicomDataset
        {
            {DicomTag.SOPInstanceUID, "123.4.4"},
            {DicomTag.SeriesInstanceUID, "123.4.5"},
            {DicomTag.StudyInstanceUID, "123.4.6"},
            {DicomTag.SOPClassUID,"1"}
        };

        foreach (var (key, value) in thingThatShouldDisappear)
            dicom.AddOrUpdate(key, value);

        foreach (var (key, value) in thingsThatShouldRemain)
            dicom.AddOrUpdate(key, value);

        dicom.AddOrUpdate(DicomTag.StudyDate, new DateTime(2002, 01, 01));

        var fi = new FileInfo(Path.Combine(TestContext.CurrentContext.WorkDirectory, "madness.dcm"));

        DicomFile df = new(dicom);
        df.Save(fi.FullName);

        DataTable dtFirstTime = null;

        for (var i = 0; i < 2; i++)
        {
            using var dt = new DataTable();
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
            anonymiser.PreInitialize(cmd, ThrowImmediatelyDataLoadEventListener.Quiet);

            anonymiser.PutterType = putterType;
            anonymiser.ArchiveRootIfAny = TestContext.CurrentContext.WorkDirectory;
            anonymiser.RelativeArchiveColumnName = "Filepath";
            anonymiser.UIDMappingServer = eds;
            anonymiser.DeleteTags = "AlgorithmName";

            // the thing we are actually testing
            anonymiser.MetadataOnly = i == 0;

            using var anoDt = anonymiser.ProcessPipelineData(dt, ThrowImmediatelyDataLoadEventListener.Quiet, new());

            Assert.That(anoDt.Rows, Has.Count.EqualTo(1));

            //Data table should contain new UIDs
            Assert.That(anoDt.Rows[0]["SOPInstanceUID"], Is.Not.EqualTo("123.4.4"));
            Assert.Multiple(() =>
            {
                Assert.That(anoDt.Rows[0]["SOPInstanceUID"].ToString()?.Length, Is.EqualTo(56));

                Assert.That(anoDt.Rows[0]["StudyInstanceUID"], Is.Not.EqualTo("123.4.6"));
            });
            Assert.That(anoDt.Rows[0]["StudyInstanceUID"].ToString()?.Length, Is.EqualTo(56));

            // second time
            if (dtFirstTime != null)
            {
                // rows should be the same whether or not we are doing Metadata only extraction
                foreach (DataRow row in dtFirstTime.Rows)
                {
                    AssertContains(dt, row.ItemArray);
                }
            }

            dtFirstTime = dt;
        }
    }

    private static void AssertContains(DataTable dt, params object[] rowValues)
    {
        Assert.That(dt.Rows.Cast<DataRow>().Any(r =>
                rowValues.All(v => r.ItemArray.Contains(v))), "Did not find expected row " + string.Join(",", rowValues)
            + Environment.NewLine + "Rows seen were:" +
            string.Join(Environment.NewLine,
                dt.Rows.Cast<DataRow>().Select(r => string.Join(",", r.ItemArray))));
    }
    private static IExtractDatasetCommand MockExtractionCommand()
    {
        return new DummyExtractDatasetCommand(TestContext.CurrentContext.WorkDirectory, 100);
    }

}

internal class DummySqlQueryBuilder : ISqlQueryBuilder
{
    /// <inheritdoc />
    public string SQL { get; }

    /// <inheritdoc />
    public bool SQLOutOfDate { get; set; }

    /// <inheritdoc />
    public string LimitationSQL { get; }

    /// <inheritdoc />
    public List<QueryTimeColumn> SelectColumns { get; init; }

    /// <inheritdoc />
    public List<ITableInfo> TablesUsedInQuery { get; }

    /// <inheritdoc />
    public IQuerySyntaxHelper QuerySyntaxHelper { get; }

    /// <inheritdoc />
    public List<IFilter> Filters { get; }

    /// <inheritdoc />
    public List<JoinInfo> JoinsUsedInQuery { get; }

    /// <inheritdoc />
    public IContainer RootFilterContainer { get; set; }

    /// <inheritdoc />
    public bool CheckSyntax { get; set; }

    /// <inheritdoc />
    public ITableInfo PrimaryExtractionTable { get; }

    /// <inheritdoc />
    public ParameterManager ParameterManager { get; }

    /// <inheritdoc />
    public void AddColumnRange(IColumn[] columnsToAdd)
    {
    }

    /// <inheritdoc />
    public void AddColumn(IColumn col)
    {
    }

    /// <inheritdoc />
    public void RegenerateSQL()
    {
    }

    /// <inheritdoc />
    public IEnumerable<Lookup> GetDistinctRequiredLookups()
    {
        yield break;
    }

    /// <inheritdoc />
    public List<CustomLine> CustomLines { get; }

    /// <inheritdoc />
    public CustomLine AddCustomLine(string text, QueryComponent positionToInsert) => null;

    /// <inheritdoc />
    public CustomLine TopXCustomLine { get; set; }
}
internal class DummyExtractionConfiguration : IExtractionConfiguration
{
    /// <inheritdoc />
    public void DeleteInDatabase()
    {
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler PropertyChanged;

    /// <inheritdoc />
    public int ID { get; set; }

    /// <inheritdoc />
    public IRepository Repository { get; set; }

    /// <inheritdoc />
    public void SetReadOnly()
    {
    }

    /// <inheritdoc />
    public void SaveToDatabase()
    {
    }

    /// <inheritdoc />
    public void RevertToDatabaseState()
    {
    }

    /// <inheritdoc />
    public RevertableObjectReport HasLocalChanges() => null;

    /// <inheritdoc />
    public bool Exists() => false;

    /// <inheritdoc />
    public string Name { get; set; }

    /// <inheritdoc />
    public IHasDependencies[] GetObjectsThisDependsOn()
    {
        return Array.Empty<IHasDependencies>();
    }

    /// <inheritdoc />
    public IHasDependencies[] GetObjectsDependingOnThis()
    {
        return Array.Empty<IHasDependencies>();
    }

    /// <inheritdoc />
    public bool ShouldBeReadOnly(out string reason)
    {
        reason = null;
        return false;
    }

    /// <inheritdoc />
    public DiscoveredServer GetDistinctLoggingDatabase() => null;

    /// <inheritdoc />
    public DiscoveredServer GetDistinctLoggingDatabase(out IExternalDatabaseServer serverChosen)
    {
        serverChosen = null;
        return null;
    }

    /// <inheritdoc />
    public string GetDistinctLoggingTask() => null;

    /// <inheritdoc />
    public IEnumerable<ArchivalDataLoadInfo> FilterRuns(IEnumerable<ArchivalDataLoadInfo> runs)
    {
        yield break;
    }

    /// <inheritdoc />
    public IDataExportRepository DataExportRepository { get; }

    /// <inheritdoc />
    public DateTime? dtCreated { get; set; }

    /// <inheritdoc />
    public int? Cohort_ID { get; set; }

    /// <inheritdoc />
    public string RequestTicket { get; set; }

    /// <inheritdoc />
    public string ReleaseTicket { get; set; }

    /// <inheritdoc />
    public int Project_ID { get; }

    /// <inheritdoc />
    public IProject Project { get; init; }

    /// <inheritdoc />
    public string Username { get; }

    /// <inheritdoc />
    public string Separator { get; set; }

    /// <inheritdoc />
    public string Description { get; set; }

    /// <inheritdoc />
    public bool IsReleased { get; set; }

    /// <inheritdoc />
    public int? ClonedFrom_ID { get; set; }

    /// <inheritdoc />
    public IExtractableCohort Cohort { get; }

    /// <inheritdoc />
    public int? DefaultPipeline_ID { get; set; }

    /// <inheritdoc />
    public int? CohortIdentificationConfiguration_ID { get; set; }

    /// <inheritdoc />
    public int? CohortRefreshPipeline_ID { get; set; }

    /// <inheritdoc />
    public IExtractableCohort GetExtractableCohort() => null;

    /// <inheritdoc />
    public IProject GetProject() => null;

    /// <inheritdoc />
    public ISqlParameter[] GlobalExtractionFilterParameters { get; }

    /// <inheritdoc />
    public IReleaseLog[] ReleaseLog { get; }

    /// <inheritdoc />
    public IEnumerable<ICumulativeExtractionResults> CumulativeExtractionResults { get; }

    /// <inheritdoc />
    public IEnumerable<ISupplementalExtractionResults> SupplementalExtractionResults { get; }

    /// <inheritdoc />
    public ExtractableColumn[] GetAllExtractableColumnsFor(IExtractableDataSet dataset)
    {
        return Array.Empty<ExtractableColumn>();
    }

    /// <inheritdoc />
    public IContainer GetFilterContainerFor(IExtractableDataSet dataset) => null;

    /// <inheritdoc />
    public IExtractableDataSet[] GetAllExtractableDataSets()
    {
        return Array.Empty<IExtractableDataSet>();
    }

    /// <inheritdoc />
    public ISelectedDataSets[] SelectedDataSets { get; }

    /// <inheritdoc />
    public void RemoveDatasetFromConfiguration(IExtractableDataSet extractableDataSet)
    {
    }

    /// <inheritdoc />
    public void Unfreeze()
    {
    }

    /// <inheritdoc />
    public IMapsDirectlyToDatabaseTable[] GetGlobals()
    {
        return Array.Empty<IMapsDirectlyToDatabaseTable>();
    }

    /// <inheritdoc />
    public bool IsExtractable(out string reason)
    {
        reason = null;
        return false;
    }
}
internal class DummyProject : IProject
{
    /// <inheritdoc />
    public IHasDependencies[] GetObjectsThisDependsOn()
    {
        return Array.Empty<IHasDependencies>();
    }

    /// <inheritdoc />
    public IHasDependencies[] GetObjectsDependingOnThis()
    {
        return Array.Empty<IHasDependencies>();
    }

    /// <inheritdoc />
    public void DeleteInDatabase()
    {
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler PropertyChanged;

    /// <inheritdoc />
    public int ID { get; set; }

    /// <inheritdoc />
    public IRepository Repository { get; set; }

    /// <inheritdoc />
    public void SetReadOnly()
    {
    }

    /// <inheritdoc />
    public void SaveToDatabase()
    {
    }

    /// <inheritdoc />
    public void RevertToDatabaseState()
    {
    }

    /// <inheritdoc />
    public RevertableObjectReport HasLocalChanges() => null;

    /// <inheritdoc />
    public bool Exists() => false;

    /// <inheritdoc />
    public string Name { get; set; }

    /// <inheritdoc />
    public string Folder { get; set; }

    /// <inheritdoc />
    public string MasterTicket { get; set; }

    /// <inheritdoc />
    public string ExtractionDirectory { get; set; }

    /// <inheritdoc />
    public int? ProjectNumber { get; set; }

    /// <inheritdoc />
    public IExtractionConfiguration[] ExtractionConfigurations { get; }

    /// <inheritdoc />
    public IProjectCohortIdentificationConfigurationAssociation[] ProjectCohortIdentificationConfigurationAssociations
    {
        get;
    }

    /// <inheritdoc />
    public IDataExportRepository DataExportRepository { get; }

    /// <inheritdoc />
    public ICatalogue[] GetAllProjectCatalogues()
    {
        return Array.Empty<ICatalogue>();
    }

    /// <inheritdoc />
    public ExtractionInformation[] GetAllProjectCatalogueColumns(ExtractionCategory any)
    {
        return Array.Empty<ExtractionInformation>();
    }

    /// <inheritdoc />
    public ExtractionInformation[] GetAllProjectCatalogueColumns(ICoreChildProvider childProvider, ExtractionCategory any)
    {
        return Array.Empty<ExtractionInformation>();
    }
}
internal class DummyExtractDatasetCommand : IExtractDatasetCommand
{
    private readonly DirectoryInfo _dir;

    public DummyExtractDatasetCommand(string dir, int i)
    {
        _dir = new DirectoryInfo(dir);
        Configuration = new DummyExtractionConfiguration()
        {
            Project = new DummyProject { ProjectNumber = i }
        };
        QueryBuilder = new DummySqlQueryBuilder()
        {
            SelectColumns = new List<QueryTimeColumn>
            {
                new(new SpontaneouslyInventedColumn(new(), "Pat", "[db]..[tb].[Pat]") { IsExtractionIdentifier = true })
            }
        };
    }

    /// <inheritdoc />
    public DirectoryInfo GetExtractionDirectory() => _dir;

    /// <inheritdoc />
    public IExtractionConfiguration Configuration { get; }

    /// <inheritdoc />
    public string DescribeExtractionImplementation() => null;

    /// <inheritdoc />
    public ExtractCommandState State { get; }

    /// <inheritdoc />
    public void ElevateState(ExtractCommandState newState)
    {
    }

    /// <inheritdoc />
    public bool IsBatchResume { get; set; }

    /// <inheritdoc />
    public ISelectedDataSets SelectedDataSets { get; }

    /// <inheritdoc />
    public IExtractableCohort ExtractableCohort { get; set; }

    /// <inheritdoc />
    public ICatalogue Catalogue { get; }

    /// <inheritdoc />
    public IExtractionDirectory Directory { get; set; }

    /// <inheritdoc />
    public IExtractableDatasetBundle DatasetBundle { get; }

    /// <inheritdoc />
    public List<IColumn> ColumnsToExtract { get; set; }

    /// <inheritdoc />
    public IProject Project { get; }

    /// <inheritdoc />
    public void GenerateQueryBuilder()
    {
    }

    /// <inheritdoc />
    public ISqlQueryBuilder QueryBuilder { get; set; }

    /// <inheritdoc />
    public ICumulativeExtractionResults CumulativeExtractionResults { get; }

    /// <inheritdoc />
    public int TopX { get; set; }

    /// <inheritdoc />
    public DateTime? BatchStart { get; set; }

    /// <inheritdoc />
    public DateTime? BatchEnd { get; set; }

    /// <inheritdoc />
    public DiscoveredServer GetDistinctLiveDatabaseServer() => null;
}