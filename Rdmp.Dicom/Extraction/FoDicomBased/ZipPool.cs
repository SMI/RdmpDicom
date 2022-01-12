using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Rdmp.Dicom.Extraction.FoDicomBased
{
    internal static class Zip
    {
        public static IArchiveEntry GetEntry(this IArchive archive, string name)
        {
            return archive.Entries.FirstOrDefault(e => e.Key.Equals(name));
        }
    }

    /// <summary>
    /// Holds multiple ZipArchive open at once to prevent repeated opening and closing
    /// </summary>
    public class ZipPool:IDisposable
    {
        public int CacheMisses { get; private set; }
        public int CacheHits { get; private set; }

        readonly object _lockDictionary = new();
        private readonly Dictionary<string, IArchive> _openZipFiles = new();    // No assuming FS is case-insensitive!

        /// <summary>
        /// The maximum number of zip files to allow to be open at once.  Defaults to 5
        /// </summary>
        public int MaxPoolSize { get; set; } = 5;

        public void Dispose()
        {
            ClearCache();
        }

        private void ClearCache()
        {
            lock(_lockDictionary)
            {
                foreach (var za in _openZipFiles.Values)
                    za.Dispose();

                _openZipFiles.Clear();
            }
        }

        public IArchive OpenRead(string zipPath)
        {
            lock (_lockDictionary)
            {
                var key = NormalizePath(zipPath);

                if (_openZipFiles.ContainsKey(key))
                {
                    CacheHits++;
                    return _openZipFiles[key];
                }

                if (_openZipFiles.Count >= MaxPoolSize)
                {
                    ClearCache();
                }

                var v = ArchiveFactory.Open(key);
                CacheMisses++;
                _openZipFiles.Add(key, v);
                return v;
            }
        }

        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(new Uri(path).LocalPath)
                       .TrimEnd('\\','/');
        }

    }
}
