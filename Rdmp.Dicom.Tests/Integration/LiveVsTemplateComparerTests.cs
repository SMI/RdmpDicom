using DicomTypeTranslation.TableCreation;
using FAnsi;
using NUnit.Framework;
using Rdmp.Core.Curation;
using Rdmp.Dicom.CommandExecution;
using Tests.Common;

namespace Rdmp.Dicom.Tests.Integration;

class LiveVsTemplateComparerTests:DatabaseTests
{

    [TestCase(DatabaseType.MySql)]
    [TestCase(DatabaseType.MicrosoftSQLServer)]
    public void TestImageTemplates(DatabaseType type)
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
                new ImageColumnTemplate {IsPrimaryKey = false, AllowNulls = true, ColumnName = "StudyDate"},
                new ImageColumnTemplate
                {
                    IsPrimaryKey = false, AllowNulls = true, ColumnName = "StudyInstanceUID"
                },
                new ImageColumnTemplate
                {
                    IsPrimaryKey = false, AllowNulls = true, ColumnName = "StudyDescription"
                },
                new ImageColumnTemplate {IsPrimaryKey = false, AllowNulls = true, ColumnName = "EchoTime"},
                new ImageColumnTemplate
                {
                    IsPrimaryKey = false, AllowNulls = true, ColumnName = "RepetitionTime"
                },
                new ImageColumnTemplate
                {
                    IsPrimaryKey = false, AllowNulls = true, ColumnName = "PatientAge"
                }
            }
        };

        // use it to create a table
        var tbl = db.ExpectTable(template.TableName);
        var cmd = new ExecuteCommandCreateNewImagingDataset(RepositoryLocator,tbl , template);
        Assert.IsFalse(cmd.IsImpossible);
        cmd.Execute();

        Assert.IsTrue(tbl.Exists());

        // import RDMP reference to the table
        var importer = new TableInfoImporter(CatalogueRepository,tbl);
        importer.DoImport(out var ti,out _);

        // compare the live with the template
        var comparer = new LiveVsTemplateComparer(ti,new() { DatabaseType = type,Tables = new() { template } });

        // should be no differences
        Assert.AreEqual(comparer.TemplateSql,comparer.LiveSql);

        // make a difference
        tbl.DropColumn(tbl.DiscoverColumn("EchoTime"));
               
        //now comparer should see a difference
        comparer = new(ti,new() { DatabaseType = type,Tables = new() { template } });
        Assert.AreNotEqual(comparer.TemplateSql,comparer.LiveSql);

        tbl.Drop();
    }
}