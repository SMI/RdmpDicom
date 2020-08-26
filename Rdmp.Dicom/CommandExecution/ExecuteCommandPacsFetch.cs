using MongoDB.Bson;
using Rdmp.Core.Caching.Requests;
using Rdmp.Core.Caching.Requests.FetchRequestProvider;
using Rdmp.Core.CommandExecution;
using Rdmp.Core.Curation;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.Cache;
using Rdmp.Core.Curation.Data.DataLoad;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.Repositories;
using Rdmp.Dicom.Cache.Pipeline;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.Progress;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Rdmp.Dicom.CommandExecution
{
    class ExecuteCommandPacsFetch : BasicCommandExecution, ICacheFetchRequestProvider
    {
        private BackfillCacheFetchRequest _request;
        private PACSSource _source;

        public ExecuteCommandPacsFetch(IBasicActivateItems activator,string start, string end, string remoteAeUri, int remotePort, string localAeUri, int localPort, string localAeTitle, string outDir):base(activator)
        {
            var startDate = DateTime.Parse(start);   
            var endDate =  DateTime.Parse(end);   
                        
            // Make something that kinda looks like a valid DLE load
            var memory = new MemoryCatalogueRepository();
            var lmd = new LoadMetadata(memory);

            var dir = Directory.CreateDirectory(outDir);
            var results = LoadDirectory.CreateDirectoryStructure(dir,"out",true);
            lmd.LocationOfFlatFiles = results.RootPath.FullName;
            lmd.SaveToDatabase();

            var lp = new LoadProgress(memory,lmd);
            var cp = new CacheProgress(memory,lp);

            //Create the source component only and a valid request range to fetch
            _source = new PACSSource();
            _source.RemoteAEUri = new Uri("http://"+ remoteAeUri); //<- rly? its not gonna pass without an http!?
            _source.RemoteAEPort = remotePort;
            _source.LocalAEUri = new Uri("http://" + localAeUri);
            _source.LocalAEPort = localPort;
            _source.LocalAETitle = localAeTitle;
            _source.TransferTimeOutInSeconds = 50000;
            _source.Modality = "ALL";

            _request = new BackfillCacheFetchRequest(BasicActivator.RepositoryLocator.CatalogueRepository,startDate);
            _request.ChunkPeriod = endDate.Subtract(startDate);
            _request.CacheProgress = cp;
            
            //Initialize it
            _source.PreInitialize(BasicActivator.RepositoryLocator.CatalogueRepository, new ThrowImmediatelyDataLoadEventListener(){WriteToConsole=true });
            _source.PreInitialize(this,new ThrowImmediatelyDataLoadEventListener(){WriteToConsole=true});

        }


        public override void Execute()
        {
            base.Execute();

            _source.GetChunk(new ThrowImmediatelyDataLoadEventListener(){WriteToConsole = true},new GracefulCancellationToken());

        }
        public ICacheFetchRequest Current => _request;

        public ICacheFetchRequest GetNext(IDataLoadEventListener listener)
        {
            return _request;
        }
    }
}
