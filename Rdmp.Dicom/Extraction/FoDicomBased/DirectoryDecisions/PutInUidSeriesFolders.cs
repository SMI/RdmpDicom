using System.IO;
using FellowOakDicom;

namespace Rdmp.Dicom.Extraction.FoDicomBased.DirectoryDecisions
{
    public class PutInUidSeriesFolders : PutDicomFilesInExtractionDirectories
    {
        protected override string WriteOutDatasetImpl(DirectoryInfo outputDirectory, string releaseIdentifier, DicomDataset dicomDataset)
        {
            var finalDir = SubDirectoryCreate(outputDirectory, releaseIdentifier);
            finalDir = SubDirectoryCreate(finalDir, dicomDataset.GetValue<string>(DicomTag.SeriesInstanceUID, 0));
            return SaveDicomData(finalDir, dicomDataset, ".dcm");
        }



    }
}
