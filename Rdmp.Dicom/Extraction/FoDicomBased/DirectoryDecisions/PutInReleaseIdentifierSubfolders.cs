using System.IO;
using Dicom;

namespace Rdmp.Dicom.Extraction.FoDicomBased.DirectoryDecisions
{
    public class PutInReleaseIdentifierSubfolders : PutDicomFilesInExtractionDirectories
    {
        protected override string WriteOutDatasetImpl(DirectoryInfo outputDirectory, string releaseIdentifier, DicomDataset dicomDataset)
        {

            var patientDir = SubDirectoryCreate(outputDirectory, releaseIdentifier);

            return SaveDicomData(patientDir, dicomDataset, ".dcm");
        }
    }
}