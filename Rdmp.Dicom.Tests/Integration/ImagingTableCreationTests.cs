using DicomTypeTranslation.TableCreation;
using FAnsi;
using NUnit.Framework;
using Rdmp.Dicom.CommandExecution;
using System.Linq;
using Tests.Common;

namespace Rdmp.Dicom.Tests.Integration;

public class ImagingTableCreationTests : DatabaseTests
{

    [TestCase(DatabaseType.MySql)]
    [TestCase(DatabaseType.MicrosoftSQLServer)]
    public void TestImageTemplates(DatabaseType type)
    {
        var db = GetCleanedServer(type);

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
                }
            }
        };
        var tbl = db.ExpectTable(template.TableName);
        var cmd = new ExecuteCommandCreateNewImagingDataset(RepositoryLocator, tbl, template);
        Assert.That(cmd.IsImpossible, Is.False);
        cmd.Execute();

        Assert.That(tbl.Exists());

        var cols = tbl.DiscoverColumns();
        Assert.That(cols, Has.Length.EqualTo(2));

        var rfa = cols.Single(c => c.GetRuntimeName().Equals("RelativeFileArchiveURI"));

        Assert.Multiple(() =>
        {
            Assert.That(rfa.IsPrimaryKey);
            Assert.That(rfa.AllowNulls, Is.False); //because PK!
        });


        var sid = cols.Single(c => c.GetRuntimeName().Equals("SeriesInstanceUID"));

        Assert.Multiple(() =>
        {
            Assert.That(sid.IsPrimaryKey, Is.False);
            Assert.That(sid.AllowNulls);
        });



    }
}