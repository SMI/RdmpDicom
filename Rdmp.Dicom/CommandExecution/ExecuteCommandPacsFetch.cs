using Rdmp.Core.Caching.Requests;
using Rdmp.Core.Caching.Requests.FetchRequestProvider;
using Rdmp.Core.CommandExecution;
using Rdmp.Core.Curation;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.Cache;
using Rdmp.Core.Curation.Data.DataLoad;
using Rdmp.Core.Repositories;
using Rdmp.Dicom.Cache.Pipeline;
using Rdmp.Core.ReusableLibraryCode.Progress;
using System;
using System.IO;

namespace Rdmp.Dicom.CommandExecution;

class ExecuteCommandPacsFetch : BasicCommandExecution, ICacheFetchRequestProvider
{
    private BackfillCacheFetchRequest _request;
    private PACSSource _source;

    public ExecuteCommandPacsFetch(IBasicActivateItems activator,string start, string end, string remoteAeHost, ushort remotePort,string remoteAeTitle, string localAeHost, ushort localPort, string localAeTitle, string outDir, int maxRetries):base(activator)
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
        _source = new()
        {
            RemoteAEHost = remoteAeHost,
            RemoteAEPort = remotePort,
            RemoteAETitle = remoteAeTitle,
            LocalAEHost = localAeHost,
            LocalAEPort = localPort,
            LocalAETitle = localAeTitle,
            TransferTimeOutInSeconds = 50000,
            Modality = "ALL",
            MaxRetries = maxRetries
        };
        //<- rly? its not gonna pass without an http!?

        _request = new(BasicActivator.RepositoryLocator.CatalogueRepository, startDate)
        {
            ChunkPeriod = endDate.Subtract(startDate), CacheProgress = cp
        };

        //Initialize it
        _source.PreInitialize(BasicActivator.RepositoryLocator.CatalogueRepository, ThrowImmediatelyDataLoadEventListener.Quiet);
        _source.PreInitialize(this, ThrowImmediatelyDataLoadEventListener.Quiet);

    }


    public override void Execute()
    {
        base.Execute();

        _source.GetChunk(ThrowImmediatelyDataLoadEventListener.Quiet,new());

    }
    public ICacheFetchRequest Current => _request;

    public ICacheFetchRequest GetNext(IDataLoadEventListener listener)
    {
        return _request;
    }
}