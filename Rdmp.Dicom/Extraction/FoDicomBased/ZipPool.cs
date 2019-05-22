using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Rdmp.Dicom.Extraction.FoDicomBased
{
    /// <summary>
    /// Holds multiple ZipArchive open at once to prevent repeated opening and closing
    /// </summary>
    public class ZipPool:IDisposable
    {
        public int CacheMisses { get; private set; }
        public int CacheHits { get; private set; }

        readonly Dictionary<string, ZipArchive> _openZipFiles = new Dictionary<string, ZipArchive>();

        public void Dispose()
        {
            foreach (var za in _openZipFiles.Values)
                za.Dispose();
        }

        public ZipArchive OpenRead(string zipPath)
        {
            var key = NormalizePath(zipPath);

            if (_openZipFiles.ContainsKey(key))
            {
                CacheHits++;
                return _openZipFiles[key];
            }

            var v = ZipFile.OpenRead(key);
            CacheMisses++;
            _openZipFiles.Add(key,v);
            return v;
        }

        public static string NormalizePath(string path)
        {
            return Path.GetFullPath(new Uri(path).LocalPath)
                       .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                       .ToUpperInvariant();
        }

    }
}
