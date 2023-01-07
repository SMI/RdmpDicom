using System.IO;
using FellowOakDicom;

namespace Rdmp.Dicom.Extraction.FoDicomBased.DirectoryDecisions;

public class PutInRoot : PutDicomFilesInExtractionDirectories
{
    protected override string WriteOutDatasetImpl(DirectoryInfo outputDirectory, string releaseIdentifier, DicomDataset dicomDataset)
    {
        return SaveDicomData(outputDirectory, dicomDataset);
    }
}