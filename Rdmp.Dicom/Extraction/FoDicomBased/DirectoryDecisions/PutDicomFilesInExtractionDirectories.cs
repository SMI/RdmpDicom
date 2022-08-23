using System;
using System.IO;
using System.Text;
using FellowOakDicom;
using FAnsi.Extensions;

namespace Rdmp.Dicom.Extraction.FoDicomBased.DirectoryDecisions
{
    public abstract class PutDicomFilesInExtractionDirectories : IPutDicomFilesInExtractionDirectories
    {
        public string WriteOutDataset(DirectoryInfo outputDirectory, string releaseIdentifier, DicomDataset dicomDataset)
        {
            if(dicomDataset == null)
                throw new ArgumentNullException(nameof(dicomDataset));

            if(!outputDirectory.Exists)
                outputDirectory.Create();

            return WriteOutDatasetImpl(outputDirectory, releaseIdentifier, dicomDataset);
        }

        protected abstract string WriteOutDatasetImpl(DirectoryInfo outputDirectory, string releaseIdentifier,DicomDataset dicomDataset);

        protected DirectoryInfo SubDirectoryCreate(DirectoryInfo parent, string child)
        {
            var childDir = new DirectoryInfo(Path.Combine(parent.FullName, child));
            //If the directory already exists, this method does nothing.
            childDir.Create();
            return childDir;
        }

        protected string SaveDicomData(DirectoryInfo outputDirectory,DicomDataset dicomDataset)
        {
            var path = Path.Combine(outputDirectory.FullName, dicomDataset.GetValue<string>(DicomTag.SOPInstanceUID, 0));
            path = Path.ChangeExtension(path, ".dcm");

            var outPath = new FileInfo(path);
            new DicomFile(dicomDataset).Save(outPath.FullName);
            return outPath.FullName;
        }

        public virtual string PredictOutputPath(DirectoryInfo outputDirectory, string releaseIdentifier, string studyUid, string seriesUid, string sopUid)
        {
            if (string.IsNullOrWhiteSpace(sopUid))
                return null;

            var path = Path.Combine(outputDirectory.FullName, sopUid);
            path = Path.ChangeExtension(path, ".dcm");

            return new FileInfo(path).FullName;
        }
    }
}