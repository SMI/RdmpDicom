using System;
using System.Collections.Generic;
using System.Linq;
using ReusableLibraryCode.DataAccess;
using FAnsi.Discovery;
using ReusableLibraryCode.Progress;
using Rdmp.Core.DataLoad.Engine.Job;
using Rdmp.Core.DataLoad.Engine;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.DataLoad;
using Rdmp.Core.DataLoad;
using Rdmp.Core.DataLoad.Engine.DatabaseManagement.Operations;

namespace Rdmp.Dicom.Attachers.Routing
{
    /// <summary>
    /// Clones databases and tables using ColumnInfos, and records operations so the cloning can be undone.
    /// </summary>
    public class PersistentRawTableCreator : IDisposeAfterDataLoad
    {
        readonly List<DiscoveredTable> _rawTables = new List<DiscoveredTable>();

        public void CreateRAWTablesInDatabase(DiscoveredDatabase rawDb, IDataLoadJob job)
        {
            var namer = job.Configuration.DatabaseNamer;
            foreach (ITableInfo tableInfo in job.RegularTablesToLoad)
            {
                var liveTable = tableInfo.Discover(DataAccessContext.DataLoad);

                var rawTableName = namer.GetName(liveTable.GetRuntimeName(),LoadBubble.Raw);

                var rawTable = rawDb.ExpectTable(rawTableName);

                if(rawTable.Exists())
                    rawTable.Drop();

                var discardedColumns = tableInfo.PreLoadDiscardedColumns.Where(c => c.Destination == DiscardedColumnDestination.Dilute).ToArray();

                var clone = new TableInfoCloneOperation(null,null,LoadBubble.Raw);
                
                clone.CloneTable(liveTable.Database, rawDb, tableInfo.Discover(DataAccessContext.DataLoad), rawTableName, true,true, true, discardedColumns);
                
                string[] existingColumns = tableInfo.ColumnInfos.Select(c => c.GetRuntimeName(LoadStage.AdjustRaw)).ToArray();

                foreach (PreLoadDiscardedColumn preLoadDiscardedColumn in tableInfo.PreLoadDiscardedColumns)
                {
                    //this column does not get dropped so will be in live TableInfo
                    if (preLoadDiscardedColumn.Destination == DiscardedColumnDestination.Dilute)
                        continue;

                    if (existingColumns.Any(e => e.Equals(preLoadDiscardedColumn.GetRuntimeName(LoadStage.AdjustRaw))))
                        throw new Exception("There is a column called " + preLoadDiscardedColumn.GetRuntimeName(LoadStage.AdjustRaw) + " as both a PreLoadDiscardedColumn and in the TableInfo (live table), you should either drop the column from the live table or remove it as a PreLoadDiscarded column");

                    //add all the preload discarded columns because they could be routed to ANO store or sent to oblivion
                    AddColumnToTable(rawTable, preLoadDiscardedColumn.RuntimeColumnName, preLoadDiscardedColumn.SqlDataType, job);
                }
                
                _rawTables.Add(rawTable);

            }
        }

        private void AddColumnToTable(DiscoveredTable table, string desiredColumnName, string desiredColumnType, IDataLoadEventListener listener)
        {
            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information,
                $"Adding column '{desiredColumnName}' with datatype '{desiredColumnType}' to table '{table.GetFullyQualifiedName()}'"));
            table.AddColumn(desiredColumnName, desiredColumnType, true, 500);
        }

        public void LoadCompletedSoDispose(ExitCodeType exitCode, IDataLoadEventListener postLoadEventsListener)
        {
            foreach (DiscoveredTable rawTable in _rawTables)
                rawTable.Drop();
        }
    }
}