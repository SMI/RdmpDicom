using System.IO;
using FellowOakDicom;

namespace Rdmp.Dicom.Extraction.FoDicomBased.DirectoryDecisions
{
    public class PutInUidStudySeriesFolders : PutDicomFilesInExtractionDirectories
    {
        public override string PredictOutputPath(DirectoryInfo outputDirectory, string releaseIdentifier, string studyUid, string seriesUid, string sopUid)
        {
            if (string.IsNullOrWhiteSpace(releaseIdentifier) || string.IsNullOrWhiteSpace(studyUid) || string.IsNullOrWhiteSpace(seriesUid))
                return null;

            return base.PredictOutputPath(
                new DirectoryInfo(Path.Combine(outputDirectory.FullName, releaseIdentifier, studyUid,seriesUid)),
                releaseIdentifier, studyUid, seriesUid, sopUid);
        }
        protected override string WriteOutDatasetImpl(DirectoryInfo outputDirectory, string releaseIdentifier, DicomDataset dicomDataset)
        {

            var finalDir = SubDirectoryCreate(outputDirectory, releaseIdentifier);
            finalDir = SubDirectoryCreate(finalDir, dicomDataset.GetValue<string>(DicomTag.StudyInstanceUID, 0));
            finalDir = SubDirectoryCreate(finalDir, dicomDataset.GetValue<string>(DicomTag.SeriesInstanceUID, 0));
            return SaveDicomData(finalDir, dicomDataset);
        }



    }
}