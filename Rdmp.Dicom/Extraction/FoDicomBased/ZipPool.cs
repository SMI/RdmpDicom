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

        readonly object lockDictionary = new();
        readonly Dictionary<string, ZipArchive> _openZipFiles = new(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// The maximum number of zip files to allow to be open at once.  Defaults to 100
        /// </summary>
        public int MaxPoolSize { get; set; } = 100;

        public void Dispose()
        {
            ClearCache();
        }

        private void ClearCache()
        {
            lock(lockDictionary)
            {
                foreach (var za in _openZipFiles.Values)
                    za.Dispose();

                _openZipFiles.Clear();
            }
        }

        public ZipArchive OpenRead(string zipPath)
        {
            if(_openZipFiles.Count >= MaxPoolSize)
            {
                ClearCache();
            }

            lock (lockDictionary)
            {
                var key = NormalizePath(zipPath);

                if (_openZipFiles.ContainsKey(key))
                {
                    CacheHits++;
                    return _openZipFiles[key];
                }

                var v = ZipFile.OpenRead(key);
                CacheMisses++;
                _openZipFiles.Add(key, v);
                return v;
            }
        }

        public static string NormalizePath(string path)
        {
            return Path.GetFullPath(new Uri(path).LocalPath)
                       .TrimEnd('\\','/');
        }

    }
}
