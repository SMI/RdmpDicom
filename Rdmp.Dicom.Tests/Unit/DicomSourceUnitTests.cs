using System;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using DicomTypeTranslation.Elevation.Exceptions;
using FellowOakDicom;
using NUnit.Framework;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.DataLoad.Engine.Job;
using Rdmp.Dicom.PipelineComponents.DicomSources;
using Rdmp.Dicom.PipelineComponents.DicomSources.Worklists;
using Rdmp.Core.ReusableLibraryCode.Progress;

namespace Rdmp.Dicom.Tests.Unit;

public sealed class DicomSourceUnitTests
{
    [Test]
    public void Test_Linux_Root()
    {
        var source = new DicomFileCollectionSource { ArchiveRoot = "/" };

        Assert.That(source.ArchiveRoot, Is.EqualTo("/"));
    }

    [TestCase(@"C:\bob\", @"\fish\1.dcm", @"/fish/1.dcm")]
    [TestCase(@"C:\bob", @"/fish/1.dcm", @"/fish/1.dcm")]
    [TestCase(@"C:\bob", @"./fish/1.dcm", @"./fish/1.dcm")]
    [TestCase(@"C:\bob", @"C:\bob\fish\1.dcm", @"./fish/1.dcm")]
    [TestCase(@"C:\bob\", "C:/bob/fish/1.dcm", "./fish/1.dcm")]
    [TestCase(@"C:\bob", "C:/bob/fish/1.dcm", "./fish/1.dcm")]
    [TestCase(@"C:\BOb", "C:/bob/fish/1.dcm", "./fish/1.dcm")] //capitalisation
    [TestCase(@"C:/bob/", @"C:\bob\fish/1.dcm", "./fish/1.dcm")] //mixed slash directions!
    [TestCase("/bob/", "/bob/fish/1.dcm", "./fish/1.dcm")] //linux style paths
    [TestCase("/bob/", @"\bob\fish\1.dcm", "./fish/1.dcm")]
    [TestCase(@"\\myserver\bob", @"\\myserver\bob\fish\1.dcm", "./fish/1.dcm")] // UNC server paths
    [TestCase(@"\\myserver\bob", @"\\myOtherServer\bob\fish\1.dcm", @"\\myOtherServer/bob/fish/1.dcm")]
    [TestCase("/", "/bob/fish/1.dcm", "./bob/fish/1.dcm")]
    [TestCase(@"C:\bob\", @"D:\fish\1.dcm", @"D:/fish/1.dcm")] //not relative so just return verbatim string (with slash fixes)
    [TestCase(@"C:\bob\", @"D:/fish/1.dcm", @"D:/fish/1.dcm")]
    [TestCase(@"C:\bob\", @"C:\fish\bob\fish\1.dcm", @"C:/fish/bob/fish/1.dcm")] //not relative so just return verbatim string (with slash fixes)
    [TestCase(@"C:\bob\", @"./fish.dcm", @"./fish.dcm")]
    [TestCase(@"./bob/", @"./bob/fish.dcm", @"./fish.dcm")] //if the "root" is relative then we can still express this relative to it
    public void Test_ApplyArchiveRootToMakeRelativePath(string root, string inputPath, string expectedRelativePath)
    {
        var source = new DicomFileCollectionSource { ArchiveRoot = root };

        var result = source.ApplyArchiveRootToMakeRelativePath(inputPath);
        Assert.That(result, Is.EqualTo(expectedRelativePath));
    }

    [Test]
    public void AssembleDataTableFromFile()
    {
        var source = new DicomFileCollectionSource { FilenameField = "RelativeFileArchiveURI" };

        var f = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData/IM-0001-0013.dcm");

        source.PreInitialize(new FlatFileToLoadDicomFileWorklist(new(new(f))), ThrowImmediatelyDataLoadEventListener.Quiet);
        var result = source.GetChunk(ThrowImmediatelyDataLoadEventListener.Quiet, new());

        Assert.Multiple(() =>
        {
            Assert.That(result.TableName, Is.EqualTo("IM00010013"));
            Assert.That(result.Columns, Is.Not.Empty);

            Assert.That(source.GetChunk(new ThrowImmediatelyDataLoadJob(), new()), Is.Null);
        });
    }

    [Test]
    public void AssembleDataTableFromFileArchive()
    {
        var zip = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData.zip");
        var dir = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData");

        if (File.Exists(zip))
            File.Delete(zip);

        ZipFile.CreateFromDirectory(dir, zip);

        var fileCount = Directory.GetFiles(dir, "*.dcm").Length;

        var source = new DicomFileCollectionSource { FilenameField = "RelativeFileArchiveURI" };
        source.PreInitialize(new FlatFileToLoadDicomFileWorklist(new(new(zip))), ThrowImmediatelyDataLoadEventListener.Quiet);
        var toMemory = new ToMemoryDataLoadEventListener(true);
        var result = source.GetChunk(toMemory, new());

        Assert.Multiple(() =>
        {
            //processed every file once
            Assert.That(toMemory.LastProgressRecieivedByTaskName.Single().Value.Progress.Value, Is.EqualTo(fileCount));

            Assert.That(result.Columns, Is.Not.Empty);
        });
    }

    [Test]
    public void AssembleDataTableFromFolder()
    {
        var file1 = new FileInfo(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData/FileWithLotsOfTags.dcm"));
        var file2 = new FileInfo(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData/IM-0001-0013.dcm"));

        var controlFile = new FileInfo(Path.Combine(TestContext.CurrentContext.TestDirectory, "list.txt"));
        File.WriteAllText(controlFile.FullName, file1.FullName + Environment.NewLine + file2.FullName);

        var source = new DicomFileCollectionSource { FilenameField = "RelativeFileArchiveURI" };
        source.PreInitialize(new FlatFileToLoadDicomFileWorklist(new(controlFile)), ThrowImmediatelyDataLoadEventListener.Quiet);

        var toMemory = new ToMemoryDataLoadEventListener(true);
        var result = source.GetChunk(toMemory, new());
        Assert.That(result.Rows, Has.Count.EqualTo(1));

        result = source.GetChunk(toMemory, new());
        Assert.Multiple(() =>
        {
            Assert.That(result.Rows, Has.Count.EqualTo(1));

            Assert.That(source.GetChunk(toMemory, new()), Is.EqualTo(null));
        });
    }


    [Test]
    public void Test_ElevationXmlLoading()
    {
        #region xml values

        const string validXml1 =
            @"<TagElevationRequestCollection>
  <TagElevationRequest>
    <ColumnName>CodeValueCol</ColumnName>
    <ElevationPathway>ProcedureCodeSequence->CodeValue</ElevationPathway>
  </TagElevationRequest>
</TagElevationRequestCollection>";

        const string validXml2 =
            @"<TagElevationRequestCollection>
  <TagElevationRequest>
    <ColumnName>CodeValueCol2</ColumnName>
    <ElevationPathway>ProcedureCodeSequence->CodeValue</ElevationPathway>
  </TagElevationRequest>
</TagElevationRequestCollection>";

        // Invalid because the pathway should be sequence->non sequence
        const string invalidXml =
            @"<TagElevationRequestCollection>
  <TagElevationRequest>
    <ColumnName>CodeValueCol</ColumnName>
    <ElevationPathway>CodeValue->CodeValue</ElevationPathway>
  </TagElevationRequest>
</TagElevationRequestCollection>";

        #endregion

        var source = new DicomFileCollectionSource();

        var file = Path.Combine(TestContext.CurrentContext.WorkDirectory, "me.xml");

        //no elevation to start with
        Assert.That(source.LoadElevationRequestsFile(), Is.Null);

        //illegal file
        File.WriteAllText(file, "<lolz>");
        source.TagElevationConfigurationFile = new(file);

        var ex = Assert.Throws<XmlException>(() => source.LoadElevationRequestsFile());
        Assert.That(ex?.Message, Does.Contain("Unexpected end of file"));

        File.WriteAllText(file, invalidXml);
        var ex2 = Assert.Throws<TagNavigationException>(() => source.LoadElevationRequestsFile());
        Assert.That(ex2?.Message, Does.Contain("Navigation Token CodeValue was not the final token in the pathway"));

        File.WriteAllText(file, validXml1);
        Assert.That(source.LoadElevationRequestsFile().Requests.Single().ColumnName, Is.EqualTo("CodeValueCol"));

        //Setting the xml property will override the file xml
        source.TagElevationConfigurationXml = new() { xml = "<lolz>" };

        var ex3 = Assert.Throws<XmlException>(() => source.LoadElevationRequestsFile());
        Assert.That(ex3?.Message, Does.Contain("Unexpected end of file"));

        source.TagElevationConfigurationXml = new() { xml = invalidXml };
        var ex4 = Assert.Throws<TagNavigationException>(() => source.LoadElevationRequestsFile());
        Assert.That(ex4?.Message, Does.Contain("Navigation Token CodeValue was not the final token in the pathway"));

        source.TagElevationConfigurationXml = new() { xml = validXml2 };
        Assert.That(source.LoadElevationRequestsFile().Requests.Single().ColumnName, Is.EqualTo("CodeValueCol2"));

        //now we go back to the file one (by setting the xml one to null)
        source.TagElevationConfigurationXml = null;
        Assert.That(source.LoadElevationRequestsFile().Requests.Single().ColumnName, Is.EqualTo("CodeValueCol"));
        source.TagElevationConfigurationXml = new() { xml = "" };
        Assert.That(source.LoadElevationRequestsFile().Requests.Single().ColumnName, Is.EqualTo("CodeValueCol"));
        source.TagElevationConfigurationXml = new() { xml = "  \r\n  " };
        Assert.That(source.LoadElevationRequestsFile().Requests.Single().ColumnName, Is.EqualTo("CodeValueCol"));
    }

    [Test]
    public void SR_treeFlatten()
    {
        var ds = srTest();
        var source = new DicomDatasetCollectionSource();
        source.PreInitialize(new ExplicitListDicomDatasetWorklist([ds], "test.dcm"), ThrowImmediatelyDataLoadEventListener.Quiet);
        using var dt = source.GetChunk(ThrowImmediatelyDataLoadEventListener.Quiet, new GracefulCancellationToken());
    }

    private static DicomDataset srTest() =>
        new([
            new DicomSequence(DicomTag.AnatomicRegionSequence,new DicomDataset([
                new DicomShortString(DicomTag.CodeValue, ""),
                new DicomShortString(DicomTag.CodingSchemeDesignator, ""),
                new DicomShortString(DicomTag.CodeMeaning, "")
            ])),
            new DicomIntegerString(DicomTag.LesionNumber, 1),
            new DicomCodeString(DicomTag.Laterality, [null]),
            new DicomSequence(DicomTag.AcquisitionContextSequence, new DicomDataset(
                new DicomCodeString(DicomTag.ValueType, "CODE"),
                new DicomSequence(DicomTag.ConceptNameCodeSequence, new DicomDataset(
                    new DicomShortString(DicomTag.CodingSchemeDesignator, "DCM"),
                    new DicomLongString(DicomTag.CodeMeaning, "Fitzpatrick Skin Type"),
                    new DicomUniqueIdentifier(DicomTag.ContextUID, "1.2.840.10008.6.1.1346")
                )),
                new DicomSequence(DicomTag.ConceptCodeSequence, new DicomDataset(
                    new DicomShortString(DicomTag.CodeValue, "C74571"),
                    new DicomShortString(DicomTag.CodingSchemeDesignator, "LN"),
                    new DicomLongString(DicomTag.CodeMeaning, "Fitzpatrick Skin Type III")
                )),
                new DicomCodeString(DicomTag.ValueType, "CODE"),
                new DicomSequence(DicomTag.ConceptNameCodeSequence, new DicomDataset(
                    new DicomShortString(DicomTag.CodeValue, "2F23"),
                    new DicomShortString(DicomTag.CodingSchemeDesignator, "I11"),
                    new DicomLongString(DicomTag.CodeMeaning, "Benign dermal fibrous or fibrohistiocytic neoplasms")
                )),
                new DicomSequence(DicomTag.ConceptCodeSequence, new DicomDataset(
                    new DicomShortString(DicomTag.CodeValue, "2F23.0"),
                    new DicomShortString(DicomTag.CodingSchemeDesignator, "I11"),
                    new DicomLongString(DicomTag.CodeMeaning, "Dermatofibroma")
                ))))
        ]);
}