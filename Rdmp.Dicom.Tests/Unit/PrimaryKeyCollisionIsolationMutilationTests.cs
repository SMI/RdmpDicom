using FAnsi;
using FAnsi.Discovery;
using Moq;
using NUnit.Framework;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.DataLoad;
using Rdmp.Core.Curation.Data.EntityNaming;
using Rdmp.Core.DataLoad.Engine.DatabaseManagement.EntityNaming;
using Rdmp.Core.DataLoad.Engine.Job;
using Rdmp.Dicom.PipelineComponents;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.DataAccess;
using System;
using System.Data;
using System.Linq;
using Tests.Common;
using TypeGuesser;

namespace Rdmp.Dicom.Tests.Unit
{
    class PrimaryKeyCollisionIsolationMutilationTests:DatabaseTests
    {
        [TestCase(DatabaseType.MicrosoftSQLServer)]
        [TestCase(DatabaseType.MySql)]
        public void Test_IsolateSingleTable_Check(DatabaseType dbType)
        {
            var db = GetCleanedServer(dbType);

            //Create a table in 'RAW' (has no constraints)
            var dt = new DataTable();
            dt.Columns.Add("A");
            dt.Columns.Add("B");
            dt.Rows.Add("Fish", 12);

            var tbl = db.CreateTable("CoolTable", dt);

            //import the table and make A look like a primary key to the metadata layer (and A would be pk in LIVE but not in RAW ofc)
            TableInfo tableInfoCreated;
            ColumnInfo[] columnInfosCreated;

            Import(tbl, out tableInfoCreated, out columnInfosCreated);

            //lie abot the primary key status
            var a = columnInfosCreated.Single(c => c.GetRuntimeName().Equals("A"));
            a.IsPrimaryKey = true;
            a.SaveToDatabase();

            var mutilator = GetMutilator(db,tableInfoCreated);

            //first time no tables exist so they must be created
            mutilator.Check(new AcceptAllCheckNotifier());
            
            var isolationTable = db.ExpectTable("CoolTable_Isolation");
            Assert.IsTrue(isolationTable.Exists());
            Assert.IsTrue(isolationTable.DiscoverColumns().Any(c=>c.GetRuntimeName().Equals("A")));
            Assert.IsTrue(isolationTable.DiscoverColumns().Any(c => c.GetRuntimeName().Equals("hic_dataLoadRunID")));

            //the check should pass second time without needing to accept any fixes
            mutilator.Check(new ThrowImmediatelyCheckNotifier());
        }

        private PrimaryKeyCollisionIsolationMutilation GetMutilator(DiscoveredDatabase db, params TableInfo[] tableInfoCreated)
        {
            //tell the mutilator to resolve the primary key collision on column A by isolating the rows 
            var mutilation = new PrimaryKeyCollisionIsolationMutilation();
            mutilation.TablesToIsolate =  tableInfoCreated;

            //tell the mutilator to set up isolation into the provided database
            var serverPointer = new ExternalDatabaseServer(CatalogueRepository, "Isolation Db",null);
            serverPointer.SetProperties(db);

            mutilation.IsolationDatabase = serverPointer;

            return mutilation;
        }

        [TestCase(DatabaseType.MicrosoftSQLServer)]
        [TestCase(DatabaseType.MySql)]
        public void Test_IsolateSingleTable_Dupliction(DatabaseType dbType)
        {
            var db = GetCleanedServer(dbType);

            //Create a table in 'RAW' (has no constraints)
            var dt = new DataTable();
            dt.Columns.Add("A");
            dt.Columns.Add("B");

            dt.Rows.Add("Fish", 1);
            dt.Rows.Add("Fish", 2);
            dt.Rows.Add("Fish", 3);
            dt.Rows.Add("Frank", 2);
            dt.Rows.Add("Candy", 2);

            var tbl = db.CreateTable("MyCoolTable2", dt);

            //import the table and make A look like a primary key to the metadata layer (and A would be pk in LIVE but not in RAW ofc)
            TableInfo tableInfoCreated;
            ColumnInfo[] columnInfosCreated;

            Import(tbl, out tableInfoCreated, out columnInfosCreated);
            
            //lie abot the primary key status
            var a = columnInfosCreated.Single(c => c.GetRuntimeName().Equals("A"));
            a.IsPrimaryKey = true;
            a.SaveToDatabase();

            var mutilator = GetMutilator(db, tableInfoCreated);
            mutilator.Check(new AcceptAllCheckNotifier());

            var config = new HICDatabaseConfiguration(db.Server,RdmpMockFactory.Mock_INameDatabasesAndTablesDuringLoads(db, "MyCoolTable2"));
            var job = Mock.Of<IDataLoadJob>(p => p.JobID == 999 &&  p.Configuration == config);
                                    
            mutilator.Initialize(db,LoadStage.AdjustRaw);
            mutilator.Mutilate(job);

            dt = tbl.GetDataTable();
            Assert.AreEqual(2,dt.Rows.Count); //candy and frank should be left 

            var dtIsolation = tbl.Database.ExpectTable("MyCoolTable2_Isolation").GetDataTable();
            Assert.AreEqual(3, dtIsolation.Rows.Count); //candy and frank should be left 
        }

        [TestCase(DatabaseType.MicrosoftSQLServer)]
        [TestCase(DatabaseType.MySql)]
        public void Test_IsolateTwoTables_Dupliction(DatabaseType dbType)
        {
            var db = GetCleanedServer(dbType);

            //Create a table in 'RAW' (has no constraints)
            var dt = new DataTable();
            dt.Columns.Add("SeriesInstanceUID");
            dt.Columns.Add("Seriesly");

            dt.Rows.Add("1.2.3", 1); //collision with children (see d2)
            dt.Rows.Add("1.2.3", 2);
            dt.Rows.Add("2.3.4", 3); //collision with no children
            dt.Rows.Add("2.3.4", 4);
            dt.Rows.Add("5.2.1", 2); //unique
            dt.Rows.Add("9.9.9", 2); // not a collision but should be deleted because of child collision

            //Create a table in 'RAW' (has no constraints)
            var dt2 = new DataTable();
            dt2.Columns.Add("SeriesInstanceUID");
            dt2.Columns.Add("SOPInstanceUID");
            dt2.Columns.Add("PatientName");

            dt2.Rows.Add("1.2.3", "1.2.1", "Frank");
                //these are not a collision because SOPInstanceUID is the pk for this table but they should also be isolated because their series contains duplication
            dt2.Rows.Add("1.2.3", "1.2.2", "Dave");

            dt2.Rows.Add("5.2.1", "1.1.1", "jjj"); //some more normal records in child
            dt2.Rows.Add("5.2.1", "1.1.2", "fff");
            dt2.Rows.Add("5.2.1", "1.1.3", "kkk");

            dt2.Rows.Add("9.9.9", "1.1.5", "zkk"); //collision on SOPInstanceUID ("1.1.5")
            dt2.Rows.Add("9.9.9", "1.1.5", "zzb");

            var tblParent = db.CreateTable("Parent", dt, new[]
            {
                new DatabaseColumnRequest("SeriesInstanceUID",new DatabaseTypeRequest(typeof(string))),
            });
            
            var tblChild = db.CreateTable("Child", dt2,new []
            {
                new DatabaseColumnRequest("SeriesInstanceUID",new DatabaseTypeRequest(typeof(string))),
                new DatabaseColumnRequest("SOPInstanceUID",new DatabaseTypeRequest(typeof(string)))
            });

            //import the table and make A look like a primary key to the metadata layer (and A would be pk in LIVE but not in RAW ofc)
            TableInfo parentTableInfo;
            ColumnInfo[] parentColumnInfosCreated;

            TableInfo childTableInfo;
            ColumnInfo[] childColumnInfosCreated;

            Import(tblParent, out parentTableInfo, out parentColumnInfosCreated);
            Import(tblChild, out childTableInfo, out childColumnInfosCreated);

            //make sure RDMP knows joins start with this table
            parentTableInfo.IsPrimaryExtractionTable = true;
            parentTableInfo.SaveToDatabase();

            //lie abot the primary key statuses
            var seriesInstanceUIdCol =
                parentColumnInfosCreated.Single(c => c.GetRuntimeName().Equals("SeriesInstanceUID"));
            seriesInstanceUIdCol.IsPrimaryKey = true;
            seriesInstanceUIdCol.SaveToDatabase();

            var sopInstanceUIdCol = childColumnInfosCreated.Single(c => c.GetRuntimeName().Equals("SOPInstanceUID"));
            sopInstanceUIdCol.IsPrimaryKey = true;
            sopInstanceUIdCol.SaveToDatabase();

            //Create a new mutilator for these two tables
            var mutilator = GetMutilator(db, parentTableInfo, childTableInfo);

            //checking should fail because it doesn't know how to join tables
            var ex = Assert.Throws<Exception>(() => mutilator.Check(new AcceptAllCheckNotifier()));
            StringAssert.Contains("join", ex.Message); //should be complaining about missing join infos

            //tell RDMP about how to join tables
            new JoinInfo(CatalogueRepository,childColumnInfosCreated.Single(
                c => c.GetRuntimeName().Equals("SeriesInstanceUID")),
                parentColumnInfosCreated.Single(c => c.GetRuntimeName().Equals("SeriesInstanceUID")),
                ExtractionJoinType.Right, null);

            //now that we have a join it should pass checks
            mutilator.Check(new AcceptAllCheckNotifier());

            var config = new HICDatabaseConfiguration(db.Server,new ReturnSameString());
            var job = Mock.Of<IDataLoadJob>(j => j.JobID==999 && j.Configuration == config);            

            mutilator.Initialize(db, LoadStage.AdjustRaw);
            mutilator.Mutilate(job);

            //parent should now only have "5.2.1"
            var dtParent = parentTableInfo.Discover(DataAccessContext.InternalDataProcessing).GetDataTable();
            Assert.AreEqual(1, dtParent.Rows.Count);

            //isolation should have 5 ("1.2.3", "2.3.4" and "9.9.9")
            var dtParentIsolation = db.ExpectTable("Parent_Isolation").GetDataTable();
            Assert.AreEqual(5, dtParentIsolation.Rows.Count); //candy and frank should be left 

            //child table should now only have 3 ("1.1.1", "1.1.2" and "1.1.3")
            var dtChild = childTableInfo.Discover(DataAccessContext.InternalDataProcessing).GetDataTable();
            Assert.AreEqual(3, dtChild.Rows.Count);

            //child isolation table should have 4:
            /*
              "1.2.3","1.2.1","Frank"
              "1.2.3","1.2.2","Dave"
              "9.9.9", "1.1.5", "zkk"
              "9.9.9", "1.1.5", "zzb"
             */

            var dtChildIsolation = db.ExpectTable("Child_Isolation").GetDataTable();
            Assert.AreEqual(4, dtChildIsolation.Rows.Count);
        }



        [TestCase(DatabaseType.MicrosoftSQLServer,false)]
        [TestCase(DatabaseType.MySql,false)]
        [TestCase(DatabaseType.MicrosoftSQLServer,true)]
        [TestCase(DatabaseType.MySql,true)]
        public void Test_IsolateTwoTables_MutipleConflictingColumns(DatabaseType dbType,bool whitespace)
        {
            var db = GetCleanedServer(dbType);

            //Create a table in 'RAW' (has no constraints)
            var dt = new DataTable();
            dt.Columns.Add("Pk");
            dt.Columns.Add("OtherCol");

            dt.Rows.Add("A",1); //these are colliding on pk "A" with different values of "OtherCol"
            dt.Rows.Add(whitespace? "A " :"A",2);

            //Create a table in 'RAW' (has no constraints)
            var dt2 = new DataTable();
            dt2.Columns.Add("Pk2");
            dt2.Columns.Add("Fk");
            dt2.Columns.Add("OtherCol2");
            dt2.Columns.Add("OtherCol3");

            dt2.Rows.Add(whitespace ? "X ": "X", "A", "FF",DBNull.Value); //these are colliding on pk "X" with different values of "OtherCol2"
            dt2.Rows.Add("X", whitespace ? "A " :"A", "GG",DBNull.Value);
            dt2.Rows.Add(whitespace ? "X ": "X", "A", "FF","HH"); //these are colliding on pk "X" with different values of "OtherCol2"
            dt2.Rows.Add("X", whitespace ? "A " :"A", "GG","HH");
            
            var tblParent = db.CreateTable("Parent", dt);
            var tblChild = db.CreateTable("Child", dt2);

            //import the table and make A look like a primary key to the metadata layer (and A would be pk in LIVE but not in RAW ofc)
            TableInfo parentTableInfo;
            ColumnInfo[] parentColumnInfosCreated;

            TableInfo childTableInfo;
            ColumnInfo[] childColumnInfosCreated;

            Import(tblParent, out parentTableInfo, out parentColumnInfosCreated);
            Import(tblChild, out childTableInfo, out childColumnInfosCreated);

            //make sure RDMP knows joins start with this table
            parentTableInfo.IsPrimaryExtractionTable = true;
            parentTableInfo.SaveToDatabase();

            //lie about the primary key statuses (to simulate live)
            var seriesInstanceUIdCol =
                parentColumnInfosCreated.Single(c => c.GetRuntimeName().Equals("Pk"));
            seriesInstanceUIdCol.IsPrimaryKey = true;
            seriesInstanceUIdCol.SaveToDatabase();

            var sopInstanceUIdCol = childColumnInfosCreated.Single(c => c.GetRuntimeName().Equals("Pk2"));
            sopInstanceUIdCol.IsPrimaryKey = true;
            sopInstanceUIdCol.SaveToDatabase();

            //Create a new mutilator for these two tables
            var mutilator = GetMutilator(db, parentTableInfo, childTableInfo);

            //tell RDMP about how to join tables
            new JoinInfo(CatalogueRepository,childColumnInfosCreated.Single(
                c => c.GetRuntimeName().Equals("Fk")),
                parentColumnInfosCreated.Single(c => c.GetRuntimeName().Equals("Pk")),
                ExtractionJoinType.Right, null);

            //now that we have a join it should pass checks
            mutilator.Check(new AcceptAllCheckNotifier());

            var config = new HICDatabaseConfiguration(db.Server,new ReturnSameString());
            var job = Mock.Of<IDataLoadJob>(j => j.JobID==999 && j.Configuration == config);            

            mutilator.Initialize(db, LoadStage.AdjustRaw);
            mutilator.Mutilate(job);

            //parent should now be empty
            var dtParent = parentTableInfo.Discover(DataAccessContext.InternalDataProcessing).GetDataTable();
            Assert.AreEqual(0, dtParent.Rows.Count);

            //isolation should have 2
            var dtParentIsolation = db.ExpectTable("Parent_Isolation").GetDataTable();
            Assert.AreEqual(2, dtParentIsolation.Rows.Count); //candy and frank should be left 

            //child table should also be empty
            var dtChild = childTableInfo.Discover(DataAccessContext.InternalDataProcessing).GetDataTable();
            Assert.AreEqual(0, dtChild.Rows.Count);

            //child isolation table should have 4:
            var dtChildIsolation = db.ExpectTable("Child_Isolation").GetDataTable();
            Assert.AreEqual(4, dtChildIsolation.Rows.Count);
        }


        [TestCase(DatabaseType.MicrosoftSQLServer)]
        [TestCase(DatabaseType.MySql)]
        public void Test_IsolateTables_Orphans(DatabaseType dbType)
        {
            var db = GetCleanedServer(dbType);

            //Create a table in 'RAW' (has no constraints)
            var dt = new DataTable();
            dt.Columns.Add("Pk");
            dt.Columns.Add("OtherCol");

            dt.Rows.Add("A",1); //these are colliding on pk "A" with different values of "OtherCol"
            dt.Rows.Add("A",2);

            //Create a table in 'RAW' (has no constraints)
            var dt2 = new DataTable();
            dt2.Columns.Add("Pk2");
            dt2.Columns.Add("Fk");
            dt2.Columns.Add("OtherCol2");
            dt2.Columns.Add("OtherCol3");

            dt2.Rows.Add("X", "B", "FF",DBNull.Value); //these are colliding (on pk 'X') and also orphans (B does not appear in parent table dt)
            dt2.Rows.Add("X", "B", "GG",DBNull.Value);
            
            var tblParent = db.CreateTable("Parent", dt);
            var tblChild = db.CreateTable("Child", dt2);

            //import the table and make A look like a primary key to the metadata layer (and A would be pk in LIVE but not in RAW ofc)
            TableInfo parentTableInfo;
            ColumnInfo[] parentColumnInfosCreated;

            TableInfo childTableInfo;
            ColumnInfo[] childColumnInfosCreated;

            Import(tblParent, out parentTableInfo, out parentColumnInfosCreated);
            Import(tblChild, out childTableInfo, out childColumnInfosCreated);

            //make sure RDMP knows joins start with this table
            parentTableInfo.IsPrimaryExtractionTable = true;
            parentTableInfo.SaveToDatabase();

            //lie about the primary key statuses (to simulate live)
            var seriesInstanceUIdCol =
                parentColumnInfosCreated.Single(c => c.GetRuntimeName().Equals("Pk"));
            seriesInstanceUIdCol.IsPrimaryKey = true;
            seriesInstanceUIdCol.SaveToDatabase();

            var sopInstanceUIdCol = childColumnInfosCreated.Single(c => c.GetRuntimeName().Equals("Pk2"));
            sopInstanceUIdCol.IsPrimaryKey = true;
            sopInstanceUIdCol.SaveToDatabase();

            //Create a new mutilator for these two tables
            var mutilator = GetMutilator(db, parentTableInfo, childTableInfo);

            //tell RDMP about how to join tables
            new JoinInfo(CatalogueRepository,childColumnInfosCreated.Single(
                c => c.GetRuntimeName().Equals("Fk")),
                parentColumnInfosCreated.Single(c => c.GetRuntimeName().Equals("Pk")),
                ExtractionJoinType.Right, null);

            //now that we have a join it should pass checks
            mutilator.Check(new AcceptAllCheckNotifier());

            var config = new HICDatabaseConfiguration(db.Server,new ReturnSameString());
            var job = Mock.Of<IDataLoadJob>(j => j.JobID==999 && j.Configuration == config);            

            mutilator.Initialize(db, LoadStage.AdjustRaw);
            var ex = Assert.Throws<Exception>(()=>mutilator.Mutilate(job));

            Assert.AreEqual("Primary key value not found for X", ex.Message);
        }

        class ReturnSameString : INameDatabasesAndTablesDuringLoads
        {
            public string GetDatabaseName(string rootDatabaseName, LoadBubble convention)
            {
                return rootDatabaseName;
            }

            public string GetName(string tableName, LoadBubble convention)
            {
                return tableName;
            }
        }
    }
}
