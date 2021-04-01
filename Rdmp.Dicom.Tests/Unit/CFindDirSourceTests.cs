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
    public void Test_ReadExampleXml_File()
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

        /*
         * 
someAE	DX\SR	XR Facial bones	0102030405	TEXT	1.2.3.4.50	20200416
someAE	DX\SR	XR Elbow Lt	0102030405	TEXT	1.2.3.4.60	20200416
someAE	XA\SR	Fluoroscopy upper limb Lt	0102030405	TEXT	1.2.3.4.70	20200416
*/
        
        Assert.AreEqual(3, dt.Rows.Count);


        Assert.AreEqual("XR Facial bones", dt.Rows[0]["StudyDescription"]);
        Assert.AreEqual("XR Elbow Lt", dt.Rows[1]["StudyDescription"]);
        Assert.AreEqual("Fluoroscopy upper limb Lt", dt.Rows[2]["StudyDescription"]);

        Assert.AreEqual("1.2.3.4.50", dt.Rows[0]["StudyInstanceUID"]);
        Assert.AreEqual("1.2.3.4.60", dt.Rows[1]["StudyInstanceUID"]);
        Assert.AreEqual("1.2.3.4.70", dt.Rows[2]["StudyInstanceUID"]);

        Assert.AreEqual("someAE", dt.Rows[0]["RetrieveAETitle"]);
        Assert.AreEqual("someAE", dt.Rows[1]["RetrieveAETitle"]);
        Assert.AreEqual("someAE", dt.Rows[2]["RetrieveAETitle"]);
    }


    [Test]
    public void Test_ReadExampleXml_Directory()
    {
        var source = new CFindDirSource();

        var work = Path.Combine(TestContext.CurrentContext.WorkDirectory, "TestData");
        var anonxml = Path.Combine(work, "anonResult.xml");
        var inventory = Path.Combine(work, "inventory.txt");

        FileAssert.Exists(anonxml);

        // provide the dir as the inventory file
        File.WriteAllText(inventory, work);

        source.SearchPattern = "anonResult.xml";

        Assert.DoesNotThrow(() => source.Check(new ThrowImmediatelyCheckNotifier()));

        source.PreInitialize(new FlatFileToLoad(new FileInfo(inventory)), new ThrowImmediatelyDataLoadEventListener());

        var dt = source.GetChunk(new ThrowImmediatelyDataLoadEventListener(), new GracefulCancellationToken());

        Assert.AreEqual(3, dt.Rows.Count);
    }
}