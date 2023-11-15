using System.IO;
using FellowOakDicom;

namespace Rdmp.Dicom.Extraction.FoDicomBased.DirectoryDecisions;

public class PutInReleaseIdentifierSubfolders : PutDicomFilesInExtractionDirectories
{
    public override string PredictOutputPath(DirectoryInfo outputDirectory, string releaseIdentifier, string studyUid, string seriesUid, string sopUid)
    {
        if (string.IsNullOrWhiteSpace(releaseIdentifier))
            return null;

        return base.PredictOutputPath(
            new DirectoryInfo(Path.Combine(outputDirectory.FullName, releaseIdentifier)),
            releaseIdentifier, studyUid, seriesUid, sopUid);
    }

    protected override string WriteOutDatasetImpl(DirectoryInfo outputDirectory, string releaseIdentifier, DicomDataset dicomDataset)
    {
        var patientDir = SubDirectoryCreate(outputDirectory, releaseIdentifier);
        return SaveDicomData(patientDir, dicomDataset);
    }
}