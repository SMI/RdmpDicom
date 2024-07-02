using System.IO;
using Rdmp.Dicom.Extraction.FoDicomBased;

namespace Rdmp.Dicom.PipelineComponents.DicomSources.Worklists;

public interface IDicomFileWorklist:IDicomWorklist
{
    /// <summary>
    /// Populates a DirectoryInfo or FileInfo depending on what the next dicom file system collection to process is (or returns false if no more processing is required)
    /// </summary>
    /// <returns></returns>
    bool GetNextFileOrDirectoryToProcess(out DirectoryInfo directory, out AmbiguousFilePath file);
}