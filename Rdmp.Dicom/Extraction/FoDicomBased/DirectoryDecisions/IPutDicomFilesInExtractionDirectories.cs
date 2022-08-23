using System.ComponentModel.Composition;
using System.IO;
using FellowOakDicom;

namespace Rdmp.Dicom.Extraction.FoDicomBased.DirectoryDecisions
{
    [InheritedExport(typeof(IPutDicomFilesInExtractionDirectories))]
    public interface IPutDicomFilesInExtractionDirectories
    {
        /// <summary>
        /// If running in <see cref="FoDicomAnonymiser.MetadataOnly"/> mode (no access to underlying files) then
        /// return what path you WOULD use for outputting the image.  Note that depending on the tags in the data
        /// table being extracted some of these may be null (e.g. if SOPInstanceUID is not part of the extracted metadata).
        /// If this is required to calculate output path then return null;
        /// </summary>
        /// <param name="releaseIdentifier"></param>
        /// <param name="studyUid"></param>
        /// <param name="seriesUid"></param>
        /// <param name="sopUid"></param>
        /// <returns></returns>
        string PredictOutputPath(DirectoryInfo outputDirectory, string releaseIdentifier, string studyUid, string seriesUid, string sopUid);
        string WriteOutDataset(DirectoryInfo outputDirectory, string releaseIdentifier, DicomDataset dicomDataset);
    }
}