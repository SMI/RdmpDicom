using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using FAnsi.Discovery;
using FAnsi.Discovery.QuerySyntax;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.DataLoad;
using ReusableLibraryCode.DataAccess;

namespace Rdmp.Dicom.PipelineComponents
{
    public class IsolationReview
    {
        public string Error { get; }
        public TableInfo[] TablesToIsolate { get; set; }
        public ExternalDatabaseServer IsolationDatabase { get; set; }
        
        public int Top { get; set; }
        public int Timeout { get; set; } = 300;

        public IsolationReview(ProcessTask processTask)
        {
            if(processTask == null)
                throw new ArgumentNullException(nameof(processTask));
        
            if (!processTask.IsPluginType() || processTask.ProcessTaskType != ProcessTaskType.MutilateDataTable ||
                processTask.Path != typeof(PrimaryKeyCollisionIsolationMutilation).FullName)
            {
                Error = "ProcessTask is not an isolation mutilation";
                return;
            }

            foreach (IArgument a in processTask.GetAllArguments())
            {
                try
                {
                    if (a.Name.Equals(nameof(PrimaryKeyCollisionIsolationMutilation.TablesToIsolate)))
                        TablesToIsolate = (TableInfo[]) a.GetValueAsSystemType();
                    if (a.Name.Equals(nameof(PrimaryKeyCollisionIsolationMutilation.IsolationDatabase)))
                        IsolationDatabase = (ExternalDatabaseServer) a.GetValueAsSystemType();
                }
                catch (Exception e)
                {
                    Error = $"Bad Argument {a} : {e.Message}";
                    return;
                }
            }
            
            if (TablesToIsolate == null || TablesToIsolate.Length == 0)
            {
                Error = "No tables configured on Isolation task";
                return;
            }
            if (IsolationDatabase == null)
            {
                Error = "No isolation database configured on Isolation task";
                return;
            }
        }

        public Dictionary<TableInfo,DiscoveredTable> GetIsolationTables()
        {
            var db = IsolationDatabase.Discover(DataAccessContext.InternalDataProcessing);

            return TablesToIsolate.ToDictionary(
                tableInfo => tableInfo, 
                tableInfo => db.ExpectTable(PrimaryKeyCollisionIsolationMutilation.GetIsolationTableName(tableInfo))
                );
        }

        public DataTable GetDifferences(KeyValuePair<TableInfo,DiscoveredTable> isolationTable, out List<IsolationDifference> differences)
        {
            var ti = isolationTable.Key;
            var tbl = isolationTable.Value;

            if(!tbl.Exists())
                throw new Exception($"Table '{tbl.GetFullyQualifiedName()}' did not exist");

            var pks = ti.ColumnInfos.Where(c => c.IsPrimaryKey).ToArray();
            
            if(pks.Length != 1)
                throw new Exception($"TableInfo {ti} for which isolation table exists has {pks.Length} IsPrimaryKey columns");
            
            var isolationCols = tbl.DiscoverColumns();

            var isolationPks = isolationCols.Where(c => c.GetRuntimeName().Equals(pks[0].GetRuntimeName())).ToArray();
            
            if(isolationPks.Length != 1)
                throw new Exception($"Found {isolationPks.Length != 1} columns called {pks[0].GetRuntimeName()} in isolation table {tbl.GetFullyQualifiedName()}");

            var isolationPk = isolationPks[0];
            
            var sortOn = isolationPk.GetRuntimeName();
         
            using (var con = tbl.Database.Server.GetConnection())
            {
                con.Open();


                string sql;

                if (Top > 0)
                {
                    var syntaxHelper = tbl.Database.Server.GetQuerySyntaxHelper();
                    var topxSql = 
                        syntaxHelper.HowDoWeAchieveTopX(Top);

                    switch (topxSql.Location )
                    {
                        case QueryComponent.SELECT :
                            sql = $"select distinct {topxSql.SQL} * from {tbl.GetFullyQualifiedName()} where {isolationPk.GetFullyQualifiedName()} is not null order by {isolationPk.GetFullyQualifiedName()}";
                            break;
                        case QueryComponent.Postfix :
                            sql = $"select distinct * from {tbl.GetFullyQualifiedName()} where {isolationPk.GetFullyQualifiedName()} is not null order by {isolationPk.GetFullyQualifiedName()} {topxSql.SQL} ";
                            break;
                        default: throw new ArgumentOutOfRangeException($"Unsure how to TOP x with DBMS {syntaxHelper.DatabaseType}, TOP X QueryComponent was listed as {topxSql.Location}");
                    }
                }
                else
                {
                    sql = $"select distinct * from {tbl.GetFullyQualifiedName()} where {isolationPk.GetFullyQualifiedName()} is not null order by {isolationPk.GetFullyQualifiedName()}";
                }

                DataTable dt = new DataTable();
                
                using (var cmd = tbl.Database.Server.GetCommand(sql, con))
                {
                    cmd.CommandTimeout = Timeout;
                    using (var da = tbl.Database.Server.GetDataAdapter(cmd))
                    {
                        da.Fill(dt);
                    }
                }

                differences = new List<IsolationDifference>();
                
                //if theres only 1 row in the table then there are no differences!
                if (dt.Rows.Count < 2)
                {
                    dt.Rows.Clear();
                    return dt;
                }

                //clone the schema and import only rows where there are 2+ entries for the same 'pk' value
                DataTable differencesDt = dt.Clone();
                
                //for each PK value the first time we encounter it it is the 'master' row version from which all other rows are compared
                DataRow masterRow = dt.Rows[0];
                bool haveImportedMasterRow = false;
                int differencesDtIdx = 0;

                foreach(DataRow currentRow in dt.Rows)
                {
                    if (masterRow[sortOn].Equals(currentRow[sortOn]))
                    {
                        //if the current row is listed as the same 'pk' as the last row then the user needs to know the difference (if any)
                        if (!haveImportedMasterRow)
                        {
                            differencesDt.ImportRow(masterRow);
                            haveImportedMasterRow = true;
                            differences.Add(new IsolationDifference(differencesDt.Rows.Count - 1, masterRow[sortOn].ToString(), true));
                            differencesDtIdx++;
                        }

                        //happens for the very first row (loop iteration) only
                        if (masterRow != currentRow)
                        {
                            differencesDt.ImportRow(currentRow);
                            var diff = new IsolationDifference(differencesDt.Rows.Count - 1, masterRow[sortOn].ToString(), false);
                            differences.Add(diff);
                            differencesDtIdx++;

                            //record the columns that differed
                            foreach (DataColumn dc in dt.Columns)
                            {
                                if(AreDifferent(masterRow[dc],currentRow[dc]))
                                    diff.ConflictingColumns.Add(dc.ColumnName);
                            }
                        }
                    }
                    else
                    {
                        //the pk has changed take the current row as the new master
                        haveImportedMasterRow = false;
                        masterRow = currentRow;
                    }
                }

                return differencesDt;
            }
        }

        private bool AreDifferent(object a, object b)
        {
            if (a == null || b == null)
                return a != b;

            if (a == DBNull.Value)
                return b != DBNull.Value;

            if (b == DBNull.Value)
                return a != DBNull.Value;

            return !a.ToString().Equals(b.ToString());
        }
    }

    public class IsolationDifference
    {
        public string Pk { get; set; }
        
        public int RowIndex { get; set; }
        
        public bool IsMaster { get; set; }

        public List<string> ConflictingColumns { get; set; } = new List<string>();

        public IsolationDifference(int rowIndex, string pk , bool isMaster)
        {
            RowIndex = rowIndex;
            Pk = pk;
            IsMaster = isMaster;
        }

    }
}

