using System;
using System.Collections.Generic;
using System.Linq;
using FAnsi.Discovery;
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
    }
}
