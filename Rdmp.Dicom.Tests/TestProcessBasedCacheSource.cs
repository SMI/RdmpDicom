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
using Rdmp.Core.DataFlowPipeline;
using Tests.Common;

namespace Rdmp.Dicom.Tests;

internal class TestProcessBasedCacheSource : UnitTests
{
    [Test]
    public void TestWithEcho()
    {
        var source = new ProcessBasedCacheSource
        {
            TimeFormat = "dd/MM/yy",
            ThrowOnNonZeroExitCode = true
        };

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

        // What dates to load
        var cp = WhenIHaveA<CacheProgress>();
        cp.CacheFillProgress = new DateTime(2001,12,24);
        cp.SaveToDatabase();
            
        // Where to put files
        var lmd = cp.LoadProgress.LoadMetadata;

        var dir = new DirectoryInfo(TestContext.CurrentContext.WorkDirectory);
        var loadDir = LoadDirectory.CreateDirectoryStructure(dir,"blah",true);

        lmd.LocationOfFlatFiles = loadDir.RootPath.FullName;            
        lmd.SaveToDatabase();

        var thrower = new ThrowImmediatelyDataLoadEventListener();
        source.PreInitialize(new CacheFetchRequestProvider(cp), thrower);
        source.PreInitialize(cp.CatalogueRepository,thrower);
        source.PreInitialize(new PermissionWindow(cp.CatalogueRepository),thrower);
            
        var toMem = new ToMemoryDataLoadEventListener(true);
        var fork = new ForkDataLoadEventListener(toMem,thrower);

        source.GetChunk(fork,new GracefulCancellationToken());

        Assert.Contains($"Hey Thomas go get 24/12/01 and store in {Path.Combine(loadDir.Cache.FullName,"ALL")}",toMem.GetAllMessagesByProgressEventType()[ProgressEventType.Information].Select(v=>v.Message).ToArray());
    }

    private static bool IsLinux => Environment.OSVersion.Platform is PlatformID.MacOSX or PlatformID.Other or PlatformID.Unix;
}