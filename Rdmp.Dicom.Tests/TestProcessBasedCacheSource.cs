using MapsDirectlyToDatabaseTable;
using NUnit.Framework;
using Rdmp.Core.Caching.Requests;
using Rdmp.Core.Caching.Requests.FetchRequestProvider;
using Rdmp.Core.Curation;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.Cache;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.Startup;
using Rdmp.Dicom.Cache.Pipeline;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.Progress;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Tests.Common;

namespace Rdmp.Dicom.Tests
{
    class TestProcessBasedCacheSource : UnitTests
    {
        [Test]
        public void TestWithEcho()
        {
            var source = new ProcessBasedCacheSource();

            if(IsLinux)
            {   
                // TODO
            }
            else
            {
                source.Command = "cmd.exe";
                source.Args = "/c echo Hey Thomas go get %s";
            }
            source.TimeFormat = "dd/MM/yy";
            source.ThrowOnNonZeroExitCode = true;

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
            
            source.PreInitialize(new CacheFetchRequestProvider(cp), new ThrowImmediatelyDataLoadEventListener());
            source.PreInitialize(cp.CatalogueRepository,new ThrowImmediatelyDataLoadEventListener());
            source.PreInitialize(new PermissionWindow(cp.CatalogueRepository),new ThrowImmediatelyDataLoadEventListener());
            
            var toMem = new ToMemoryDataLoadEventListener(true);
            source.GetChunk(toMem,new GracefulCancellationToken());

            Assert.Contains("Hey Thomas go get 24/12/01",toMem.GetAllMessagesByProgressEventType()[ProgressEventType.Information].Select(v=>v.Message).ToArray());
            
            

        }

        public static bool IsLinux
        {
            get
            {
                int p = (int) Environment.OSVersion.Platform;
                return (p == 4) || (p == 6) || (p == 128);
            }
        }
    }
}
