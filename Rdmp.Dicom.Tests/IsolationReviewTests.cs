using System.Data;
using System.Linq;
using FAnsi;
using NUnit.Framework;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.DataLoad;
using Rdmp.Dicom.PipelineComponents;
using Tests.Common;

namespace Rdmp.Dicom.Tests;

class IsolationReviewTests : DatabaseTests
{

    [TestCase(DatabaseType.MicrosoftSQLServer)]
    [TestCase(DatabaseType.Oracle)]
    [TestCase(DatabaseType.PostgreSql)]
    [TestCase(DatabaseType.MySql)]
    public void TestFindTables(DatabaseType dbType)
    {
        var db = GetCleanedServer(dbType);

        using var dt = new DataTable();
        dt.Columns.Add("A");
        dt.Columns.Add("B");
        dt.Columns.Add("C");

        // 'pk' 1 differs on col B AND col C
        dt.Rows.Add(1, 2, 3);
        dt.Rows.Add(1, 3, 2);

        //novel (should not appear in diff table)
        dt.Rows.Add(4, 1, 1);

        //novel (should not appear in diff table)
        dt.Rows.Add(5, 1, 1);

        // 'pk' 2 differs on col C
        dt.Rows.Add(2, 1, 1);
        dt.Rows.Add(2, 1, 2);

        //novel (should not appear in diff table)
        dt.Rows.Add(6, 1, 1);

        // 'pk' 3 differs on col B
        dt.Rows.Add(3, 1, 1);
        dt.Rows.Add(3, 2, 1);
            
            
        db.CreateTable("mytbl_Isolation",dt);

        var lmd = new LoadMetadata(CatalogueRepository, "ExampleLoad");
        var pt = new ProcessTask(CatalogueRepository, lmd, LoadStage.AdjustRaw)
        {
            ProcessTaskType = ProcessTaskType.MutilateDataTable,
            Path = typeof(PrimaryKeyCollisionIsolationMutilation).FullName
        };
        pt.SaveToDatabase();

        //make an isolation db that is the 
        var eds = new ExternalDatabaseServer(CatalogueRepository,"Isolation db",null);
        eds.SetProperties(db);

        var args = pt.CreateArgumentsForClassIfNotExists(typeof(PrimaryKeyCollisionIsolationMutilation));

        var ti = new TableInfo(CatalogueRepository, "mytbl");
        var ci = new ColumnInfo(CatalogueRepository, "A", "varchar(1)", ti) {IsPrimaryKey = true};
        ci.SaveToDatabase();

        SetArg(args, "IsolationDatabase", eds);
        SetArg(args, "TablesToIsolate", new []{ti});
            
        var reviewer = new IsolationReview(pt);

        //no error since it is configured correctly
        Assert.IsNull(reviewer.Error);

        //tables should exist
        var isolationTables = reviewer.GetIsolationTables();
        Assert.IsTrue(isolationTables.Single().Value.Exists());

            
        var diffDataTable = reviewer.GetDifferences(isolationTables.Single(),out var diffs);
            
        Assert.AreEqual(6,diffDataTable.Rows.Count);
        Assert.AreEqual(6,diffs.Count);
    }

    private void SetArg(IArgument[] args, string argName, object value)
    {
        var arg = args.Single(a=>a.Name.Equals(argName));
        arg.SetValue(value);
        arg.SaveToDatabase();
    }
}