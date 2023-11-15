using NUnit.Framework;
using Rdmp.Core.CommandLine.Interactive;
using Rdmp.Dicom.CommandExecution;
using Rdmp.Core.ReusableLibraryCode.Checks;
using System.IO;
using Tests.Common;

namespace Rdmp.Dicom.Tests.Unit;

class CFindSourceTests : UnitTests
{
    [Test]
    public void TestRunFindOn_PublicServer()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.WorkDirectory);

        var cmd = new ExecuteCommandCFind(
            new ConsoleInputManager(RepositoryLocator, ThrowImmediatelyCheckNotifier.Quiet) { DisallowInput = true },
            "2001-01-01",
            "2002-01-01",
            "www.dicomserver.co.uk",
            104,
            "you",
            "me",
            dir.FullName);
        cmd.Execute();

        // file name is miday on 2001 1st January
        var f = Path.Combine(dir.FullName, @"out/Data/Cache/ALL/20010101120000.csv");
        FileAssert.Exists(f);
            
        var result = File.ReadAllLines(f);

        // should be at least 1 image in the public test server
        Assert.GreaterOrEqual(result.Length,1);
    }
}