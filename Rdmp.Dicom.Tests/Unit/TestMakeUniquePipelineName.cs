using System;
using NUnit.Framework;
using Rdmp.Dicom.CommandExecution;

namespace Rdmp.Dicom.Tests.Unit;

internal class ExecuteCommandCreateNewImagingDatasetSuiteUnitTests
{
    [Test]
    public void TestMakeUniqueName()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ExecuteCommandCreateNewImagingDatasetSuite.MakeUniqueName(Array.Empty<string>(), "ff")
        , Is.EqualTo("ff"));

            Assert.That(ExecuteCommandCreateNewImagingDatasetSuite.MakeUniqueName(new[] { "ff" }, "ff")
    , Is.EqualTo("ff2"));
            Assert.That(ExecuteCommandCreateNewImagingDatasetSuite.MakeUniqueName(new[] { "ff", "ff2", "ff3" }, "ff")
    , Is.EqualTo("ff4"));
        });
    }

}