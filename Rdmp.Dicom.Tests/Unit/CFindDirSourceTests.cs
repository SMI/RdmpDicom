using NUnit.Framework;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.DataFlowPipeline.Requirements;
using Rdmp.Dicom;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.Progress;
using System.IO;

public class CFindDirSourceTests
{
    [Test]
    public void Test_ReadExampleXml()
    {
        var source = new CFindDirSource();

        var work = TestContext.CurrentContext.WorkDirectory;
        var anonxml = Path.Combine(work, "TestData/anonResult.xml");
        var inventory = Path.Combine(work, "TestData/inventory.txt");

        FileAssert.Exists(anonxml);

        File.WriteAllText(inventory, anonxml);
        source.SearchPattern = "anonResult.xml";

        Assert.DoesNotThrow(() => source.Check(new ThrowImmediatelyCheckNotifier()));

        source.PreInitialize(new FlatFileToLoad(new FileInfo(inventory)), new ThrowImmediatelyDataLoadEventListener());

        var dt = source.GetChunk(new ThrowImmediatelyDataLoadEventListener(), new GracefulCancellationToken());
        Assert.AreEqual(3, dt.Rows);
    }
}