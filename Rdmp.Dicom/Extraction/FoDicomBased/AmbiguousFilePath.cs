using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Dicom;
using Rdmp.Dicom.PACS;
using ReusableLibraryCode.Progress;

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
        
        private static readonly Regex _regexDigitsAndDotsOnly = new Regex(@"^[0-9\.]*$");



        public AmbiguousFilePath(string fullPath)
        {
            if(!IsAbsolute(fullPath))
                throw new ArgumentException("Relative path was encountered without specifying a root, if you want to process relative paths you will need to provide a root path too.  fullPath was '" + fullPath + "'",nameof(fullPath));

            FullPath = fullPath;
        }

        public AmbiguousFilePath(string root, string path)
        {
            //if root is provided but is not absolute
            if(!string.IsNullOrWhiteSpace(root) && !IsAbsolute(root))
                throw new ArgumentException("Specified root path '" + root + "' was not IsAbsolute", nameof(root));

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

        /// <summary>
        /// Reads the dataset at the referenced path.  Supports limited retries if your file system is unstable
        /// </summary>
        /// <param name="retryCount">Number of times to attempt the read again when encountering an Exception</param>
        /// <param name="retryDelay">Number of milliseconds to wait after encountering an Exception reading before trying</param>
        /// <param name="pool"></param>
        /// <param name="listener"></param>
        /// <returns></returns>
        public DicomFile GetDataset(int retryCount = 0, int retryDelay=100, ZipPool pool = null, IDataLoadEventListener listener = null)
        {
            if (IsZipReference(FullPath))
            {
                int attmept = 0;

                TryAgain:

                var bits = FullPath.Split('!');

                var zip = pool != null ? pool.OpenRead(bits[0]) : ZipFile.Open(bits[0], ZipArchiveMode.Read);

                try
                {
                    var entry = zip.GetEntry(bits[1]);

                    if (entry == null)
                    {
                        //Maybe user has formatted it dodgy
                        //e.g. \2015\3\18\2.25.177481563701402448825228719253578992342.dcm
                        string adjusted = bits[1].TrimStart('\\','/');

                        //if that doesn't work
                        if ((entry = zip.GetEntry(adjusted)) == null)
                        {
                            //try normalizing the slashes
                            adjusted = adjusted.Replace('\\','/');

                            //nope we just cannot get a legit path in this zip
                            if ((entry = zip.GetEntry(adjusted)) == null)
                                throw new AmbiguousFilePathResolutionException($"Could not find path '{bits[1]}' within zip archive '{bits[0]}'");
                        }

                        //we fixed it to something that actually exists so update our state that we don't make the same mistake again
                        FullPath = bits[0] + '!' + adjusted;
                    }
                        
                    if (!IsDicomReference(bits[1]))
                        throw new AmbiguousFilePathResolutionException("Path provided '" + FullPath + "' was to a zip file but not to a dicom file entry");

                    var buffer = ByteStreamHelper.ReadFully(entry.Open());

                    //todo: when GH-627 goes live we can use FileReadOption  https://github.com/fo-dicom/fo-dicom/blob/GH-627/DICOM/DicomFile.cs
                    //using (var memoryStream = new MemoryStream(buffer))
                    var memoryStream = new MemoryStream(buffer);

                    return DicomFile.Open(memoryStream);
                }
                catch(Exception ex)
                {
                    if (attmept < retryCount)
                    {
                        listener?.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning, $"Sleeping for {retryDelay}ms because of encountering Exception", ex));

                        Thread.Sleep(retryDelay);
                        attmept++;
                        goto TryAgain;
                    }
                    else 
                        throw;
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

        public static bool IsDicomReference(string fullPath)
        {
            if(string.IsNullOrWhiteSpace(fullPath))
                return false;

            var extension = Path.GetExtension(fullPath);


            return 
                string.IsNullOrWhiteSpace(extension) || 

                // The following is a valid dicom file name but looks like it has an extension .5323
                // 123.3221.23123.5325
                _regexDigitsAndDotsOnly.IsMatch(extension) ||
                extension.Equals(".dcm", StringComparison.CurrentCultureIgnoreCase);
        }

        public static bool IsZipReference(string path)
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
            return !string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path);
        }
    }
}
