using DicomTypeTranslation.TableCreation;
using FAnsi;
using NUnit.Framework;
using Rdmp.Core.CommandExecution.AtomicCommands;
using Rdmp.Core.Curation;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.DataLoad.Triggers.Implementations;
using Rdmp.Dicom.CommandExecution;
using ReusableLibraryCode.Checks;
using System;
using System.Collections.Generic;
using System.Text;
using Tests.Common;

namespace Rdmp.Dicom.Tests.Integration
{
    class ExecuteCommandAddTagTests : DatabaseTests
    {
        [TestCase(DatabaseType.MySql)]
        [TestCase(DatabaseType.MicrosoftSQLServer)]
        public void TestImageTemplates(DatabaseType type)
        {
            var db = GetCleanedServer(type);

            // Create a nice template with lots of columns
            var template = new ImageTableTemplate();
            template.TableName = "Fish";
            template.Columns = new[]
            {
                new ImageColumnTemplate {IsPrimaryKey = true, AllowNulls = true,ColumnName = "RelativeFileArchiveURI"},
                new ImageColumnTemplate {IsPrimaryKey = false,AllowNulls = true, ColumnName = "SeriesInstanceUID"},
                new ImageColumnTemplate {IsPrimaryKey = false,AllowNulls = true, ColumnName = "StudyDate"},
            };

            // use it to create a table
            var tbl = db.ExpectTable(template.TableName);
            IAtomicCommand cmd = new ExecuteCommandCreateNewImagingDataset(RepositoryLocator,tbl , template);
            Assert.IsFalse(cmd.IsImpossible);
            cmd.Execute();

            Assert.IsTrue(tbl.Exists());

            // import RDMP reference to the table
            var importer = new TableInfoImporter(CatalogueRepository,tbl);
            importer.DoImport(out TableInfo ti,out ColumnInfo[] cols);
            
            var forward = new ForwardEngineerCatalogue(ti,cols);
            forward.ExecuteForwardEngineering(out Catalogue catalogue,out _,out _);

            // Create an archive table and backup trigger like we would have if this were the target of a data load
            var triggerImplementerFactory = new TriggerImplementerFactory(type);
            var implementer = triggerImplementerFactory.Create(tbl);
            implementer.CreateTrigger(new ThrowImmediatelyCheckNotifier());

            var archive = tbl.Database.ExpectTable(tbl.GetRuntimeName() + "_Archive");

            Assert.IsTrue(archive.Exists());

            // Test the actual commands
            cmd = new ExecuteCommandAddTag(null,catalogue,"ffffff","int");
            Assert.IsFalse(cmd.IsImpossible,cmd.ReasonCommandImpossible);
            cmd.Execute();

            cmd = new ExecuteCommandAddTag(null,catalogue,"EchoTime",null);
            Assert.IsFalse(cmd.IsImpossible,cmd.ReasonCommandImpossible);
            cmd.Execute();

            cmd = new ExecuteCommandAddTag(null,catalogue,"StudyDate",null);
            Assert.IsFalse(cmd.IsImpossible,cmd.ReasonCommandImpossible);
            cmd.Execute();

            Assert.AreEqual("int",tbl.DiscoverColumn("ffffff").DataType.SQLType);
            Assert.AreEqual("int",tbl.DiscoverColumn("EchoTime").DataType.SQLType);
            Assert.AreEqual(typeof(DateTime),tbl.DiscoverColumn("StudyDate").DataType.GetCSharpDataType());

            Assert.AreEqual("int",archive.DiscoverColumn("ffffff").DataType.SQLType);
            Assert.AreEqual("int",archive.DiscoverColumn("EchoTime").DataType.SQLType);
            Assert.AreEqual(typeof(DateTime),archive.DiscoverColumn("StudyDate").DataType.GetCSharpDataType());

        }
    }
}
