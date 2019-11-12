using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Dicom;
using Rdmp.Core.Startup;
using Rdmp.Dicom.PACS;

namespace Rdmp.Dicom.Extraction.FoDicomBased
{
    /// <summary>
    /// A file path that might be relative or absolute or might be a path to a file in a zip file.  Zip boundary is ! e.g. C:\MassiveImageArchive\DOI\Bah.zip!000000.dcm
    /// 
    /// The following are examples of paths we might get given
    /// 
    /// C:\MassiveImageArchive\DOI\Bah.zip!000000.dcm
    /// C:\MassiveImageArchive\DOI\Calc-Test_P_00202_RIGHT_CC\1.3.6.1.4.1.9590.100.1.2.32737473111512049914158289192624777641\1.3.6.1.4.1.9590.100.1.2.178738814511944891404623704932278059093\000000.dcm
    /// C:\MassiveImageArchive\DOI\Calc-Test_P_00077_LEFT_CC\1.3.6.1.4.1.9590.100.1.2.171237778213286314004831596800955774428\1.3.6.1.4.1.9590.100.1.2.120762895113344405411143779050723940544\000000.dcm
    /// \Mass-Training_P_01983_LEFT_MLO_1\1.3.6.1.4.1.9590.100.1.2.315974405612087965807898763551341558217\1.3.6.1.4.1.9590.100.1.2.35571788212802531401890896782882200908\000000.dcm
    ///
    ///
    /// Linux paths must follow the following rules:
    ///
    /// Absolute path:
    ///     Anything starting with '/'
    ///
    /// Relative path:
    ///     Anything not starting with '/' e.g. "./series1/1.dcm"
    /// 
    /// </summary>
    public class AmbiguousFilePath
    {
        public string FullPath { get; private set; }

        public AmbiguousFilePath(string fullPath)
        {
            if(!IsAbsolute(fullPath))
                throw new ArgumentException("Relative path was encountered without specifying a root, if you want to process relative paths you will need to provide a root path too.  fullPath was '" + fullPath + "'","fullPath");

            FullPath = fullPath;
        }

        public AmbiguousFilePath(string root, string path)
        {
            //if root is provided but is not absolute
            if(!string.IsNullOrWhiteSpace(root) && !IsAbsolute(root))
                throw new ArgumentException("Specified root path '" + root + "' was not IsAbsolute", "root");

            FullPath = Combine(root, path);
        }

        private string Combine(string root, string path)
        {
            if (IsAbsolute(path))
                return path;

            if (!IsZipReference(path))
                return Path.Combine(root,path);
            
            var bits = path.Split('!');
            return Path.Combine(root,bits[0]) + '!' + bits[1];
        }
        

        public DicomFile GetDataset(ZipPool pool = null)
        {
            if (IsZipReference(FullPath))
            {
                var bits = FullPath.Split('!');

                var zip = pool != null ? pool.OpenRead(bits[0]) : ZipFile.Open(bits[0], ZipArchiveMode.Read);

                try
                {
                    var entry = zip.GetEntry(bits[1]);

                    if (!IsDicomReference(bits[1]))
                        throw new AmbiguousFilePathResolutionException("Path provided '" + FullPath + "' was to a zip file but not to a dicom file entry");

                    var buffer = ByteStreamHelper.ReadFully(entry.Open());

                    //todo: when GH-627 goes live we can use FileReadOption  https://github.com/fo-dicom/fo-dicom/blob/GH-627/DICOM/DicomFile.cs
                    //using (var memoryStream = new MemoryStream(buffer))
                    var memoryStream = new MemoryStream(buffer);

                    return DicomFile.Open(memoryStream);
                }
                finally
                {
                    if(pool == null)
                        zip.Dispose();
                }
            }

            if(!IsDicomReference(FullPath))
                throw new AmbiguousFilePathResolutionException("Path provided '" + FullPath + "' was not to either an entry in a zip file or to a dicom file");

            return DicomFile.Open(FullPath);
        }

        private bool IsDicomReference(string fullPath)
        {
            if(string.IsNullOrWhiteSpace(fullPath))
                return false;

            return fullPath.EndsWith(".dcm", StringComparison.CurrentCultureIgnoreCase);
        }

        private bool IsZipReference(string path)
        {
            switch (path.Count(c=>c=='!'))
            {
                case 0: return false;
                case 1: return true;
                default: throw new Exception("Path '" + path + "' had too many exclamation marks, expected 0 or 1");
            }
        }

        private bool IsAbsolute(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            return Path.IsPathRooted(path);
        }
    }
}
