using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using DicomTypeTranslation.Elevation.Exceptions;
using NUnit.Framework;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.DataFlowPipeline.Requirements;
using Rdmp.Core.DataLoad.Engine.Job;
using Rdmp.Dicom.PipelineComponents.DicomSources;
using Rdmp.Dicom.PipelineComponents.DicomSources.Worklists;
using ReusableLibraryCode.Progress;

namespace Rdmp.Dicom.Tests.Unit;

public class DicomSourceUnitTests
{
        
    [Test]
    public void Test_Linux_Root()
    {
        var source = new DicomFileCollectionSource {ArchiveRoot = "/"};

        Assert.AreEqual("/",source.ArchiveRoot);
    }

    [TestCase(@"C:\bob\",@"\fish\1.dcm",@"/fish/1.dcm")]
    [TestCase(@"C:\bob",@"/fish/1.dcm",@"/fish/1.dcm")]
    [TestCase(@"C:\bob",@"./fish/1.dcm",@"./fish/1.dcm")]
    [TestCase(@"C:\bob",@"C:\bob\fish\1.dcm",@"./fish/1.dcm")]
    [TestCase(@"C:\bob\","C:/bob/fish/1.dcm","./fish/1.dcm")]
    [TestCase(@"C:\bob","C:/bob/fish/1.dcm","./fish/1.dcm")]
    [TestCase(@"C:\BOb","C:/bob/fish/1.dcm","./fish/1.dcm")] //capitalisation
    [TestCase(@"C:/bob/",@"C:\bob\fish/1.dcm","./fish/1.dcm")] //mixed slash directions!
    [TestCase("/bob/","/bob/fish/1.dcm","./fish/1.dcm")] //linux style paths
    [TestCase("/bob/",@"\bob\fish\1.dcm","./fish/1.dcm")]
    [TestCase(@"\\myserver\bob",@"\\myserver\bob\fish\1.dcm","./fish/1.dcm")] // UNC server paths
    [TestCase(@"\\myserver\bob",@"\\myOtherServer\bob\fish\1.dcm",@"\\myOtherServer/bob/fish/1.dcm")]
    [TestCase("/","/bob/fish/1.dcm","./bob/fish/1.dcm")]
    [TestCase(@"C:\bob\",@"D:\fish\1.dcm",@"D:/fish/1.dcm")] //not relative so just return verbatim string (with slash fixes)
    [TestCase(@"C:\bob\",@"D:/fish/1.dcm",@"D:/fish/1.dcm")]
    [TestCase(@"C:\bob\",@"C:\fish\bob\fish\1.dcm",@"C:/fish/bob/fish/1.dcm")] //not relative so just return verbatim string (with slash fixes)
    [TestCase(@"C:\bob\",@"./fish.dcm",@"./fish.dcm")]
    [TestCase(@"./bob/",@"./bob/fish.dcm",@"./fish.dcm")] //if the "root" is relative then we can still express this relative to it

    public void Test_ApplyArchiveRootToMakeRelativePath(string root, string inputPath, string expectedRelativePath)
    {
        var source = new DicomFileCollectionSource {ArchiveRoot = root};

        var result = source.ApplyArchiveRootToMakeRelativePath(inputPath);
        Assert.AreEqual(expectedRelativePath, result);
    }      
    [Test]
    public void AssembleDataTableFromFile()
    {
        var source = new DicomFileCollectionSource {FilenameField = "RelativeFileArchiveURI"};

        var f = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TestData/IM-0001-0013.dcm");

        source.PreInitialize(new FlatFileToLoadDicomFileWorklist(new(new(f))), new ThrowImmediatelyDataLoadEventListener());
        var result = source.GetChunk(new ThrowImmediatelyDataLoadEventListener(), new());

        Assert.AreEqual("IM00010013",result.TableName);
        Assert.Greater(result.Columns.Count,0);
            
        Assert.IsNull(source.GetChunk(new ThrowImmediatelyDataLoadJob(), new()));
    }

    [Test]
    public void AssembleDataTableFromFileArchive()
    {
        var zip = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData.zip");
        var dir = Path.Combine(TestContext.CurrentContext.TestDirectory,"TestData");
          
        if(File.Exists(zip))
            File.Delete(zip);
            
        ZipFile.CreateFromDirectory(dir,zip);

        var fileCount = Directory.GetFiles(dir,"*.dcm").Length;

        var source = new DicomFileCollectionSource {FilenameField = "RelativeFileArchiveURI"};
        source.PreInitialize(new FlatFileToLoadDicomFileWorklist(new(new(zip))), new ThrowImmediatelyDataLoadEventListener());
        var toMemory = new ToMemoryDataLoadEventListener(true);
        var result = source.GetChunk(toMemory, new());

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

        var source = new DicomFileCollectionSource {FilenameField = "RelativeFileArchiveURI"};
        source.PreInitialize(new FlatFileToLoadDicomFileWorklist(new(controlFile)), new ThrowImmediatelyDataLoadEventListener());
            
        var toMemory = new ToMemoryDataLoadEventListener(true);
        var result = source.GetChunk(toMemory, new());
        Assert.AreEqual(1,result.Rows.Count);

        result = source.GetChunk(toMemory, new());
        Assert.AreEqual(1, result.Rows.Count);

        Assert.AreEqual(null, source.GetChunk(toMemory, new()));
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

        var file = Path.Combine(TestContext.CurrentContext.WorkDirectory,"me.xml");

        //no elevation to start with
        Assert.IsNull(source.LoadElevationRequestsFile());

        //illegal file
        File.WriteAllText(file, "<lolz>");
        source.TagElevationConfigurationFile = new(file);
            
        var ex = Assert.Throws<XmlException>(()=>source.LoadElevationRequestsFile());
        StringAssert.Contains("Unexpected end of file",ex.Message);
            
        File.WriteAllText(file, invalidXml);
        var ex2 = Assert.Throws<TagNavigationException>(() => source.LoadElevationRequestsFile());
        StringAssert.Contains("Navigation Token CodeValue was not the final token in the pathway", ex2.Message);

        File.WriteAllText(file, validXml1);
        Assert.AreEqual("CodeValueCol",source.LoadElevationRequestsFile().Requests.Single().ColumnName);
            
        //Setting the xml property will override the file xml
        source.TagElevationConfigurationXml = new() {xml= "<lolz>" };

        var ex3 = Assert.Throws<XmlException>(() => source.LoadElevationRequestsFile());
        StringAssert.Contains("Unexpected end of file", ex3.Message);

        source.TagElevationConfigurationXml = new() { xml = invalidXml };
        var ex4 = Assert.Throws<TagNavigationException>(() => source.LoadElevationRequestsFile());
        StringAssert.Contains("Navigation Token CodeValue was not the final token in the pathway", ex4.Message);

        source.TagElevationConfigurationXml = new() { xml = validXml2 };
        Assert.AreEqual("CodeValueCol2", source.LoadElevationRequestsFile().Requests.Single().ColumnName);
            
        //now we go back to the file one (by setting the xml one to null)
        source.TagElevationConfigurationXml = null;
        Assert.AreEqual("CodeValueCol", source.LoadElevationRequestsFile().Requests.Single().ColumnName);
        source.TagElevationConfigurationXml = new() {xml = "" };
        Assert.AreEqual("CodeValueCol", source.LoadElevationRequestsFile().Requests.Single().ColumnName);
        source.TagElevationConfigurationXml = new() { xml = "  \r\n  " };
        Assert.AreEqual("CodeValueCol", source.LoadElevationRequestsFile().Requests.Single().ColumnName);
    }
}