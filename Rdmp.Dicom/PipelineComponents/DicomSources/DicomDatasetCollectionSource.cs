using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using Dicom;
using ReusableLibraryCode.Progress;
using Rdmp.Dicom.PipelineComponents.DicomSources.Worklists;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.DataFlowPipeline.Requirements;

namespace Rdmp.Dicom.PipelineComponents.DicomSources
{
    public class DicomDatasetCollectionSource : DicomSource, IPipelineRequirement<IDicomWorklist>
    {
        private IDicomDatasetWorklist _datasetListWorklist;

        private const int BatchSize = 50000;

        readonly Stopwatch _sw = new();

        public void PreInitialize(IDicomWorklist value, IDataLoadEventListener listener)
        {
            _datasetListWorklist = value as IDicomDatasetWorklist;

            if (_datasetListWorklist == null)
                listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning, "Expected IDicomWorklist to be of Type IDicomDatasetWorklist (but it was " + value.GetType().Name + "). Component will be skipped."));
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
            DicomDataset ds;
            
            var dt = GetDataTable();

            while (currentBatch > 0 && (ds = _datasetListWorklist.GetNextDatasetToProcess(out var filename,out var otherValuesToStoreInRow)) != null)
            {
                string filename1 = filename;
                Dictionary<string, string> row = otherValuesToStoreInRow;
                DicomDataset ds1 = ds;
                
                ProcessDataset(filename1, ds1, dt, listener, row);
                currentBatch--;
            }
            
            _sw.Stop();
            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "GetChunk cumulative total time is " + _sw.ElapsedMilliseconds + "ms"));

            return dt.Rows.Count > 0 ? dt : null;
        }

        public override DataTable TryGetPreview()
        {
            return GetDataTable();
        }
    }
}