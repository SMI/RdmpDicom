﻿using System.IO;
using FellowOakDicom;

namespace Rdmp.Dicom.Extraction.FoDicomBased.DirectoryDecisions
{
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