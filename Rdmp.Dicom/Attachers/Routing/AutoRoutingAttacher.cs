using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using FAnsi.Implementations.MySql;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.DataLoad;
using Rdmp.Core.Curation.Data.Pipelines;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.DataLoad;
using Rdmp.Core.DataLoad.Engine.Attachers;
using Rdmp.Core.DataLoad.Engine.Job;
using Rdmp.Core.DataLoad.Engine.Pipeline.Destinations;
using Rdmp.Core.Logging;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.Progress;
using Rdmp.Dicom.PipelineComponents.DicomSources.Worklists;
using Rdmp.Core.DataFlowPipeline.Requirements;
using Rdmp.Core.Curation.Checks;

namespace Rdmp.Dicom.Attachers.Routing
{
    /// <summary>
    /// Routes tags from DICOM files to relational database tables in RAW as part of a DLE configuration. 
    /// <para><see cref="AutoRoutingAttacherPipelineUseCase"/></para>
    /// </summary>
    public class AutoRoutingAttacher:Attacher,IPluginAttacher, IDemandToUseAPipeline, IDataFlowDestination<DataTable>
    {
        public IDataLoadJob Job;
        private Dictionary<string, HashSet<DataTable>> _columnNameToTargetTablesDictionary;
        private Dictionary<string, Tuple<SqlBulkInsertDestination,ITableLoadInfo>> _uploaders;
        
        [DemandsInitialization(@"Optional, when specified this regex must match ALL table names in the load.  
The pattern must contain a single Group e.g. '^(.*)_.*$' would match CT_Image and CT_Study with the group matching 'CT'.  
This Grouping will be used to extract the Modality code when deciding which table(s) to put a given record into")]
        public Regex ModalityMatchingRegex { get; set; }

        [DemandsInitialization("The pipeline that generates DataTables for auto routing", mandatory:true)]
        public Pipeline LoadPipeline { get; set; }

        [DemandsInitialization("This attacher expects multiple flat files that will be loaded this pattern should match them (file pattern not regex e.g. *.csv)")]
        public string ListPattern { get; set; }
        
        readonly Dictionary<string, bool> _columnNamesRoutedSuccesfully = new(StringComparer.CurrentCultureIgnoreCase);

        readonly Stopwatch _sw = new();
        Dictionary<DataTable, string> _modalityMap;

        protected AutoRoutingAttacher(bool requestsExternalDatabaseCreation) : base(requestsExternalDatabaseCreation) //Derived classes can change mind about RAW creation
        {
            
        }

        public AutoRoutingAttacher() : base(true)//Create RAW for us
        {
        }

        public override ExitCodeType Attach(IDataLoadJob job,GracefulCancellationToken token)
        {
            Job = job;
            
            //if we have an explicit payload to run instead (this is how you inject explicit files/archives/directories to be loaded without touching the disk
            if (job.Payload != null)
            {
                var useCase = new AutoRoutingAttacherPipelineUseCase(this, (IDicomWorklist)job.Payload);
                var engine = useCase.GetEngine(LoadPipeline, Job);
                engine.ExecutePipeline(token);
            }
            else
            {
                if (ListPattern == null)
                {
                    job.OnNotify(this,new NotifyEventArgs(ProgressEventType.Warning, "ListPattern was null and no Payload alternative was set, skipping component"));
                    return ExitCodeType.Success;
                }

                //no explicit injected payload, so use the ForLoading directory to generate the list of dicom/zip files to process
                foreach (var filesToLoad in job.LoadDirectory.ForLoading.GetFiles(ListPattern))
                {
                    var useCase = new AutoRoutingAttacherPipelineUseCase(this, new FlatFileToLoadDicomFileWorklist(new FlatFileToLoad(filesToLoad)));
                    var engine = useCase.GetEngine(LoadPipeline, job);
                    engine.ExecutePipeline(token);
                }
            }

            var unmatchedColumns = string.Join(","+Environment.NewLine , _columnNamesRoutedSuccesfully.Where(kvp => kvp.Value == false).Select(k => k.Key));

            if (!string.IsNullOrWhiteSpace(unmatchedColumns))
            {
                //for each column see in an input table that was not succesfully routed somewhere
                job.OnNotify(this,new NotifyEventArgs(ProgressEventType.Warning, "Ignored input columns:"+unmatchedColumns));
            }
            

            return ExitCodeType.Success;
        }

        private void CreateTableUploaders()
        {
            _uploaders = new Dictionary<string, Tuple<SqlBulkInsertDestination,ITableLoadInfo>>(StringComparer.CurrentCultureIgnoreCase);
            foreach (var t in Job.RegularTablesToLoad)
            {
                var tblName = t.GetRuntimeName(LoadBubble.Raw,Job.Configuration.DatabaseNamer);
                var dest = new SqlBulkInsertDestination(_dbInfo,tblName,Array.Empty<string>());

                var tli = Job.DataLoadInfo.CreateTableLoadInfo("", tblName, new[] {new DataSource("AutoRoutingAttacher")}, 0);

                dest.PreInitialize(tli, Job);

                _uploaders.Add(t.GetRuntimeName(LoadStage.AdjustRaw,Job.Configuration.DatabaseNamer), Tuple.Create(dest,tli));
            }
        }

        private void RefreshUploadDictionary()
        {
            //Each column name can exist in multiple TableInfos (e.g. foreign keys with the same name) so we can route a column to multiple destination tables
            _columnNameToTargetTablesDictionary = new Dictionary<string, HashSet<DataTable>>(StringComparer.CurrentCultureIgnoreCase);

            foreach (var tableInfo in Job.RegularTablesToLoad)
            {
                var dt = new DataTable(tableInfo.GetRuntimeName(LoadBubble.Raw, Job.Configuration.DatabaseNamer));

                foreach (var columnInfo in tableInfo.GetColumnsAtStage(LoadStage.AdjustRaw))
                {
                    var colName = columnInfo.GetRuntimeName(LoadStage.AdjustRaw);

                    //add the column to the DataTable that will be uploaded
                    dt.Columns.Add(colName);

                    //add it to the routing dictionary
                    if (!_columnNameToTargetTablesDictionary.ContainsKey(colName))
                        _columnNameToTargetTablesDictionary.Add(colName, new HashSet<DataTable>());

                    if (!_columnNameToTargetTablesDictionary[colName].Contains(dt))
                        _columnNameToTargetTablesDictionary[colName].Add(dt);
                }
            }
        }

        public override void Check(ICheckNotifier notifier)
        {
            if(LoadPipeline != null)
            {
                new PipelineChecker(LoadPipeline).Check(notifier);
                
                //don't check this since we are our own Fixed source for the engine so we just end up in a loop! but do instantiate it incase there are construction/context errors
                
                PipelineChecker c = new PipelineChecker(LoadPipeline);
                c.Check(notifier);
            }

            if (ModalityMatchingRegex != null && !ModalityMatchingRegex.ToString().Contains('('))
                notifier.OnCheckPerformed(
                    new CheckEventArgs(
                        "Expected ModalityMatchingRegex '" + ModalityMatchingRegex +
                        "' to contain a group matching for extracting modality e.g. '^(.*)_.*$'", CheckResult.Fail));
        }

        public override void LoadCompletedSoDispose(ExitCodeType exitCode, IDataLoadEventListener postLoadEventListener)
        {
        }

        public IPipelineUseCase GetDesignTimePipelineUseCase(RequiredPropertyInfo property)
        {
            return AutoRoutingAttacherPipelineUseCase.GetDesignTimeUseCase(this);
        }

        #region Process Results Of Pipeline Read
        public DataTable ProcessPipelineData(DataTable toProcess, IDataLoadEventListener listener,GracefulCancellationToken cancellationToken)
        {
            MySqlBulkCopy.BulkInsertBatchTimeoutInSeconds = int.MaxValue; //forever

            _sw.Start();

            RefreshUploadDictionary();

            CreateTableUploaders();
            
            CreateModalityMap();

            AddRows(toProcess);

            Exception ex = null;
            try
            {
                BulkInsert(cancellationToken);

            }
            catch(Exception exception)
            {
                ex = exception;
            }

            DisposeUploaders(ex);

            if (ex != null)
                throw new Exception("Error occurred during upload",ex);

            _sw.Stop();

            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "ProcessPipelineData (Upload) cumulative total time is " + _sw.ElapsedMilliseconds + "ms"));

            return null;
        }

        private void CreateModalityMap()
        {
            //no need
            if(ModalityMatchingRegex == null)
                return;

            _modalityMap = new Dictionary<DataTable,string>();

            foreach (DataTable dt in _columnNameToTargetTablesDictionary.Values.SelectMany(v=>v).Distinct())
            {
                var m = ModalityMatchingRegex.Match(dt.TableName);

                if(!m.Success)
                    throw new Exception("ModalityMatchingRegex failed to match against DataTable " + dt.TableName);

                var modality = m.Groups[1].Value;
            
                if(!_modalityMap.ContainsKey(dt))
                    _modalityMap.Add(dt,modality);
            }
        }

        private void BulkInsert(GracefulCancellationToken token)
        {
            foreach (var dt in _columnNameToTargetTablesDictionary.Values.SelectMany(hs => hs).Distinct())
                if (dt.Rows.Count > 0)
                    _uploaders[dt.TableName].Item1.ProcessPipelineData(dt, Job, token);
        }

        private void AddRows(DataTable toProcess)
        {
            foreach (DataColumn dc in toProcess.Columns)
                if (!_columnNamesRoutedSuccesfully.ContainsKey(dc.ColumnName))
                    _columnNamesRoutedSuccesfully.Add(dc.ColumnName, false);


            //for every row in the input table
            foreach (DataRow inputRow in toProcess.Rows)
            {
                Dictionary<DataTable,DataRow>  newDestinationRows = new Dictionary<DataTable, DataRow>();

                //get the modality of the current record (if we care)
                string modality = null;

                if (_modalityMap != null)
                    modality = inputRow["Modality"].ToString();

                bool addedToAtLeastOneTable = false;

                //for every input cell
                foreach (DataColumn column in toProcess.Columns)
                {
                    //if there is a destination for that DataTable
                    if (!_columnNameToTargetTablesDictionary.ContainsKey(column.ColumnName)) continue;
                    //there is a matching destination column in one or more destination tables in RAW
                    foreach (var destinationTable in _columnNameToTargetTablesDictionary[column.ColumnName])
                    {
                        //if we are mapping modalities to tables and this table isn't an ALL table
                        if (_modalityMap != null && !_modalityMap[destinationTable].Equals("ALL",StringComparison.CurrentCultureIgnoreCase))
                            if(!string.Equals(_modalityMap[destinationTable], modality,StringComparison.CurrentCultureIgnoreCase))
                                continue; //skip it

                        AddCellValue(inputRow, column, destinationTable, newDestinationRows);
                        addedToAtLeastOneTable = true;
                    }

                    _columnNamesRoutedSuccesfully[column.ColumnName] = true;
                }

                //we didn't add the row to any tables yet
                if( _modalityMap != null && !addedToAtLeastOneTable)
                {
                    //Try again but put it in OTHER
                    foreach (DataColumn column in toProcess.Columns)
                    {
                        if (!_columnNameToTargetTablesDictionary.ContainsKey(column.ColumnName)) continue;
                        //there is a matching destination column in one or more destination tables in RAW
                        foreach (var destinationTable in _columnNameToTargetTablesDictionary[column.ColumnName].Where(destinationTable => _modalityMap[destinationTable].Equals("OTHER",StringComparison.CurrentCultureIgnoreCase)))
                        {
                            AddCellValue(inputRow, column, destinationTable, newDestinationRows);
                            addedToAtLeastOneTable = true;
                        }
                        _columnNamesRoutedSuccesfully[column.ColumnName] = true;
                    }
                }

                if(!addedToAtLeastOneTable && _modalityMap != null)
                    throw new Exception("Failed to route row with modality:" + modality + " Mapping was " +
                        string.Join(Environment.NewLine,_modalityMap.Select(kvp=>kvp.Key.TableName + "=" + kvp.Value)));
            }
        }

        private void AddCellValue(DataRow inputRow, DataColumn column, DataTable destinationTable, Dictionary<DataTable, DataRow> newDestinationRows)
        {
            //if destination table doesn't have a new row yet add one
            if (!newDestinationRows.ContainsKey(destinationTable))
                newDestinationRows.Add(destinationTable, destinationTable.Rows.Add());

            //copy the value into the new row
            newDestinationRows[destinationTable][column.ColumnName] = inputRow[column.ColumnName];
        }

        public void Dispose(IDataLoadEventListener listener, Exception pipelineFailureExceptionIfAny)
        {
            
        }

        private void DisposeUploaders(Exception exception)
        {
            foreach (var (item1, item2) in _uploaders.Values)
            {
                item1.Dispose(new ThrowImmediatelyDataLoadEventListener(), exception);
                item2.CloseAndArchive();
            }
            
            foreach (var dt in _columnNameToTargetTablesDictionary.SelectMany(v => v.Value).Distinct())
                dt.Dispose();
            
            _columnNameToTargetTablesDictionary = null;
            _uploaders = null;
        }

        public void Abort(IDataLoadEventListener listener)
        {
            
        }
        #endregion
    }
}
