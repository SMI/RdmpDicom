using DicomTypeTranslation.TableCreation;
using FAnsi;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.ReusableLibraryCode.DataAccess;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Rdmp.Dicom;

/// <summary>
/// Compares differences between an imaging template and a live data table on a server (what columns have been renamed, resized etc).
/// </summary>
public class LiveVsTemplateComparer
{
    public string TemplateSql {get;}
    public string LiveSql  {get;}

    public LiveVsTemplateComparer(ITableInfo table,ImageTableTemplateCollection templateCollection)
    {
        // locate the live table and script it as it stands today
        var discoveredTable = table.Discover(DataAccessContext.InternalDataProcessing);
        LiveSql = discoveredTable.ScriptTableCreation(false,false,false);

        LiveSql = TailorLiveSql(LiveSql,discoveredTable.Database.Server.DatabaseType);

        // The live table name e.g. CT_StudyTable
        var liveTableName = discoveredTable.GetRuntimeName();
        // Without the prefix e.g. StudyTable
        var liveTableNameWithoutPrefix = liveTableName.Substring(liveTableName.IndexOf("_", StringComparison.Ordinal)+1);

        var template = templateCollection.Tables.FirstOrDefault(
            c=>c.TableName.Equals(liveTableName,StringComparison.CurrentCultureIgnoreCase) ||
               c.TableName.Equals(liveTableNameWithoutPrefix,StringComparison.CurrentCultureIgnoreCase));

        if(template == null)
            throw new($"Could not find a Template called '{liveTableName}' or '{liveTableNameWithoutPrefix}'.  Templates in file were {string.Join(",",templateCollection.Tables.Select(t=>t.TableName))}");

        //script the template
        var creator = new ImagingTableCreation(discoveredTable.Database.Server.GetQuerySyntaxHelper());            
        TemplateSql = creator.GetCreateTableSql(discoveredTable.Database,liveTableName,template, discoveredTable.Schema);

        TemplateSql  = TailorTemplateSql(TemplateSql );
    }
    private string TailorTemplateSql(string templateSql)
    {
        //condense all multiple spaces to single spaces
        templateSql = Regex.Replace(templateSql,"  +"," ");
            
        return templateSql;
    }

    private string TailorLiveSql(string liveSql, DatabaseType databaseType)
    {
        // get rid of collation
        liveSql = Regex.Replace(liveSql,"\\bCOLLATE \\w+","");
            
        // condense all multiple spaces to single spaces
        liveSql = Regex.Replace(liveSql,"  +"," ");
            
        return liveSql;
    }
}