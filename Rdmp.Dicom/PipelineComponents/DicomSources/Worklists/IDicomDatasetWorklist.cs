using System.Collections.Generic;
using Dicom;

namespace Rdmp.Dicom.PipelineComponents.DicomSources.Worklists
{
    public interface IDicomDatasetWorklist : IDicomWorklist
    {
        /// <summary>
        /// Returns the next DicomDataset that should be processed.  Returns null if there are no more datasets to process.
        /// </summary>
        /// <param name="filename">The absolute or relative path to the file that is represented by the DicomDataset</param>
        /// <param name="otherValuesToStoreInRow">Key value collection of any other columns that should be populated with values 
        /// (there should not include the names of any dicom tags in the key collection).  E.g. 'MessageGuid' would be acceptable but 'StudyDate' would not</param>
        /// <returns></returns>
        DicomDataset GetNextDatasetToProcess(out string filename, out Dictionary<string, string> otherValuesToStoreInRow);

        /// <summary>
        /// Marks the given dataset as corrupt / unloadable
        /// </summary>
        /// <param name="ds"></param>
        void MarkCorrupt(DicomDataset ds);
    }
}