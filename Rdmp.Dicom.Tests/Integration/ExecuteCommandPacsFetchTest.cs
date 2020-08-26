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
            
            var cmd = new ExecuteCommandPacsFetch(new ConsoleInputManager(RepositoryLocator, new ThrowImmediatelyCheckNotifier()){DisallowInput= true},"2010-01-01","2020-01-01","www.dicomserver.co.uk",104,"you","localhost",23,"me",TestContext.CurrentContext.WorkDirectory);
            cmd.Execute();
        }
    }
}
