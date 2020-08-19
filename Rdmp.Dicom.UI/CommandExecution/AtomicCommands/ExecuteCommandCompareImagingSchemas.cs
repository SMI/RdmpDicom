using DicomTypeTranslation.TableCreation;
using FAnsi;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Icons.IconProvision;
using Rdmp.UI.CommandExecution.AtomicCommands;
using Rdmp.UI.ItemActivation;
using Rdmp.UI.SimpleDialogs.SqlDialogs;
using ReusableLibraryCode.DataAccess;
using ReusableLibraryCode.Icons.IconProvision;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Rdmp.Dicom.UI.CommandExecution.AtomicCommands
{
    /// <summary>
    /// Shows the differences made to a table after creation from a given imaging template
    /// </summary>
    class ExecuteCommandCompareImagingSchemas : BasicUICommandExecution
    {
        private readonly ITableInfo _tableInfo;

        public ExecuteCommandCompareImagingSchemas(IActivateItems activator, Catalogue c):base(activator)
        {
            var tis = c.GetTableInfosIdeallyJustFromMainTables();

            if(tis.Length != 1)
                SetImpossible($"Catalogue has {tis.Length} underlying TableInfos");
            else
                _tableInfo = tis[0];
        }

        public override Image GetImage(IIconProvider iconProvider)
        {
            return iconProvider.GetImage(RDMPConcept.Diff);
        }
        public override void Execute()
        {
            base.Execute();

            // locate the live table and script it as it stands today
            var discoveredTable = _tableInfo.Discover(DataAccessContext.InternalDataProcessing);
            var liveSql = discoveredTable.ScriptTableCreation(false,false,false);

            liveSql = TailorLiveSql(liveSql,discoveredTable.Database.Server.DatabaseType);

            // The live table name e.g. CT_StudyTable
            var liveTableName = discoveredTable.GetRuntimeName();
            // Without the prefix e.g. StudyTable
            var liveTableNameWithoutPrefix = liveTableName.Substring(liveTableName.IndexOf("_")+1);

            // Get the template for diff
            var file = Activator.SelectFile("Template File","Imaging Template","*.it");

            if(file == null)
                return;

            var templateCollection = ImageTableTemplateCollection.LoadFrom(File.ReadAllText(file.FullName));

            var template = templateCollection.Tables.FirstOrDefault(
                c=>c.TableName.Equals(liveTableName,StringComparison.CurrentCultureIgnoreCase) ||
                c.TableName.Equals(liveTableNameWithoutPrefix,StringComparison.CurrentCultureIgnoreCase));

            if(template == null)
                throw new Exception($"Could not find a Template called '{liveTableName}' or '{liveTableNameWithoutPrefix}' in file '{file.FullName}'.  Templates in file were {string.Join(",",templateCollection.Tables.Select(t=>t.TableName))}");

            //script the template
            var creator = new ImagingTableCreation(discoveredTable.Database.Server.GetQuerySyntaxHelper());            
            var templateSql = creator.GetCreateTableSql(discoveredTable.Database,liveTableName,template, discoveredTable.Schema);

            templateSql = TailorTemplateSql(templateSql);

            var viewer = new SQLBeforeAndAfterViewer(liveSql,templateSql,"Your Database", "Template","Differences between live table and image template",MessageBoxButtons.OK);
            viewer.Show();
        }

        private string TailorTemplateSql(string templateSql)
        {
            //condense all multiple spaces to single spaces
            templateSql = Regex.Replace(templateSql,"  +"," ");
            
            return templateSql;
        }

        private string TailorLiveSql(string liveSql, DatabaseType databaseType)
        {
            if(databaseType == DatabaseType.MicrosoftSQLServer)
            {
                liveSql = Regex.Replace(liveSql,"COLLATE \\w+","");
            }

            //condense all multiple spaces to single spaces
            liveSql = Regex.Replace(liveSql,"  +"," ");
            
            return liveSql;
        }
    }
}
