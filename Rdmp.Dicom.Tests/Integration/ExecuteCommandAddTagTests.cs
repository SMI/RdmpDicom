using DicomTypeTranslation.TableCreation;
using FAnsi;
using NUnit.Framework;
using Rdmp.Core.CommandExecution.AtomicCommands;
using Rdmp.Core.CommandLine.Interactive;
using Rdmp.Core.Curation;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.DataLoad.Triggers.Implementations;
using Rdmp.Dicom.CommandExecution;
using ReusableLibraryCode.Checks;
using System;
using Tests.Common;

namespace Rdmp.Dicom.Tests.Integration;

class ExecuteCommandAddTagTests : DatabaseTests
{
    [TestCase(DatabaseType.MySql)]
    [TestCase(DatabaseType.MicrosoftSQLServer)]
    public void TestAddTag_WithArchive(DatabaseType type)
    {
        var db = GetCleanedServer(type);

        // Create a nice template with lots of columns
        var template = new ImageTableTemplate
        {
            TableName = "Fish",
            Columns = new[]
            {
                new ImageColumnTemplate
                {
                    IsPrimaryKey = true, AllowNulls = true, ColumnName = "RelativeFileArchiveURI"
                },
                new ImageColumnTemplate
                {
                    IsPrimaryKey = false, AllowNulls = true, ColumnName = "SeriesInstanceUID"
                },
                new ImageColumnTemplate {IsPrimaryKey = false, AllowNulls = true, ColumnName = "StudyDate"}
            }
        };

        // use it to create a table
        var tbl = db.ExpectTable(template.TableName);
        IAtomicCommand cmd = new ExecuteCommandCreateNewImagingDataset(RepositoryLocator,tbl , template);
        Assert.IsFalse(cmd.IsImpossible);
        cmd.Execute();

        Assert.IsTrue(tbl.Exists());

        // import RDMP reference to the table
        var importer = new TableInfoImporter(CatalogueRepository,tbl);
        importer.DoImport(out var ti,out var cols);
            
        var forward = new ForwardEngineerCatalogue(ti,cols);
        forward.ExecuteForwardEngineering(out var catalogue,out _,out _);

        // Create an archive table and backup trigger like we would have if this were the target of a data load
        var triggerImplementerFactory = new TriggerImplementerFactory(type);
        var implementer = triggerImplementerFactory.Create(tbl);
        implementer.CreateTrigger(new ThrowImmediatelyCheckNotifier());

        var archive = tbl.Database.ExpectTable($"{tbl.GetRuntimeName()}_Archive");

        Assert.IsTrue(archive.Exists());

        var activator = new ConsoleInputManager(RepositoryLocator,new ThrowImmediatelyCheckNotifier())
        {
            DisallowInput = true
        };

        // Test the actual commands
        cmd = new ExecuteCommandAddTag(activator,catalogue,"ffffff","int");
        Assert.IsFalse(cmd.IsImpossible,cmd.ReasonCommandImpossible);
        cmd.Execute();

        cmd = new ExecuteCommandAddTag(activator,catalogue,"EchoTime",null);
        Assert.IsFalse(cmd.IsImpossible,cmd.ReasonCommandImpossible);
        cmd.Execute();

        // attempting to add something that is already there is not a problem and just gets skipped
        Assert.DoesNotThrow(()=>new ExecuteCommandAddTag(activator,catalogue,"StudyDate",null).Execute());
            
        cmd = new ExecuteCommandAddTag(activator,catalogue,"SeriesDate",null);
        Assert.IsFalse(cmd.IsImpossible,cmd.ReasonCommandImpossible);
        cmd.Execute();

        Assert.AreEqual("int",tbl.DiscoverColumn("ffffff").DataType.SQLType);
        Assert.AreEqual("decimal(38,19)",tbl.DiscoverColumn("EchoTime").DataType.SQLType);
        Assert.AreEqual(typeof(DateTime),tbl.DiscoverColumn("SeriesDate").DataType.GetCSharpDataType());

        Assert.AreEqual("int",archive.DiscoverColumn("ffffff").DataType.SQLType);
        Assert.AreEqual("decimal(38,19)",archive.DiscoverColumn("EchoTime").DataType.SQLType);
        Assert.AreEqual(typeof(DateTime),archive.DiscoverColumn("SeriesDate").DataType.GetCSharpDataType());

    }
}