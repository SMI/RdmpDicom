using System.IO;
using NUnit.Framework;
using Rdmp.Dicom.PipelineComponents.CFind;
using Rdmp.Core.ReusableLibraryCode.Checks;
using Rdmp.Core.ReusableLibraryCode.Progress;

namespace Rdmp.Dicom.Tests.Unit;
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

        Assert.DoesNotThrow(() => source.Check(ThrowImmediatelyCheckNotifier.Quiet));

        source.PreInitialize(new(new(inventory)), ThrowImmediatelyDataLoadEventListener.Quiet);

        var dt = source.GetChunk(ThrowImmediatelyDataLoadEventListener.Quiet, new());

        /*
     * 
someAE	DX\SR	XR Facial bones	0102030405	TEXT	1.2.3.4.50	20200416
someAE	DX\SR	XR Elbow Lt	0102030405	TEXT	1.2.3.4.60	20200416
someAE	XA\SR	Fluoroscopy upper limb Lt	0102030405	TEXT	1.2.3.4.70	20200416
*/

        Assert.That(dt.Rows, Has.Count.EqualTo(3));

        Assert.Multiple(() =>
        {
            Assert.That(dt.Rows[0]["StudyDescription"], Is.EqualTo("XR Facial bones"));
            Assert.That(dt.Rows[1]["StudyDescription"], Is.EqualTo("XR Elbow Lt"));
            Assert.That(dt.Rows[2]["StudyDescription"], Is.EqualTo("Fluoroscopy upper limb Lt"));

            Assert.That(dt.Rows[0]["StudyInstanceUID"], Is.EqualTo("1.2.3.4.50"));
            Assert.That(dt.Rows[1]["StudyInstanceUID"], Is.EqualTo("1.2.3.4.60"));
            Assert.That(dt.Rows[2]["StudyInstanceUID"], Is.EqualTo("1.2.3.4.70"));

            Assert.That(dt.Rows[0]["RetrieveAETitle"], Is.EqualTo("someAE"));
            Assert.That(dt.Rows[1]["RetrieveAETitle"], Is.EqualTo("someAE"));
            Assert.That(dt.Rows[2]["RetrieveAETitle"], Is.EqualTo("someAE"));
        });

        Assert.That(dt.TableName, Is.Not.Null);
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

        Assert.DoesNotThrow(() => source.Check(ThrowImmediatelyCheckNotifier.Quiet));

        source.PreInitialize(new(new(inventory)), ThrowImmediatelyDataLoadEventListener.Quiet);

        var dt = source.GetChunk(ThrowImmediatelyDataLoadEventListener.Quiet, new());

        Assert.That(dt.Rows, Has.Count.EqualTo(3));
    }
}
