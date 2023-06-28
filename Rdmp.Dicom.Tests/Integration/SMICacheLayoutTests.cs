using NUnit.Framework;
using Rdmp.Dicom.Cache;
using Rdmp.Core.ReusableLibraryCode.Progress;
using System.IO;

namespace Rdmp.Dicom.Tests.Integration;

class SMICacheLayoutTests
{
    [Test]
    public void TestFactoryConstruction()
    {
        var rootDirectory = new DirectoryInfo(TestContext.CurrentContext.WorkDirectory);
        var layout = new SMICacheLayout(rootDirectory,new("CT"));

        var downloadDirectory = layout.GetLoadCacheDirectory(ThrowImmediatelyDataLoadEventListener.Quiet);

        Assert.AreEqual(downloadDirectory.FullName, Path.Combine(rootDirectory.FullName, "CT"));
    }
}