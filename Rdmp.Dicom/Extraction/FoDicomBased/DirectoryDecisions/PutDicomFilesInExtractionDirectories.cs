using System;
using System.IO;
using System.Text;
using Dicom;
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

        protected string SaveDicomData(DirectoryInfo outputDirectory,DicomDataset dicomDataset,string ext)
        {

            var extSb = new StringBuilder();
            if (!ext.IsBasicallyNull())
            {
                if (!ext.StartsWith("."))
                    extSb.Append(".");
                extSb.Append(ext);

            }
            var outPath = new FileInfo(Path.Combine(outputDirectory.FullName, dicomDataset.GetValue<string>(DicomTag.SOPInstanceUID, 0) + extSb));
            new DicomFile(dicomDataset).Save(outPath.FullName);
            return outPath.FullName;
        }
    }
}