using System.Data;
using System.Diagnostics;
using FellowOakDicom;
using Rdmp.Core.ReusableLibraryCode.Progress;
using Rdmp.Dicom.PipelineComponents.DicomSources.Worklists;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.DataFlowPipeline.Requirements;

namespace Rdmp.Dicom.PipelineComponents.DicomSources;

public class DicomDatasetCollectionSource : DicomSource, IPipelineRequirement<IDicomWorklist>
{
    private IDicomDatasetWorklist _datasetListWorklist;

    private const int BatchSize = 50000;

    private readonly Stopwatch _sw = new();

    public void PreInitialize(IDicomWorklist value, IDataLoadEventListener listener)
    {
        _datasetListWorklist = value as IDicomDatasetWorklist;

        if (_datasetListWorklist == null)
            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning,
                $"Expected IDicomWorklist to be of Type IDicomDatasetWorklist (but it was {value.GetType().Name}). Component will be skipped."));
    }

    protected override void MarkCorrupt(DicomDataset ds)
    {
        base.MarkCorrupt(ds);
        _datasetListWorklist.MarkCorrupt(ds);
    }
        
    public override DataTable GetChunk(IDataLoadEventListener listener, GracefulCancellationToken cancellationToken)
    {
        _sw.Start();

        if(_datasetListWorklist == null)
        {
            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning, "Skipping component because _datasetListWorklist is null"));
            return null;
        }

        var currentBatch = BatchSize;

        var dt = GetDataTable();

        dt.BeginLoadData();
        while (currentBatch > 0 && _datasetListWorklist.GetNextDatasetToProcess(out var filename,out var otherValuesToStoreInRow) is { } ds)
        {
            ProcessDataset(filename, ds, dt, listener, otherValuesToStoreInRow);
            currentBatch--;
        }
        dt.EndLoadData();
            
        _sw.Stop();
        listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information,
            $"GetChunk cumulative total time is {_sw.ElapsedMilliseconds}ms"));

        return dt.Rows.Count > 0 ? dt : null;
    }

    public override DataTable TryGetPreview()
    {
        return GetDataTable();
    }
}