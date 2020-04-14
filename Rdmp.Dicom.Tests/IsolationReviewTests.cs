using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FAnsi;
using NUnit.Framework;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.DataLoad;
using Rdmp.Dicom.PipelineComponents;
using Tests.Common;

namespace Rdmp.Dicom.Tests
{
    class IsolationReviewTests : DatabaseTests
    {

        [TestCase(DatabaseType.MicrosoftSQLServer)]
        [TestCase(DatabaseType.Oracle)]
        [TestCase(DatabaseType.PostgreSql)]
        [TestCase(DatabaseType.MySql)]
        public void TestFindTables(DatabaseType dbType)
        {
            var db = GetCleanedServer(dbType);

            var lmd = new LoadMetadata(CatalogueRepository, "ExampleLoad");
            var pt = new ProcessTask(CatalogueRepository, lmd,LoadStage.AdjustRaw);
            pt.ProcessTaskType = ProcessTaskType.MutilateDataTable;
            pt.Path = typeof(PrimaryKeyCollisionIsolationMutilation).FullName;
            pt.SaveToDatabase();

            //make an isolation db that is the 
            var eds = new ExternalDatabaseServer(CatalogueRepository,"Isolation db",null);
            eds.SetProperties(db);

            var args = pt.CreateArgumentsForClassIfNotExists(typeof(PrimaryKeyCollisionIsolationMutilation));

            var ti = new TableInfo(CatalogueRepository, "mytbl");

            SetArg(args, nameof(PrimaryKeyCollisionIsolationMutilation.IsolationDatabase), eds);
            SetArg(args, nameof(PrimaryKeyCollisionIsolationMutilation.TablesToIsolate), new []{ti});
            
            var reviewer = new IsolationReview(pt);

            //no error since it is configured correctly
            Assert.IsNull(reviewer.Error);

            //but no tables on the other end
            Assert.IsFalse(reviewer.GetIsolationTables().First().Value.Exists());

        }

        private void SetArg(IArgument[] args, string argName, object value)
        {
            var arg = args.Single(a=>a.Name.Equals(argName));
            arg.SetValue(value);
            arg.SaveToDatabase();
        }
    }
}
