using FAnsi;
using FAnsi.Discovery;
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
        [TestCase(DatabaseType.PostgreSql)]
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
            Import(tbl, out var tableInfoCreated, out var columnInfosCreated);

            //lie about the primary key status
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
            var mutilation = new PrimaryKeyCollisionIsolationMutilation {TablesToIsolate = tableInfoCreated};

            //tell the mutilator to set up isolation into the provided database
            var serverPointer = new ExternalDatabaseServer(CatalogueRepository, "Isolation Db",null);
            serverPointer.SetProperties(db);

            mutilation.IsolationDatabase = serverPointer;

            return mutilation;
        }

        [TestCase(DatabaseType.MicrosoftSQLServer)]
        [TestCase(DatabaseType.MySql)]
        [TestCase(DatabaseType.PostgreSql)]
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
            Import(tbl, out var tableInfoCreated, out var columnInfosCreated);
            
            //lie about the primary key status
            var a = columnInfosCreated.Single(c => c.GetRuntimeName().Equals("A"));
            a.IsPrimaryKey = true;
            a.SaveToDatabase();

            var mutilator = GetMutilator(db, tableInfoCreated);
            mutilator.Check(new AcceptAllCheckNotifier());

            var config = new HICDatabaseConfiguration(db.Server,RdmpMockFactory.Mock_INameDatabasesAndTablesDuringLoads(db, "MyCoolTable2"));
            var job = new ThrowImmediatelyDataLoadJob(config,tableInfoCreated);
                                    
            mutilator.Initialize(db,LoadStage.AdjustRaw);
            mutilator.Mutilate(job);

            dt = tbl.GetDataTable();
            Assert.AreEqual(2,dt.Rows.Count); 

            var dtIsolation = tbl.Database.ExpectTable("MyCoolTable2_Isolation").GetDataTable();
            Assert.AreEqual(3, dtIsolation.Rows.Count); 
        }

        [TestCase(DatabaseType.MicrosoftSQLServer)]
        [TestCase(DatabaseType.MySql)]
        [TestCase(DatabaseType.PostgreSql)]
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
            Import(tblParent, out var parentTableInfo, out var parentColumnInfosCreated);
            Import(tblChild, out var childTableInfo, out var childColumnInfosCreated);

            //make sure RDMP knows joins start with this table
            parentTableInfo.IsPrimaryExtractionTable = true;
            parentTableInfo.SaveToDatabase();

            //lie about the primary key statuses
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
            var job = new ThrowImmediatelyDataLoadJob(config,parentTableInfo,childTableInfo);

            mutilator.Initialize(db, LoadStage.AdjustRaw);
            mutilator.Mutilate(job);

            //parent should now only have "5.2.1"
            var dtParent = parentTableInfo.Discover(DataAccessContext.InternalDataProcessing).GetDataTable();
            Assert.AreEqual(1, dtParent.Rows.Count);

            //isolation should have 5 ("1.2.3", "2.3.4" and "9.9.9")
            var dtParentIsolation = db.ExpectTable("Parent_Isolation").GetDataTable();
            Assert.AreEqual(5, dtParentIsolation.Rows.Count); 

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
        [TestCase(DatabaseType.PostgreSql,false)]
        [TestCase(DatabaseType.PostgreSql,true)]
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
            Import(tblParent, out var parentTableInfo, out var parentColumnInfosCreated);
            Import(tblChild, out var childTableInfo, out var childColumnInfosCreated);

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
            var job = new ThrowImmediatelyDataLoadJob(config,parentTableInfo,childTableInfo);

            mutilator.Initialize(db, LoadStage.AdjustRaw);
            mutilator.Mutilate(job);

            //parent should now be empty
            var dtParent = parentTableInfo.Discover(DataAccessContext.InternalDataProcessing).GetDataTable();
            Assert.AreEqual(0, dtParent.Rows.Count);

            //isolation should have 2
            var dtParentIsolation = db.ExpectTable("Parent_Isolation").GetDataTable();
            Assert.AreEqual(2, dtParentIsolation.Rows.Count); 

            //child table should also be empty
            var dtChild = childTableInfo.Discover(DataAccessContext.InternalDataProcessing).GetDataTable();
            Assert.AreEqual(0, dtChild.Rows.Count);

            //child isolation table should have 4:
            var dtChildIsolation = db.ExpectTable("Child_Isolation").GetDataTable();
            Assert.AreEqual(4, dtChildIsolation.Rows.Count);
        }
        
        [TestCase(DatabaseType.MicrosoftSQLServer)]
        [TestCase(DatabaseType.MySql)]
        [TestCase(DatabaseType.PostgreSql)]
        public void Test_IsolateTwoTables_IntKeys(DatabaseType dbType)
        { 
            /***************************************
             *      Parent(Pk)     Child (Pk2,Fk,OtherCol)
             *         4        ->       8,4,1
             *                               ⬍  (collision causes pk 4 to be migrated)
             *                           8,4,2
             *         5        ->       9,5,1  (good record with no collisions anywhere)
             **********************************/
             
            var db = GetCleanedServer(dbType);

            //Create a table in 'RAW' (has no constraints)
            var dt = new DataTable();
            dt.Columns.Add("Pk");
            dt.Columns.Add("OtherCol");

            dt.Rows.Add(4,1); 
            dt.Rows.Add(5,2); 

            //Create a table in 'RAW' (has no constraints)
            var dt2 = new DataTable();
            dt2.Columns.Add("Pk2");
            dt2.Columns.Add("Fk");
            dt2.Columns.Add("OtherCol");

            dt2.Rows.Add(8, 4,1); //these are colliding on pk 8 which will ship full hierarchy of parent pk 4 to the isolation table
            dt2.Rows.Add(8, 4,2);
            dt2.Rows.Add(9, 5,1); //good record with no collisions, should not be deleted!
            
            var tblParent = db.CreateTable("Parent", dt);
            var tblChild = db.CreateTable("Child", dt2);

            //make sure FAnsi made an int column
            Assert.AreEqual(typeof(int),tblParent.DiscoverColumn("Pk").GetGuesser().Guess.CSharpType);

            //import the table and make A look like a primary key to the metadata layer (and A would be pk in LIVE but not in RAW ofc)
            Import(tblParent, out var parentTableInfo, out var parentColumnInfosCreated);
            Import(tblChild, out var childTableInfo, out var childColumnInfosCreated);

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
            var job = new ThrowImmediatelyDataLoadJob(config,parentTableInfo,childTableInfo);

            mutilator.Initialize(db, LoadStage.AdjustRaw);
            mutilator.Mutilate(job);

            //parent should now have 1
            var dtParent = parentTableInfo.Discover(DataAccessContext.InternalDataProcessing).GetDataTable();
            Assert.AreEqual(1, dtParent.Rows.Count);

            //isolation should have 1
            var dtParentIsolation = db.ExpectTable("Parent_Isolation").GetDataTable();
            Assert.AreEqual(1, dtParentIsolation.Rows.Count); 

            //child table should have the good 1
            var dtChild = childTableInfo.Discover(DataAccessContext.InternalDataProcessing).GetDataTable();
            Assert.AreEqual(1, dtChild.Rows.Count);

            //child isolation table should have 2:
            var dtChildIsolation = db.ExpectTable("Child_Isolation").GetDataTable();
            Assert.AreEqual(2, dtChildIsolation.Rows.Count);
        }

        [TestCase(DatabaseType.MicrosoftSQLServer)]
        [TestCase(DatabaseType.MySql)]
        [TestCase(DatabaseType.PostgreSql)]
        public void Test_IsolateTwoTables_MultipleCollidingChildren(DatabaseType dbType)
        { 
            /***************************************
             *      Parent(Pk)     Child (Pk2,Fk,OtherCol)
             *         A        ->       X,A,1
             *                               ⬍  (collision causes A delete)
             *                           X,A,2
             *                  ->       Y,A,1
             *                               ⬍  (collision causes repeated attempt to delete A)
             *                           Y,A,2
             *
             **********************************/
             
            var db = GetCleanedServer(dbType);

            //Create a table in 'RAW' (has no constraints)
            var dt = new DataTable();
            dt.Columns.Add("Pk");
            dt.Columns.Add("OtherCol");

            dt.Rows.Add("A",1); 

            //Create a table in 'RAW' (has no constraints)
            var dt2 = new DataTable();
            dt2.Columns.Add("Pk2");
            dt2.Columns.Add("Fk");
            dt2.Columns.Add("OtherCol");

            dt2.Rows.Add("X", "A",1); //these are colliding on pk "X" which will ship A to the isolation table
            dt2.Rows.Add("X", "A",2);
            dt2.Rows.Add("Y", "A",2); //these are colliding on pk "Y" but also reference A (which has already been shipped to isolation)
            dt2.Rows.Add("Y", "A",1);
            
            var tblParent = db.CreateTable("Parent", dt);
            var tblChild = db.CreateTable("Child", dt2);

            //import the table and make A look like a primary key to the metadata layer (and A would be pk in LIVE but not in RAW ofc)
            Import(tblParent, out var parentTableInfo, out var parentColumnInfosCreated);
            Import(tblChild, out var childTableInfo, out var childColumnInfosCreated);

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
            var job = new ThrowImmediatelyDataLoadJob(config,parentTableInfo,childTableInfo);

            mutilator.Initialize(db, LoadStage.AdjustRaw);
            mutilator.Mutilate(job);

            //parent should now have 0...
            var dtParent = parentTableInfo.Discover(DataAccessContext.InternalDataProcessing).GetDataTable();
            Assert.AreEqual(0, dtParent.Rows.Count);

            //isolation should have 1
            var dtParentIsolation = db.ExpectTable("Parent_Isolation").GetDataTable();
            Assert.AreEqual(1, dtParentIsolation.Rows.Count); 

            //child table should also be empty
            var dtChild = childTableInfo.Discover(DataAccessContext.InternalDataProcessing).GetDataTable();
            Assert.AreEqual(0, dtChild.Rows.Count);

            //child isolation table should have 4:
            var dtChildIsolation = db.ExpectTable("Child_Isolation").GetDataTable();
            Assert.AreEqual(4, dtChildIsolation.Rows.Count);
        }
        [TestCase(DatabaseType.MicrosoftSQLServer)]
        [TestCase(DatabaseType.MySql)]
        [TestCase(DatabaseType.PostgreSql)]
        public void Test_IsolateTables_Orphans(DatabaseType dbType)
        {
            var db = GetCleanedServer(dbType);

            /***************************************
             *      Parent(Pk)     Child (Pk2,Fk,OtherCol)
             *         A        <>       X,B,FF
             *                               ⬍  (collision on pk X causes lookup of Fk B which does not exist in Parent)
             *                           X,B,GG
             *
             **********************************/

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
            Import(tblParent, out var parentTableInfo, out var parentColumnInfosCreated);
            Import(tblChild, out var childTableInfo, out var childColumnInfosCreated);

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
            var job = new ThrowImmediatelyDataLoadJob(config,parentTableInfo,childTableInfo);

            mutilator.Initialize(db, LoadStage.AdjustRaw);
            var ex = Assert.Throws<Exception>(()=>mutilator.Mutilate(job));

            Assert.AreEqual("Primary key value not found for X", ex.Message);
        }

        [TestCase(DatabaseType.MicrosoftSQLServer)]
        [TestCase(DatabaseType.MySql)]
        [TestCase(DatabaseType.PostgreSql)]
        public void Test_IsolateTables_NullForeignKey(DatabaseType dbType)
        {
            var db = GetCleanedServer(dbType);

            /***************************************
             *      Parent(Pk)     Child (Pk2,Fk,OtherCol)
             *         A        <>       X,A,FF
             *                               ⬍  (collision on pk X causes lookup of A but then we throw because the second record has no Fk listed)
             *                           X,NULL,GG
             *
             **********************************/

            //Create a table in 'RAW' (has no constraints)
            var dt = new DataTable();
            dt.Columns.Add("Pk");
            dt.Columns.Add("OtherCol");

            dt.Rows.Add("A",1); //these are colliding on pk "A" with different values of "OtherCol"
            
            //Create a table in 'RAW' (has no constraints)
            var dt2 = new DataTable();
            dt2.Columns.Add("Pk2");
            dt2.Columns.Add("Fk");
            dt2.Columns.Add("OtherCol2");
            dt2.Columns.Add("OtherCol3");

            dt2.Rows.Add("X", "A", "FF",DBNull.Value); //these are colliding (on pk 'X').  "A" exists but the null value in the other record is a problem
            dt2.Rows.Add("X", DBNull.Value, "GG",DBNull.Value);
            
            var tblParent = db.CreateTable("Parent", dt);
            var tblChild = db.CreateTable("Child", dt2);

            //import the table and make A look like a primary key to the metadata layer (and A would be pk in LIVE but not in RAW ofc)
            Import(tblParent, out var parentTableInfo, out var parentColumnInfosCreated);
            Import(tblChild, out var childTableInfo, out var childColumnInfosCreated);

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
            var job = new ThrowImmediatelyDataLoadJob(config,parentTableInfo,childTableInfo);
            
            mutilator.Initialize(db, LoadStage.AdjustRaw);
            mutilator.Mutilate(job);
            
            //parent should now have 0...
            var dtParent = parentTableInfo.Discover(DataAccessContext.InternalDataProcessing).GetDataTable();
            Assert.AreEqual(0, dtParent.Rows.Count);

            //isolation should have 1 (A)
            var dtParentIsolation = db.ExpectTable("Parent_Isolation").GetDataTable();
            Assert.AreEqual(1, dtParentIsolation.Rows.Count);
            AssertContains(dtParentIsolation,"A",true,0);

            //child table should have the null record only
            var dtChild = childTableInfo.Discover(DataAccessContext.InternalDataProcessing).GetDataTable();
            Assert.AreEqual(1, dtChild.Rows.Count);
            AssertContains(dtChild,"X",DBNull.Value,"GG",DBNull.Value);

            //child isolation table should have 1 record (the X,A,FF)
            var dtChildIsolation = db.ExpectTable("Child_Isolation").GetDataTable();
            Assert.AreEqual(1, dtChildIsolation.Rows.Count);
            AssertContains(dtChildIsolation,"X","A","FF",DBNull.Value,0);

        }

        private void AssertContains(DataTable dt, params object[] rowValues)
        {
            Assert.IsTrue(dt.Rows.Cast<DataRow>().Any(r=>
                    rowValues.All(v=>r.ItemArray.Contains(v))),"Did not find expected row " + string.Join("," , rowValues)
                                                          + Environment.NewLine + "Rows seen were:" + 
                                                          string.Join(Environment.NewLine,
                                                              dt.Rows.Cast<DataRow>().Select(r=>string.Join(",",r.ItemArray))));
        }

        [TestCase(DatabaseType.MicrosoftSQLServer)]
        [TestCase(DatabaseType.MySql)]
        [TestCase(DatabaseType.PostgreSql)]
        public void Test_IsolateTables_AmbiguousFk(DatabaseType dbType)
        {
            var db = GetCleanedServer(dbType);

            /***************************************
             *      Parent(Pk)     Child (Pk2,Fk,OtherCol)
             *         A        ->       X,A,FF
             *                               ⬍  (collision on pk X is a big problem because they list different fks!)
             *         B        ->       X,B,GG
             *                           Y,B,AA (good record, if we have to isolate both A and B we had better isolate this too)
             **********************************/

            //Create a table in 'RAW' (has no constraints)
            var dt = new DataTable();
            dt.Columns.Add("Pk");
            dt.Columns.Add("OtherCol");

            dt.Rows.Add("A",1);
            dt.Rows.Add("B",2);

            //Create a table in 'RAW' (has no constraints)
            var dt2 = new DataTable();
            dt2.Columns.Add("Pk2");
            dt2.Columns.Add("Fk");
            dt2.Columns.Add("OtherCol2");
            dt2.Columns.Add("OtherCol3");

            dt2.Rows.Add("X", "A", "FF",DBNull.Value); //these are colliding (on pk 'X') but list two different (but existing) pks!
            dt2.Rows.Add("X", "B", "GG",DBNull.Value);
            dt2.Rows.Add("Y", "B", "AA",DBNull.Value); //good record but has to be isolated because it is child of B which is involved in the above collision
            
            var tblParent = db.CreateTable("Parent", dt);
            var tblChild = db.CreateTable("Child", dt2);

            //import the table and make A look like a primary key to the metadata layer (and A would be pk in LIVE but not in RAW ofc)
            Import(tblParent, out var parentTableInfo, out var parentColumnInfosCreated);
            Import(tblChild, out var childTableInfo, out var childColumnInfosCreated);

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
            var job = new ThrowImmediatelyDataLoadJob(config,parentTableInfo,childTableInfo);

            mutilator.Initialize(db, LoadStage.AdjustRaw);
            mutilator.Mutilate(job);

            
            //parent should now have 0...
            var dtParent = parentTableInfo.Discover(DataAccessContext.InternalDataProcessing).GetDataTable();
            Assert.AreEqual(0, dtParent.Rows.Count);

            //isolation should have 2 (A and B)
            var dtParentIsolation = db.ExpectTable("Parent_Isolation").GetDataTable();
            Assert.AreEqual(2, dtParentIsolation.Rows.Count); 

            //child table should also be empty
            var dtChild = childTableInfo.Discover(DataAccessContext.InternalDataProcessing).GetDataTable();
            Assert.AreEqual(0, dtChild.Rows.Count);

            //child isolation table should have 3 (both bad records and the good record that would otherwise be an orphan in live)
            var dtChildIsolation = db.ExpectTable("Child_Isolation").GetDataTable();
            Assert.AreEqual(3, dtChildIsolation.Rows.Count);

        }

        [TestCase(DatabaseType.MicrosoftSQLServer)]
        [TestCase(DatabaseType.MySql)]
        [TestCase(DatabaseType.PostgreSql)]
        public void Test_IsolateTables_NoRecordsLeftBehind(DatabaseType dbType)
        {
            var db = GetCleanedServer(dbType);

            /***************************************
             *      Parent(Pk)     Child (Pk2,Fk,OtherCol)
             *         A        ->       X,A,FF
             *                               ⬍  (collision on Pk X means we migrate A but we must make sure to ship Y too or it will be an orphan)
             *                           X,A,GG
             *                           Y,A,HH ('good' record but must be isolated because referenced foreign key A is going away).
             *
             **********************************/

            //Create a table in 'RAW' (has no constraints)
            var dt = new DataTable();
            dt.Columns.Add("Pk");
            dt.Columns.Add("OtherCol");

            dt.Rows.Add("A",1);

            //Create a table in 'RAW' (has no constraints)
            var dt2 = new DataTable();
            dt2.Columns.Add("Pk2");
            dt2.Columns.Add("Fk");
            dt2.Columns.Add("OtherCol2");
            dt2.Columns.Add("OtherCol3");

            dt2.Rows.Add("X", "A", "FF",DBNull.Value); //these are colliding (on pk 'X')
            dt2.Rows.Add("X", "A", "GG",DBNull.Value);
            dt2.Rows.Add("Y", "A", "HH",DBNull.Value); //must not be left behind
            
            var tblParent = db.CreateTable("Parent", dt);
            var tblChild = db.CreateTable("Child", dt2);

            //import the table and make A look like a primary key to the metadata layer (and A would be pk in LIVE but not in RAW ofc)
            Import(tblParent, out var parentTableInfo, out var parentColumnInfosCreated);
            Import(tblChild, out var childTableInfo, out var childColumnInfosCreated);

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
            var job = new ThrowImmediatelyDataLoadJob(config,parentTableInfo,childTableInfo);

            mutilator.Initialize(db, LoadStage.AdjustRaw);
            Assert.DoesNotThrow(()=>mutilator.Mutilate(job));

            //parent should now have 0...
            var dtParent = parentTableInfo.Discover(DataAccessContext.InternalDataProcessing).GetDataTable();
            Assert.AreEqual(0, dtParent.Rows.Count);

            //isolation should have 1
            var dtParentIsolation = db.ExpectTable("Parent_Isolation").GetDataTable();
            Assert.AreEqual(1, dtParentIsolation.Rows.Count); 

            //child table should also be empty
            var dtChild = childTableInfo.Discover(DataAccessContext.InternalDataProcessing).GetDataTable();
            Assert.AreEqual(0, dtChild.Rows.Count);

            //child isolation table should have 3 (both bad records and the good record that would otherwise be an orphan in live)
            var dtChildIsolation = db.ExpectTable("Child_Isolation").GetDataTable();
            Assert.AreEqual(3, dtChildIsolation.Rows.Count);

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
