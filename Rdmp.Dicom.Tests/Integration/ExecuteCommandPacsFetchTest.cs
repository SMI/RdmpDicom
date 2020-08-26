using NUnit.Framework;
using Rdmp.Core.CommandLine.Interactive;
using Rdmp.Dicom.CommandExecution;
using ReusableLibraryCode.Checks;
using System;
using System.Collections.Generic;
using System.Text;
using Tests.Common;

namespace Rdmp.Dicom.Tests.Integration
{
    class ExecuteCommandPacsFetchTest : DatabaseTests
    {
        [Test]
        public void TestLocal()
        {
            
            var cmd = new ExecuteCommandPacsFetch(new ConsoleInputManager(RepositoryLocator, new ThrowImmediatelyCheckNotifier()){DisallowInput= true},"2001-01-01","2002-01-01","localhost",22,"localhost",23,"myHappyDicom",TestContext.CurrentContext.WorkDirectory);
            
            
            var ex = Assert.Throws<Exception>(cmd.Execute);

            StringAssert.StartsWith("Error when attempting to send DICOM request: One or more errors occurred. (One or more errors occurred. (No connection could be made because the target machine actively refused it.",ex.Message);

        }
    }
}
