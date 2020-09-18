using Rdmp.Core.Caching.Layouts;
using System.IO;

namespace Rdmp.Dicom.Cache
{
    public class SMICachePathResolver : ILoadCachePathResolver
    {
        public string Modality { get; }

        public SMICachePathResolver(string modality)
        {
            Modality = modality;
        }

        public DirectoryInfo GetLoadCacheDirectory(DirectoryInfo rootDirectory)
        {
            var directoryInfo = new DirectoryInfo(Path.Combine(rootDirectory.FullName, Modality));
            return directoryInfo.Exists ? directoryInfo : Directory.CreateDirectory(directoryInfo.FullName);
        }
    }
}