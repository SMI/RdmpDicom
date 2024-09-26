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
using Rdmp.Core.ReusableLibraryCode.Checks;
using Rdmp.Core.ReusableLibraryCode.Progress;
using Rdmp.Dicom.PipelineComponents.DicomSources.Worklists;
using Rdmp.Core.Curation.Checks;
using Rdmp.Core.DataFlowPipeline.Requirements;

namespace Rdmp.Dicom.Attachers.Routing;

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

    private Dictionary<DataTable, string> _modalityMap;

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
        if (job.Payload is IDicomWorklist worklist)
        {
            new AutoRoutingAttacherPipelineUseCase(this, worklist).GetEngine(LoadPipeline, Job).ExecutePipeline(token);
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
                new AutoRoutingAttacherPipelineUseCase(this,
                        new FlatFileToLoadDicomFileWorklist(new FlatFileToLoad(filesToLoad)))
                    .GetEngine(LoadPipeline, job)
                    .ExecutePipeline(token);
        }

        var unmatchedColumns = string.Join($",{Environment.NewLine}",
            _columnNamesRoutedSuccesfully.Where(static kvp => kvp.Value == false).Select(static k => k.Key));

        if (!string.IsNullOrWhiteSpace(unmatchedColumns))
            //for each column see in an input table that was not succesfully routed somewhere
            job.OnNotify(this,new NotifyEventArgs(ProgressEventType.Warning,
                $"Ignored input columns:{unmatchedColumns}"));

        return ExitCodeType.Success;
    }

    private void CreateTableUploaders()
    {
        _uploaders = new Dictionary<string, Tuple<SqlBulkInsertDestination, ITableLoadInfo>>(StringComparer.OrdinalIgnoreCase);
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
        _columnNameToTargetTablesDictionary = new Dictionary<string, HashSet<DataTable>>(StringComparer.OrdinalIgnoreCase);

        foreach (var tableInfo in Job.RegularTablesToLoad)
        {
            var dt = new DataTable(tableInfo.GetRuntimeName(LoadBubble.Raw, Job.Configuration.DatabaseNamer));
            dt.BeginLoadData();

            foreach (var colName in tableInfo.GetColumnsAtStage(LoadStage.AdjustRaw)
                         .Select(static columnInfo => columnInfo.GetRuntimeName(LoadStage.AdjustRaw)))
            {
                //add the column to the DataTable that will be uploaded
                dt.Columns.Add(colName);

                //add it to the routing dictionary
                if (!_columnNameToTargetTablesDictionary.TryGetValue(colName, out var targets))
                    _columnNameToTargetTablesDictionary.Add(colName, targets = new HashSet<DataTable>());
                targets.Add(dt);
            }
        }
    }

    public override void Check(ICheckNotifier notifier)
    {
        if (LoadPipeline != null) new PipelineChecker(LoadPipeline).Check(notifier);

        if (ModalityMatchingRegex?.ToString().Contains('(') == false)
            notifier.OnCheckPerformed(
                new CheckEventArgs(
                    $"Expected ModalityMatchingRegex '{ModalityMatchingRegex}' to contain a group matching for extracting modality e.g. '^(.*)_.*$'", CheckResult.Fail));
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

        var sw = Stopwatch.StartNew();

        RefreshUploadDictionary();

        CreateTableUploaders();

        CreateModalityMap();

        AddRows(toProcess);

        try
        {
            BulkInsert(cancellationToken);
        }
        catch (Exception exception)
        {
            DisposeUploaders(exception);
            throw new Exception("Error occurred during upload",exception);
        }

        DisposeUploaders(null);

        sw.Stop();

        listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information,
            $"ProcessPipelineData (Upload) cumulative total time is {sw.ElapsedMilliseconds}ms"));

        return null;
    }

    private void CreateModalityMap()
    {
        //no need
        if(ModalityMatchingRegex == null)
            return;

        _modalityMap = new Dictionary<DataTable, string>();

        foreach (var dt in _columnNameToTargetTablesDictionary.Values.SelectMany(static v=>v).Distinct())
        {
            var m = ModalityMatchingRegex.Match(dt.TableName);

            if(!m.Success)
                throw new Exception($"ModalityMatchingRegex failed to match against DataTable {dt.TableName}");

            var modality = m.Groups[1].Value;

            _modalityMap.TryAdd(dt, modality);
        }
    }

    private void BulkInsert(GracefulCancellationToken token)
    {
        foreach (var dt in _columnNameToTargetTablesDictionary.Values.SelectMany(static hs => hs).Distinct().Where(static dt => dt.Rows.Count > 0))
        {
            dt.EndLoadData();
            _uploaders[dt.TableName].Item1.ProcessPipelineData(dt, Job, token);
        }
    }

    private void AddRows(DataTable toProcess)
    {
        foreach (DataColumn dc in toProcess.Columns) _columnNamesRoutedSuccesfully.TryAdd(dc.ColumnName, false);


        //for every row in the input table
        foreach (DataRow inputRow in toProcess.Rows)
        {
            Dictionary<DataTable,DataRow> newDestinationRows = new();

            //get the modality of the current record (if we care)

            var modality = _modalityMap == null ? null : inputRow["Modality"].ToString();

            var addedToAtLeastOneTable = false;

            //for every input cell
            foreach (DataColumn column in toProcess.Columns)
            {
                //if there is a destination for that DataTable
                if (!_columnNameToTargetTablesDictionary.TryGetValue(column.ColumnName, out var destinations)) continue;
                //there is a matching destination column in one or more destination tables in RAW
                foreach (var destinationTable in destinations)
                {
                    //if we are mapping modalities to tables and this table isn't an ALL table
                    var tableModality = _modalityMap?[destinationTable];
                    if (tableModality?.Equals("ALL", StringComparison.OrdinalIgnoreCase) == false &&
                        tableModality?.Equals(modality, StringComparison.OrdinalIgnoreCase) == false)
                        continue; //skip it

                    AddCellValue(inputRow, column, destinationTable, newDestinationRows);
                    addedToAtLeastOneTable = true;
                }

                _columnNamesRoutedSuccesfully[column.ColumnName] = true;
            }

            //we didn't add the row to any tables yet
            if (_modalityMap != null && !addedToAtLeastOneTable)
                //Try again but put it in OTHER
                foreach (DataColumn column in toProcess.Columns)
                {
                    if (!_columnNameToTargetTablesDictionary.TryGetValue(column.ColumnName, out var tables)) continue;
                    //there is a matching destination column in one or more destination tables in RAW
                    foreach (var destinationTable in tables.Where(destinationTable => _modalityMap[destinationTable].Equals("OTHER", StringComparison.OrdinalIgnoreCase)))
                    {
                        AddCellValue(inputRow, column, destinationTable, newDestinationRows);
                        addedToAtLeastOneTable = true;
                    }

                    _columnNamesRoutedSuccesfully[column.ColumnName] = true;
                }

            if (!addedToAtLeastOneTable && _modalityMap != null)
                throw new Exception(
                    $"Failed to route row with modality:{modality} Mapping was {string.Join(Environment.NewLine, _modalityMap.Select(static kvp => $"{kvp.Key.TableName}={kvp.Value}"))}");
        }
    }

    private static void AddCellValue(DataRow inputRow, DataColumn column, DataTable destinationTable, Dictionary<DataTable, DataRow> newDestinationRows)
    {
        //if destination table doesn't have a new row yet add one
        if (!newDestinationRows.TryGetValue(destinationTable, out var destRow))
            newDestinationRows.Add(destinationTable, destRow = destinationTable.Rows.Add());

        //copy the value into the new row
        destRow[column.ColumnName] = inputRow[column.ColumnName];
    }

    public void Dispose(IDataLoadEventListener listener, Exception pipelineFailureExceptionIfAny)
    {

    }

    private void DisposeUploaders(Exception exception)
    {
        foreach (var (item1, item2) in _uploaders.Values)
        {
            item1.Dispose(ThrowImmediatelyDataLoadEventListener.Quiet, exception);
            item2.CloseAndArchive();
        }

        foreach (var dt in _columnNameToTargetTablesDictionary.SelectMany(static v => v.Value).Distinct())
            dt.Dispose();

        _columnNameToTargetTablesDictionary = null;
        _uploaders = null;
    }

    public void Abort(IDataLoadEventListener listener)
    {

    }
    #endregion
}