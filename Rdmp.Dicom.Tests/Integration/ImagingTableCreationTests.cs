using DicomTypeTranslation.TableCreation;
using FAnsi;
using NUnit.Framework;
using Rdmp.Dicom.CommandExecution;
using System;
using System.Linq;
using Tests.Common;

namespace Rdmp.Dicom.Tests.Integration
{
    public class ImagingTableCreationTests:DatabaseTests
    {

        [TestCase(DatabaseType.MySql)]
        [TestCase(DatabaseType.MicrosoftSQLServer)]
        public void TestImageTemplates(DatabaseType type)
        {
            var db = GetCleanedServer(type);

            var template = new ImageTableTemplate();
            template.TableName = "Fish";
            template.Columns = new[]
            {
                new ImageColumnTemplate {IsPrimaryKey = true, AllowNulls = true,ColumnName = "RelativeFileArchiveURI"},
                new ImageColumnTemplate {IsPrimaryKey = false,AllowNulls = true, ColumnName = "SeriesInstanceUID"}
            };
            var tbl = db.ExpectTable(template.TableName);
            var cmd = new ExecuteCommandCreateNewImagingDataset(RepositoryLocator,tbl , template);
            Assert.IsFalse(cmd.IsImpossible);
            cmd.Execute();

            Assert.IsTrue(tbl.Exists());

            var cols = tbl.DiscoverColumns();
            Assert.AreEqual(2,cols.Length);

            var rfa = cols.Single(c => c.GetRuntimeName().Equals("RelativeFileArchiveURI"));

            Assert.IsTrue(rfa.IsPrimaryKey);
            Assert.IsFalse(rfa.AllowNulls); //because PK!


            var sid = cols.Single(c => c.GetRuntimeName().Equals("SeriesInstanceUID"));

            Assert.IsFalse(sid.IsPrimaryKey);
            Assert.IsTrue(sid.AllowNulls); 



        }
    }
}
