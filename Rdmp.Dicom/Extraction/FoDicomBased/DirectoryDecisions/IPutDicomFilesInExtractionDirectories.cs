using System.ComponentModel.Composition;
using System.IO;
using Dicom;

namespace Rdmp.Dicom.Extraction.FoDicomBased.DirectoryDecisions
{
    [InheritedExport(typeof(IPutDicomFilesInExtractionDirectories))]
    public interface IPutDicomFilesInExtractionDirectories
    {
        string WriteOutDataset(DirectoryInfo outputDirectory, string releaseIdentifier, DicomDataset dicomDataset);
    }
}