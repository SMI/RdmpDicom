using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using FAnsi;
using FAnsi.Discovery;
using FAnsi.Discovery.QuerySyntax;
using MapsDirectlyToDatabaseTable;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.DataLoad;
using Rdmp.Core.Curation.Data.EntityNaming;
using Rdmp.Core.DataLoad;
using Rdmp.Core.DataLoad.Engine.DatabaseManagement.EntityNaming;
using Rdmp.Core.DataLoad.Engine.DatabaseManagement.Operations;
using Rdmp.Core.DataLoad.Engine.Job;
using Rdmp.Core.DataLoad.Engine.Mutilators;
using Rdmp.Core.DataLoad.Triggers;
using Rdmp.Core.QueryBuilding;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.DataAccess;
using ReusableLibraryCode.Progress;
using TypeGuesser;

namespace Rdmp.Dicom.PipelineComponents;

public class PrimaryKeyCollisionIsolationMutilation:IPluginMutilateDataTables
{
    [DemandsInitialization("All tables which participate in record isolation e.g. Study,Series, Image.  These tables must have valid JoinInfos configured and one must be marked TableInfo.IsPrimaryExtractionTable",Mandatory=true)]
    public TableInfo[] TablesToIsolate { get; set; }
        
    [DemandsInitialization("Database in which to put _Isolation tables.",Mandatory=true)]
    public ExternalDatabaseServer IsolationDatabase { get; set; }

    [DemandsInitialization("Timeout for each individual sql command, measured in seconds",Mandatory=true, DefaultValue = 30)]
    public int TimeoutInSeconds {get;set;}
        
    private List<JoinInfo> _joins;
    private DiscoveredDatabase _raw;
    private IQuerySyntaxHelper _syntaxHelper;
    private int _dataLoadInfoId;
    private QueryBuilder _qb;

    private TableInfo _primaryTable;
    private ColumnInfo _primaryTablePk;
    private string _fromSql;
    private INameDatabasesAndTablesDuringLoads _namer;
    private IDataLoadJob _job;

    public void Check(ICheckNotifier notifier)
    {
        //if there is only one or no tables that's fine (mandatory will check for null itself)
        if (TablesToIsolate == null)
            throw new("No tables have been selected");
             
        //make sure there is only one primary key per table and that it's a string
        foreach (var t in TablesToIsolate)
        {
            if (t.ColumnInfos.Count(c => c.IsPrimaryKey) != 1)
                throw new($"Table '{t}' did not have exactly 1 IsPrimaryKey column");
        }

        //if there are multiple tables then we must know how to join them
        if (TablesToIsolate.Length >1 && TablesToIsolate.Count(t => t.IsPrimaryExtractionTable) != 1)
        {
            var primaryTables = TablesToIsolate.Where(t => t.IsPrimaryExtractionTable).ToArray();
                
            notifier.OnCheckPerformed(
                new(
                    $"There are {TablesToIsolate.Length} tables to operate on but {primaryTables.Length} are marked IsPrimaryExtractionTable ({string.Join(",",primaryTables.Select(t=>t.Name))}).  This should be set on a single top level table only e.g. Study",
                    CheckResult.Fail));
        }

        try
        {
            //if there are multiple tables we need to know how to join them on a 1 column to 1 basis
            BuildJoinOrder(true);
        }
        catch (Exception e)
        {
            notifier.OnCheckPerformed(new("Failed to build join order", CheckResult.Fail, e));
            return;
        }

        //This is where we put the duplicate records
        var db = IsolationDatabase.Discover(DataAccessContext.DataLoad);

        if(!db.Exists())
            throw new("IsolationDatabase did not exist");

        //Make sure the isolation tables exist and the schema matches RAW
        foreach (var tableInfo in TablesToIsolate)
        {
            var table = db.ExpectTable(GetIsolationTableName(tableInfo));

            if (!table.Exists())
            {
                var fix = notifier.OnCheckPerformed(
                    new($"Isolation table '{table.GetFullyQualifiedName()}' did not exist",
                        CheckResult.Fail, null, "Create isolation table?"));

                if (fix)
                    CreateIsolationTable(table, tableInfo);
                else
                    throw new("User rejected change");
            }
            else
                ValidateIsolationTableSchema(table,tableInfo,notifier);
        }
    }

    public static string GetIsolationTableName(TableInfo tableInfo)
    {
        return $"{tableInfo.GetRuntimeName(LoadBubble.Live)}_Isolation";
    }

    private void ValidateIsolationTableSchema(DiscoveredTable toValidate, TableInfo tableInfo, ICheckNotifier notifier)
    {
        var expected = tableInfo.GetColumnsAtStage(LoadStage.AdjustRaw).Select(c=>c.GetRuntimeName(LoadStage.AdjustRaw)).Union(new []{SpecialFieldNames.DataLoadRunID}).ToArray();
        var found = toValidate.DiscoverColumns().Select(c=>c.GetRuntimeName()).ToArray();

        foreach (var missingFromIsolation in expected.Except(found, StringComparer.CurrentCultureIgnoreCase))
            notifier.OnCheckPerformed(
                new(
                    $"Isolation table '{toValidate}' did not contain expected column'{missingFromIsolation}'", CheckResult.Fail));

        foreach (var unexpectedInIsolation in found.Except(expected, StringComparer.CurrentCultureIgnoreCase))
            notifier.OnCheckPerformed(
                new(
                    $"Isolation table '{toValidate}' contained an unexpected column'{unexpectedInIsolation}'", CheckResult.Fail));
    }

    private void CreateIsolationTable(DiscoveredTable toCreate, TableInfo tableInfo)
    {
        var from = tableInfo.Discover(DataAccessContext.DataLoad);

        //create a RAW table schema called TableName_Isolation
        var cloner = new TableInfoCloneOperation(new(toCreate.Database.Server),tableInfo,LoadBubble.Live,_job ?? (IDataLoadEventListener)new ThrowImmediatelyDataLoadEventListener());
        cloner.CloneTable(from.Database, toCreate.Database, from, toCreate.GetRuntimeName(), true, true, true, tableInfo.PreLoadDiscardedColumns);
            
        if(!toCreate.Exists())
            throw new($"Table '{toCreate}' did not exist after issuing create command");

        //Add the data load run id
        toCreate.AddColumn(SpecialFieldNames.DataLoadRunID,new DatabaseTypeRequest(typeof(int)),false,10);
    }

    private void BuildJoinOrder(bool isChecks)
    {
        _qb = new(null, null);

        var memory = new MemoryRepository();

        foreach (var t in TablesToIsolate)
            _qb.AddColumn(new ColumnInfoToIColumn(memory,t.ColumnInfos.First()));

        _primaryTable = TablesToIsolate.Length == 1 ? TablesToIsolate[0] : TablesToIsolate.Single(t => t.IsPrimaryExtractionTable);
        _primaryTablePk = _primaryTable.ColumnInfos.Single(c => c.IsPrimaryKey);

        _qb.PrimaryExtractionTable = _primaryTable;

        _qb.RegenerateSQL();
            
        _joins = _qb.JoinsUsedInQuery ?? new List<JoinInfo>();

        _fromSql = SqlQueryBuilderHelper.GetFROMSQL(_qb);

        if(!isChecks)
            foreach (var tableInfo in TablesToIsolate)
                _fromSql = _fromSql.Replace(tableInfo.GetFullyQualifiedName(), GetRAWTableNameFullyQualified(tableInfo));

        if (_joins.Any(j=>j.GetSupplementalJoins().Any()))
            throw new($"Supplemental (2 column) joins are not supported when resolving multi table primary key collisions: problem join is {_joins.Select(j=>j.GetSupplementalJoins().FirstOrDefault())}");

        //order the tables in order of dependency
        List<TableInfo> tables = new();

        var next = _primaryTable;

        var overflow = 10;
        while (next != null)
        {
            tables.Add(next);
            var jnext = _joins.SingleOrDefault(j => j.PrimaryKey.TableInfo.Equals(next));
            if (jnext == null)
                break;

            next = jnext.ForeignKey.TableInfo;
                
            if(overflow-- ==0)
                throw new("Joins resulted in a loop overflow");
        }

        TablesToIsolate = tables.ToArray();
    }

    private string GetRAWTableNameFullyQualified(TableInfo tableInfo)
    {
        return _syntaxHelper.EnsureFullyQualified(
            _namer.GetDatabaseName(tableInfo.GetDatabaseRuntimeName(), LoadBubble.Raw), null,
            _namer.GetName(tableInfo.GetRuntimeName(), LoadBubble.Raw));
    }

    public void LoadCompletedSoDispose(ExitCodeType exitCode, IDataLoadEventListener postLoadEventsListener)
    {
    }

    public void Initialize(DiscoveredDatabase dbInfo, LoadStage loadStage)
    {
        _raw = dbInfo;
        _syntaxHelper = _raw.Server.GetQuerySyntaxHelper();

        if(loadStage != LoadStage.AdjustRaw)
            throw new("This component should only run in AdjustRaw");
    }

    public ExitCodeType Mutilate(IDataLoadJob job)
    {
        _dataLoadInfoId = job.JobID;
        _namer = job.Configuration.DatabaseNamer;
        _job = job;

        BuildJoinOrder(false);
            
        foreach (var tableInfo in TablesToIsolate)
        {
            var pkCol = tableInfo.ColumnInfos.Single(c => c.IsPrimaryKey);

            var allCollisions = DetectCollisions(pkCol, tableInfo).Distinct().ToArray();

            if (!allCollisions.Any()) continue;
            _job.OnNotify(this,new(ProgressEventType.Information, $"Found duplication in column '{pkCol}', duplicate values were '{string.Join(",",allCollisions)}'"));
            MigrateRecords(pkCol, allCollisions);
        }

        return ExitCodeType.Success;
    }

    private void MigrateRecords(ColumnInfo deleteOn,object[] deleteValues)
    {
        var deleteOnColumnName = GetRAWColumnNameFullyQualified(deleteOn);

        using var con = _raw.Server.GetConnection();
        con.Open();

        //if we are deleting on a child table we need to look up the primary table primary key (e.g. StudyInstanceUID) we should then migrate that data instead (for all tables)
        if (!deleteOn.Equals(_primaryTablePk))
        {
            deleteValues = GetPrimaryKeyValuesFor(deleteOn, deleteValues, con);
            deleteOnColumnName = GetRAWColumnNameFullyQualified(_primaryTablePk);
        }

        //pull all records that we must isolate in all joined tables
        var toPush = TablesToIsolate.ToDictionary(tableInfo => tableInfo, tableInfo => PullTable(tableInfo, con, deleteOnColumnName, deleteValues));

        //push the results to isolation
        foreach (var (key, value) in toPush)
        {
            var toDatabase = IsolationDatabase.Discover(DataAccessContext.DataLoad);
            var toTable = toDatabase.ExpectTable(GetIsolationTableName(key));

            using var bulkInsert = toTable.BeginBulkInsert();
            bulkInsert.Upload(value);
        }

        foreach (var t in TablesToIsolate.Reverse())
            DeleteRows(t, deleteOnColumnName, deleteValues, con);

        con.Close();
    }

    /// <summary>
    /// Returns the fully qualified RAW name of the column factoring in namer e.g. [ab213_ImagingRAW]..[StudyTable].[MyCol]
    /// </summary>
    /// <param name="col"></param>
    /// <returns></returns>
    private string GetRAWColumnNameFullyQualified(ColumnInfo col)
    {
        return _syntaxHelper.EnsureFullyQualified(_raw.GetRuntimeName(), null, col.TableInfo.GetRuntimeName(LoadBubble.Raw, _namer), col.GetRuntimeName(LoadStage.AdjustRaw));
    }

    /// <summary>
    /// Looks up the full join table to identify primary key value(s) for all colliding child tables
    /// </summary>
    /// <param name="deleteOn"></param>
    /// <param name="deleteValue"></param>
    /// <param name="con"></param>
    /// <returns></returns>
    private object[] GetPrimaryKeyValuesFor(ColumnInfo deleteOn, object[] deleteValue, DbConnection con)
    {
        var deleteOnColumnName = GetRAWColumnNameFullyQualified(deleteOn);
        var pkColumnName = GetRAWColumnNameFullyQualified(_primaryTablePk);

        HashSet<object> toReturn = new();

        //fetch all the data
        var sqlSelect = $"Select distinct {pkColumnName} {_fromSql} WHERE {deleteOnColumnName} = @val";
        using(var cmdSelect = _raw.Server.GetCommand(sqlSelect, con))
        {
            var p = cmdSelect.CreateParameter();
            p.ParameterName = "@val";
            cmdSelect.Parameters.Add(p);
            cmdSelect.CommandTimeout = TimeoutInSeconds;

            foreach (var d in deleteValue)
            {
                p.Value = d;
                var readOne = false;
                using var r = cmdSelect.ExecuteReader();
                while (r.Read())
                {
                    var result = r[0];

                    if(result == DBNull.Value || result == null)
                        throw new($"Primary key value not found via '{ sqlSelect }' for {d} foreign Key was null");

                    toReturn.Add(result);
                    readOne = true;
                }

                if(!readOne)
                    throw new($"Primary key value not found for {d} using '{ sqlSelect }'");
            }

        }
            

        return toReturn.ToArray();
    }

    private DataTable PullTable(TableInfo tableInfo, DbConnection con, string deleteOnColumnName, object[] deleteValues)
    {
        DataTable dt = new();
        var pk = tableInfo.ColumnInfos.Single(c => c.IsPrimaryKey);
        var pkColumnName = GetRAWColumnNameFullyQualified(pk);

        var deleteFromTableName = GetRAWTableNameFullyQualified(tableInfo);
            
        //fetch all the data (LEFT/RIGHT joins can introduce null records so add not null to WHERE for the table being migrated to avoid full null rows)
        var sqlSelect =
            $"Select distinct {deleteFromTableName}.* {_fromSql} WHERE {deleteOnColumnName} = @val AND {pkColumnName} is not null";
        using var cmdSelect = _raw.Server.GetCommand(sqlSelect, con);
        var p = cmdSelect.CreateParameter();
        p.ParameterName = "@val";
        cmdSelect.Parameters.Add(p);
        cmdSelect.CommandTimeout = TimeoutInSeconds;

        foreach (var value in deleteValues)
        {
            p.Value = value;

            using var da = _raw.Server.GetDataAdapter(cmdSelect);
            da.Fill(dt);
        }
                
        dt.Columns.Add(SpecialFieldNames.DataLoadRunID, typeof(int));
                
        foreach (DataRow row in dt.Rows)
            row[SpecialFieldNames.DataLoadRunID] = _dataLoadInfoId;


        return dt;
    }

    private void DeleteRows(TableInfo toDelete, string deleteOnColumnName, object[] deleteValues, DbConnection con)
    {
        var syntax = _raw.Server.GetQuerySyntaxHelper();

        //now delete all records
        var sqlDelete =
            $"DELETE {syntax.EnsureWrapped(toDelete.GetRuntimeName(LoadBubble.Raw, _namer))} {_fromSql} WHERE {deleteOnColumnName} = @val";

        if(syntax.DatabaseType == DatabaseType.PostgreSql)
        {
            sqlDelete = GetPostgreSqlDeleteCommand(toDelete,deleteOnColumnName,syntax);
        }

        _job.OnNotify(this,new(ProgressEventType.Information, $"Running:{sqlDelete}"));

        using var cmdDelete = _raw.Server.GetCommand(sqlDelete, con);
        var p2 = cmdDelete.CreateParameter();
        p2.ParameterName = "@val";
        cmdDelete.Parameters.Add(p2);
        cmdDelete.CommandTimeout = TimeoutInSeconds;

        foreach (var d in deleteValues)
        {
            p2.Value = d;
                    
            //then delete it
            var affectedRows = cmdDelete.ExecuteNonQuery();

            _job.OnNotify(this,new(ProgressEventType.Information,
                $"{affectedRows} affected rows"));

        }
    }

    private string GetPostgreSqlDeleteCommand(TableInfo toDelete,string deleteOnColumnName, IQuerySyntaxHelper syntax)
    {
        if(!_joins.Any())
        {
            return
                $"DELETE FROM {syntax.EnsureWrapped(toDelete.GetRuntimeName(LoadBubble.Raw, _namer))} WHERE {deleteOnColumnName} = @val";
        }

        var sb = new StringBuilder();

        // 1 join per pair of tables
                
        if(_joins.Count != TablesToIsolate.Length -1)
            throw new($"Unexpected join count, expected {(TablesToIsolate.Length -1)} but found {_joins.Count}");

        // Imagine a 3 table query (2 joins)
        // if we are index 2 (child)
        // we want all joins
        // if we are index 1 (middle)
        // we want first join only
        // if we are index 0 (parent)
        // we want no joins at all

        var idx = Array.IndexOf(TablesToIsolate,toDelete);
        var usings = new HashSet<TableInfo>();

        foreach (var j in _joins.Where(j => idx > 0))
        {
            idx--;

            sb.Append(syntax.EnsureWrapped(j.PrimaryKey.TableInfo.GetRuntimeName(LoadBubble.Raw,_namer)));
            sb.Append('.');
            sb.Append(syntax.EnsureWrapped(j.PrimaryKey.GetRuntimeName(LoadStage.AdjustRaw)));
                    
            sb.Append('=');
                    
            sb.Append(syntax.EnsureWrapped(j.ForeignKey.TableInfo.GetRuntimeName(LoadBubble.Raw,_namer)));
            sb.Append('.');
            sb.Append(syntax.EnsureWrapped(j.ForeignKey.GetRuntimeName(LoadStage.AdjustRaw)));

            sb.Append(" AND ");

            usings.Add(j.ForeignKey.TableInfo);
            usings.Add(j.PrimaryKey.TableInfo);
        }

        var delTable = syntax.EnsureWrapped(toDelete.GetRuntimeName(LoadBubble.Raw, _namer));
        var usingsStr = string.Join(",",usings.Except(new []{toDelete}).Select(t=>syntax.EnsureWrapped(t.GetRuntimeName(LoadBubble.Raw,_namer))));
        usingsStr = string.IsNullOrWhiteSpace(usingsStr) ? "" : $" USING {usingsStr}";

        return $"DELETE FROM {delTable} {usingsStr} WHERE {sb} {deleteOnColumnName} = @val";
    }

    private IEnumerable<object> DetectCollisions(ColumnInfo pkCol,TableInfo tableInfo)
    {
        var pkColName = pkCol.GetRuntimeName(LoadStage.AdjustRaw);

        var tableNameFullyQualified = GetRAWTableNameFullyQualified(tableInfo);

        var primaryKeysColliding = string.Format(
            "SELECT {0} FROM {1} GROUP BY {0} HAVING count(*)>1",
            _syntaxHelper.EnsureWrapped(pkColName),
            tableNameFullyQualified
        );

        using var con = _raw.Server.GetConnection();
        con.Open();
        using var cmd = _raw.Server.GetCommand(primaryKeysColliding, con);
        cmd.CommandTimeout = TimeoutInSeconds;

        using var r = cmd.ExecuteReader();
        while (r.Read())
            yield return r[pkColName];
    }
}