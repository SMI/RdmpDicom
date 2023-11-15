using NUnit.Framework;
using Rdmp.Core.CommandLine.Interactive;
using Rdmp.Dicom.CommandExecution;
using Rdmp.Core.ReusableLibraryCode.Checks;
using Tests.Common;

namespace Rdmp.Dicom.Tests.Integration;

class ExecuteCommandPacsFetchTest : DatabaseTests
{
    [Ignore("Can be run manually, but that's a lot of data to pull!")]
    [Test]
    public void TestLocal()
    {
        var cmd = new ExecuteCommandPacsFetch(new ConsoleInputManager(RepositoryLocator, ThrowImmediatelyCheckNotifier.Quiet){DisallowInput= true},"2013-01-01","2014-01-01","www.dicomserver.co.uk",11112,"you","localhost",11112,"me",TestContext.CurrentContext.WorkDirectory,0);
        cmd.Execute();
    }
}