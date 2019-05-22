using NUnit.Framework;
using Rdmp.Dicom.Cache;
using ReusableLibraryCode.Progress;
using System.IO;

namespace Rdmp.Dicom.Tests.Integration
{
    class SMICacheLayoutTests
    {
        [Test]
        public void TestFactoryConstruction()
        {
            var rootDirectory = new DirectoryInfo(TestContext.CurrentContext.WorkDirectory);
            var layout = new SMICacheLayout(rootDirectory,new SMICachePathResolver("CT"));

            var downloadDirectory = layout.GetLoadCacheDirectory(new ThrowImmediatelyDataLoadEventListener());

            Assert.AreEqual(downloadDirectory.FullName, Path.Combine(rootDirectory.FullName, "CT"));
        }
    }
}
