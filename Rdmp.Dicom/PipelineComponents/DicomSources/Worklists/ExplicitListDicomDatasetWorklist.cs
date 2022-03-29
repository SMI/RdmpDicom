using System.Collections.Generic;
using FellowOakDicom;

namespace Rdmp.Dicom.PipelineComponents.DicomSources.Worklists
{
    public class ExplicitListDicomDatasetWorklist : IDicomDatasetWorklist
    {
        private readonly DicomDataset[] _datasets;
        private readonly string _filenameToPretend;
        private int index = 0;
        private readonly Dictionary<string, string> _otherValuesToStoreInRow;


        public HashSet<DicomDataset> CorruptMessages = new();


        /// <summary>
        /// For testing <see cref="DicomDatasetCollectionSource"/> this will feed the source the datasets you specify and make it look like they came from the given
        /// filename (must not be null).  Optionally you can specify a dictionary of other values to have fed to the source e.g. "MessageGuid=102321380"
        /// </summary>
        /// <param name="datasets"></param>
        /// <param name="filenameToPretend"></param>
        /// <param name="otherValuesToStoreInRow"></param>
        public ExplicitListDicomDatasetWorklist(DicomDataset[] datasets, string filenameToPretend,Dictionary<string, string> otherValuesToStoreInRow = null)
        {
            _datasets = datasets;
            _filenameToPretend = filenameToPretend;
            _otherValuesToStoreInRow = otherValuesToStoreInRow;
        }

        public DicomDataset GetNextDatasetToProcess(out string filename, out Dictionary<string, string> otherValuesToStoreInRow)
        {
            otherValuesToStoreInRow = _otherValuesToStoreInRow;
            filename = _filenameToPretend;

            return index >= _datasets.Length ? null : _datasets[index++];
        }

        public void MarkCorrupt(DicomDataset ds)
        {
            CorruptMessages.Add(ds);
        }
    }
}