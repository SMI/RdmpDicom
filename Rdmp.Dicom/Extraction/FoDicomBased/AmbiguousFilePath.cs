using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using FellowOakDicom;
using Rdmp.Dicom.PACS;
using Rdmp.Core.ReusableLibraryCode.Progress;
using LibArchive.Net;
using Microsoft.IdentityModel.Tokens;

namespace Rdmp.Dicom.Extraction.FoDicomBased;

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
public partial class AmbiguousFilePath
{

    private readonly SortedDictionary<string, string> _fullPaths;
    private static readonly Regex RegexDigitsAndDotsOnly = DigitsAndDotsOnlyRegex();



    public AmbiguousFilePath(string fullPath) : this(null,fullPath)
    {
    }

    public AmbiguousFilePath(string root, IEnumerable<(string,string)> paths)
    {
        //if root is provided but is not absolute
        if(!string.IsNullOrWhiteSpace(root) && !IsAbsolute(root))
            throw new ArgumentException($"Specified root path '{root}' was not IsAbsolute", nameof(root));

        _fullPaths = new SortedDictionary<string, string>();
        paths.Select(p => (Combine(root, p.Item1),p.Item2)).Each(p=>_fullPaths.Add(p.Item1,p.Item2));
    }

    public AmbiguousFilePath(string fullPath, string fileName)
    {
        var absPath = Combine(fullPath, fileName);
        try
        {
            // If not a direct zip reference already, try to read as a whole zip:
            if (!absPath.Contains('!'))
            {
                _fullPaths = new SortedDictionary<string, string>();
                using var zip = new LibArchiveReader(absPath);
                zip.Entries().Each(e => _fullPaths.Add($"{absPath}!{e.Name}",$"{fileName}!{e.Name}"));
                return;
            }
        }
        catch
        {
            // Not a zip so ignore, treat as single file
        }
        _fullPaths = new SortedDictionary<string, string> { { absPath, fileName } };
    }

    private string Combine(string root, string path)
    {
        if (IsAbsolute(path))
            return path;

        if (!IsZipReference(path))
            return Path.Combine(root,path);

        var bits = path.Split('!');
        return $"{Path.Combine(root, bits[0])}!{bits[1]}";
    }


    /// <summary>
    /// Reads the dataset at the referenced path.  Supports limited retries if your file system is unstable
    /// </summary>
    /// <param name="retryCount">Number of times to attempt the read again when encountering an Exception</param>
    /// <param name="retryDelay">Number of milliseconds to wait after encountering an Exception reading before trying</param>
    /// <param name="listener"></param>
    /// <returns></returns>
    public IEnumerable<ValueTuple<string,DicomFile>> GetDataset(int retryCount = 0, int retryDelay=100, IDataLoadEventListener listener = null)
    {
        while (!_fullPaths.IsNullOrEmpty())
        {
            var entry = _fullPaths.First();
            if (!IsZipReference(entry.Key))
            {
                if (!IsDicomReference(entry.Key))
                    throw new AmbiguousFilePathResolutionException(
                        $"Path provided '{entry.Key}' was not to either an entry in a zip file or to a dicom file");
                _fullPaths.Remove(entry.Key);
                yield return new ValueTuple<string,DicomFile>(entry.Value,DicomFile.Open(entry.Key));
                continue;
            }

            var attempt = 0;
            // Can't 'yield return' directly from inside try/catch, so buffer:
            List<(string tag, DicomFile)> resultQueue = new();
            var bits = entry.Key.Split('!');
            TryAgain:
            try
            {
                var found = false;
                using var zip = new LibArchiveReader(bits[0]);
                foreach (var zipEntry in zip.Entries())
                {
                    var tryNames = new[]
                    {
                        $"{bits[0]}!{zipEntry.Name}",
                        $"{bits[0]}!/{zipEntry.Name}",
                        $"{bits[0]}!\\{zipEntry.Name}",
                        $"{bits[0]}!{zipEntry.Name.Replace('/','\\')}",
                        $"{bits[0]}!/{zipEntry.Name.Replace('/','\\')}",
                        $"{bits[0]}!\\{zipEntry.Name.Replace('/','\\')}",
                    };
                    foreach(var name in tryNames)
                        if (_fullPaths.TryGetValue(name,out var tag))
                        {
                            if (name.Equals(entry.Key))
                                found = true;
                            _fullPaths.Remove(name);
                            using var s = zipEntry.Stream;
                            var f = LoadStream(s);
                            if (f!=null)
                                resultQueue.Add((tag,f));
                        }
                }
                if (!found)
                    throw new AmbiguousFilePathResolutionException($"Could not find path '{bits[1]}' within zip archive '{bits[0]}'");
            }
            catch (Exception ex)
            {
                if (attempt >= retryCount)
                    throw;
                listener?.OnNotify(this,
                    new NotifyEventArgs(ProgressEventType.Warning,
                        $"Sleeping for {retryDelay}ms because of encountering Exception : {ex.Message} handling {bits[0]}", ex));
                Thread.Sleep(retryDelay);
                attempt++;
                goto TryAgain;
            }

            foreach (var r in resultQueue)
                yield return r;
        }
    }

    private static DicomFile LoadStream(Stream s)
    {
        try
        {
            using var ms = new MemoryStream(ByteStreamHelper.ReadFully(s));
            return DicomFile.Open(ms, FileReadOption.ReadAll);
        }
        catch (DicomFileException e)
        {
            Debug.WriteLine($"DICOM file rejected: {e}");
        }
        return null;
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
            RegexDigitsAndDotsOnly.IsMatch(extension) ||
            extension.Equals(".dcm", StringComparison.CurrentCultureIgnoreCase);
    }

    public static bool IsZipReference(string path)
    {
        return path.Count(c => c == '!') switch
        {
            0 => false,
            1 => true,
            _ => throw new Exception($"Path '{path}' had too many exclamation marks, expected 0 or 1")
        };
    }

    private bool IsAbsolute(string path)
    {
        return !string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path);
    }

    [GeneratedRegex("^[0-9\\.]*$")]
    private static partial Regex DigitsAndDotsOnlyRegex();
}