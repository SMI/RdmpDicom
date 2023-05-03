using FAnsi.Discovery;
using Rdmp.Core.MapsDirectlyToDatabaseTable.Versioning;
using NUnit.Framework;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.Aggregation;
using Rdmp.Core.Curation.Data.Cohort;
using Rdmp.Core.Databases;
using Rdmp.Core.QueryCaching.Aggregation;
using Rdmp.Dicom.ExternalApis;
using Rdmp.Core.ReusableLibraryCode.Checks;
using System.Threading;
using Rdmp.Core.CohortCreation.Execution;
using Tests.Common;
using DatabaseType = FAnsi.DatabaseType;

namespace Rdmp.Dicom.Tests.Integration;

public class SemEHRApiCallerTests : DatabaseTests
{

    public CachedAggregateConfigurationResultsManager SetupCache(DatabaseType dbType, out DiscoveredDatabase cacheDb)
    {
        cacheDb = GetCleanedServer(dbType);
        var creator = new MasterDatabaseScriptExecutor(cacheDb);
        var patcher = new QueryCachingPatcher();

        creator.CreateAndPatchDatabase(patcher, new AcceptAllCheckNotifier());

        var eds = new ExternalDatabaseServer(CatalogueRepository, "cache", patcher);
        eds.SetProperties(cacheDb);

        return new CachedAggregateConfigurationResultsManager(eds);
    }


    [RequiresSemEHR]
    [TestCase(DatabaseType.MicrosoftSQLServer)]
    public void TalkToApi(DatabaseType dbType)
    {
        var cacheMgr = SetupCache(dbType, out var cacheDb);
        var caller = new SemEHRApiCaller();

        var cata = new Catalogue(CatalogueRepository, $"{PluginCohortCompiler.ApiPrefix}cata");
        var cic = new CohortIdentificationConfiguration(CatalogueRepository, "my cic");
        cic.CreateRootContainerIfNotExists();
            
        var ac = new AggregateConfiguration(CatalogueRepository, cata, "blah");
        cic.RootCohortAggregateContainer.AddChild(ac,0);

        var semEHRConfiguration = new SemEHRConfiguration()
        {
            Url = RequiresSemEHR.SemEHRTestUrl + "/api/search_anns/myQuery/",
            Query = "C0205076",
            ValidateServerCert = false
        };

        caller.Run(ac, cacheMgr, CancellationToken.None, semEHRConfiguration);

        var resultTable = cacheMgr.GetLatestResultsTableUnsafe(ac, AggregateOperation.IndexedExtractionIdentifierList);

        Assert.IsNotNull(resultTable);

        var tbl = cacheDb.ExpectTable(resultTable.GetRuntimeName());
        Assert.AreEqual(75, tbl.GetDataTable().Rows.Count);
    }
}