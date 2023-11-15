using System;
using NUnit.Framework;
using Rdmp.Dicom.CommandExecution;

namespace Rdmp.Dicom.Tests.Unit;

internal class ExecuteCommandCreateNewImagingDatasetSuiteUnitTests
{
    [Test]
    public void TestMakeUniqueName()
    {
        Assert.AreEqual("ff",
            ExecuteCommandCreateNewImagingDatasetSuite.MakeUniqueName(Array.Empty<string>(), "ff")
        );

        Assert.AreEqual("ff2",
            ExecuteCommandCreateNewImagingDatasetSuite.MakeUniqueName(new[] {"ff" }, "ff")
        );
        Assert.AreEqual("ff4",
            ExecuteCommandCreateNewImagingDatasetSuite.MakeUniqueName(new[] { "ff","ff2","ff3" }, "ff")
        );
    }

}