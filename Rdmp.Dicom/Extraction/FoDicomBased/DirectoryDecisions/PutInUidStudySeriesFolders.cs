using System;
using System.IO;
using Dicom;

namespace Rdmp.Dicom.Extraction.FoDicomBased.DirectoryDecisions
{
    [Obsolete("This is deprecated, limits on the path length imposed by diferent platforms mean it is unreliable.", false)]
    public class PutInUidStudySeriesFolders : PutDicomFilesInExtractionDirectories
    {
        protected override string WriteOutDatasetImpl(DirectoryInfo outputDirectory, string releaseIdentifier, DicomDataset dicomDataset)
        {

            var finalDir = SubDirectoryCreate(outputDirectory, releaseIdentifier);
            finalDir = SubDirectoryCreate(finalDir, dicomDataset.GetValue<string>(DicomTag.StudyInstanceUID, 0));
            finalDir = SubDirectoryCreate(finalDir, dicomDataset.GetValue<string>(DicomTag.SeriesInstanceUID, 0));
            return SaveDicomData(finalDir, dicomDataset, ".dcm");
        }



    }
}