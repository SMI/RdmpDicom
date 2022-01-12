using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using Rdmp.Core.Caching.Layouts;
using Rdmp.Core.Curation.Data.DataLoad;
using ReusableLibraryCode.Progress;
using Rdmp.Core.Caching.Pipeline.Destinations;

namespace Rdmp.Dicom.Cache
{
    [Export(typeof(ICacheLayout))]
    public class SMICacheLayout : CacheLayout
    {
        public SMICacheLayout(DirectoryInfo cacheDirectory, SMICachePathResolver resolver): base(cacheDirectory, "yyyyMMddHH", CacheArchiveType.Zip, CacheFileGranularity.Hour, resolver)
        {
            
        }
        
        public void CreateArchive(DateTime archiveDate,IDataLoadEventListener listener, string extension)
        {
            var downloadDirectory = GetLoadCacheDirectory(listener);
            var dataFiles = downloadDirectory.EnumerateFiles(extension).ToArray();
            
            if (!dataFiles.Any())
                return;

            // todo: SMI needs ZipArchive.Create whereas SCI needs Update
            ArchiveFiles(dataFiles, archiveDate, listener);
            Cleanup(listener);
        }

        // archives files into <modality>/<year>/<month>/<day>/<date>.dcm
        public string GetArchiveFilepathForDate(DateTime archiveDate,IDataLoadEventListener listener)
        {
            var loadCacheDirectory = GetLoadCacheDirectory(listener);
            var yearDirectory = archiveDate.ToString("yyyy");
            var monthDirectory = archiveDate.ToString("MM");
            var dayDirectory = archiveDate.ToString("dd");
            var filename = $"{archiveDate.ToString(DateFormat)}.{ArchiveType.ToString().ToLower()}";

            return Path.Combine(loadCacheDirectory.FullName, yearDirectory, monthDirectory, dayDirectory, filename);
        }

        public void ValidateLayout()
        {
            // Ensure there are no files in the root directory
            if (RootDirectory.EnumerateFiles("*", SearchOption.TopDirectoryOnly).Any())
                throw new Exception("The imaging cache directory should not contain any files at its root.");

            // Ensure all directories are named after valid dates by filtering on those that don't parse exactly
            // todo: reinstate correct version after an actual layout is decided on!

            // Currently (30/7/15) the layout contains monthly folders in the root and day-hour zips inside (needs to be changed as is pretty crappy)
            /*
            const string rootSubdirectoryFormat = "yyyyMM";
            var invalidDirs = RootDirectory.EnumerateDirectories().Where(info =>
            {
                DateTime dt;
                return !DateTime.TryParseExact(info.Name, rootSubdirectoryFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt);
            }).ToList();

            if (invalidDirs.Any())
                throw new Exception("The following directories could not be parsed as valid " + rootSubdirectoryFormat +
                                    " dates: " + string.Join(", ", invalidDirs.Select(info => info.Name)));
            */
            // todo: could evaluate contents of subdirectories, but this layout will likely change so no point implementing for now
        }

        public void Cleanup(IDataLoadEventListener listener)
        {
            // removes all non-archive files from the download directory
            var downloadDirectory = GetLoadCacheDirectory(listener);
            var nonArchiveFiles = downloadDirectory.EnumerateFiles("*").Where(info => info.Extension.ToLower() != ("." + ArchiveType.ToString().ToLower()));
            nonArchiveFiles.ToList().ForEach(info => info.Delete());
        }

        public override Queue<DateTime> GetSortedDateQueue(IDataLoadEventListener listener)
        {
            // todo: This enumerates all files in the entire cache! Could be very expensive

            // This cache is laid out by <modality>/<year>/<month>/<day>/yyyyMMddHH.<type>
            var allFiles = GetLoadCacheDirectory(listener).EnumerateFiles($"*.{ArchiveType}", SearchOption.AllDirectories).ToList();
            var dateTimes = allFiles.Select(ConvertFilenameToDateTime).ToList();
            dateTimes.Sort((a, b) => a.CompareTo(b));
            return new Queue<DateTime>(dateTimes);
        }

        private DateTime ConvertFilenameToDateTime(FileInfo fileInfo)
        {
            var nameWithoutExtension = fileInfo.Name.Substring(0, fileInfo.Name.Length - fileInfo.Extension.Length);

            if (!DateTime.TryParseExact(nameWithoutExtension, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                throw new Exception(
                    $"Dodgy file in cache. Could not parse '{nameWithoutExtension}' using DateFormat={DateFormat} : {fileInfo.FullName}");

            return dt;
        }
    }
}