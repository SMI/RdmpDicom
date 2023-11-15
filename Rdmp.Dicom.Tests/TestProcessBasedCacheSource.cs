using NUnit.Framework;
using Rdmp.Core.Caching.Requests.FetchRequestProvider;
using Rdmp.Core.Curation;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.Cache;
using Rdmp.Dicom.Cache.Pipeline;
using Rdmp.Core.ReusableLibraryCode.Progress;
using System;
using System.IO;
using System.Linq;
using Tests.Common;

namespace Rdmp.Dicom.Tests;

class TestProcessBasedCacheSource : UnitTests
{
    [Test]
    public void TestWithEcho()
    {
        var source = new ProcessBasedCacheSource();

        if (IsLinux)
        {
            source.Command = "/bin/echo";
            source.Args = "Hey Thomas go get %s and store in %d";
        }
        else
        {
            source.Command = "cmd.exe";
            source.Args = "/c echo Hey Thomas go get %s and store in %d";
        }

        source.TimeFormat = "dd/MM/yy";
        source.ThrowOnNonZeroExitCode = true;

        // What dates to load
        var cp = WhenIHaveA<CacheProgress>();
        cp.CacheFillProgress = new DateTime(2001, 12, 24);
        cp.SaveToDatabase();

        // Where to put files
        var lmd = cp.LoadProgress.LoadMetadata;

        var dir = new DirectoryInfo(TestContext.CurrentContext.WorkDirectory);
        var loadDir = LoadDirectory.CreateDirectoryStructure(dir, "blah", true);

        lmd.LocationOfFlatFiles = loadDir.RootPath.FullName;
        lmd.SaveToDatabase();

        source.PreInitialize(new CacheFetchRequestProvider(cp), ThrowImmediatelyDataLoadEventListener.Quiet);
        source.PreInitialize(cp.CatalogueRepository, ThrowImmediatelyDataLoadEventListener.Quiet);
        source.PreInitialize(new PermissionWindow(cp.CatalogueRepository), ThrowImmediatelyDataLoadEventListener.Quiet);

        var toMem = new ToMemoryDataLoadEventListener(true);
        var fork = new ForkDataLoadEventListener(toMem, ThrowImmediatelyDataLoadEventListener.Quiet);
        source.GetChunk(fork, new());

        Assert.Contains($"Hey Thomas go get 24/12/01 and store in {Path.Combine(loadDir.Cache.FullName, "ALL")}",
            toMem.GetAllMessagesByProgressEventType()[ProgressEventType.Information].Select(v => v.Message).ToArray());
    }

    private static bool IsLinux => Environment.OSVersion.Platform != PlatformID.Win32NT;
}