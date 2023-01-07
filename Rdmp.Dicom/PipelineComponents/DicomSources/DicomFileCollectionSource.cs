using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FellowOakDicom;
using FAnsi.Discovery;
using LibArchive.Net;
using ReusableLibraryCode.Progress;
using Rdmp.Dicom.PipelineComponents.DicomSources.Worklists;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.DataFlowPipeline.Requirements;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Dicom.Extraction.FoDicomBased;
using Rdmp.Dicom.PACS;

namespace Rdmp.Dicom.PipelineComponents.DicomSources;

/// <summary>
/// Turns dicom files into DataTables by processing tags
/// </summary>
public class DicomFileCollectionSource : DicomSource, IPipelineRequirement<IDicomWorklist>
{
    [DemandsInitialization("Number of threads to use to process files",defaultValue:1,mandatory:true)]
    public int ThreadCount { get; set; }
        
    [DemandsInitialization("The number of failed zip/dcm files to skip before throwing an Exception instead of just warnings", defaultValue: 100, mandatory: true)]
    public int ErrorThreshold { get; set; }

    [DemandsInitialization("Number of times to attempt the read again when encountering an Exception", DefaultValue = 0)]
    public int RetryCount { get; set; }

    [DemandsInitialization("Number of milliseconds to wait after encountering an Exception reading before trying", DefaultValue = 100)]
    public int RetryDelay { get; set; }


    private int _filesProcessedSoFar = 0;
    private int _totalErrors = 0;

    private IDicomFileWorklist _fileWorklist;
        
    //start recording performance
    Stopwatch _stopwatch = new();

    private IDataLoadEventListener _listener;

    private readonly ZipPool _zipPool = new();

    public override DataTable GetChunk(IDataLoadEventListener listener, GracefulCancellationToken cancellationToken)
    {
        _listener = listener;

        if (_fileWorklist == null)
        {
            listener.OnNotify(this, new(ProgressEventType.Warning, "Skipping component because _fileWorklist is null"));
            return null;
        }

        _stopwatch.Start();
            
        var dt = GetDataTable();

        try
        {
            if (!_fileWorklist.GetNextFileOrDirectoryToProcess(out var directory, out var file))
                return null;

            // Exactly one of file/directory must be null:
            if ((file!=null) == (directory!=null))
                throw new("Expected IDicomProcessListProvider to return either a DirectoryInfo or a FileInfo not both/neither");

            if (file != null)
            {
                foreach (var df in file.GetDataset(RetryCount,RetryDelay,listener))
                {
                    dt.TableName = QuerySyntaxHelper.MakeHeaderNameSensible(Path.GetFileNameWithoutExtension(df.Item1));
                    Interlocked.Increment(ref _filesProcessedSoFar);
                    ProcessDataset(df.Item1,df.Item2.Dataset,dt,listener);
                }
            }

            if (directory!=null)
            {
                // Processing a directory
                dt.TableName = QuerySyntaxHelper.MakeHeaderNameSensible(Path.GetFileNameWithoutExtension(directory.Name));
                ProcessDirectoryAsync(dt, directory, listener);
                Task.WaitAll(tasks.ToArray());
            }

        }
        finally
        {
            //stop recording performance
            _stopwatch.Stop();

            //let people know how far through we are
            UpdateProgressListeners();
        }

        return dt;
    }

    private void UpdateProgressListeners()
    {
        _listener.OnProgress(this, new("Processing Files", new(_filesProcessedSoFar, ProgressType.Records), _stopwatch.Elapsed));
    }

    private void ProcessZipArchive(DataTable dt, string zipFileName, IDataLoadEventListener listener)
    {
        var skippedEntries = 0;
        var corruptedEntries = 0;
            
        try
        {
            using var archive=new LibArchiveReader(zipFileName);
            foreach (var f in archive.Entries())
            {
                //it's not a dicom file!
                if (!AmbiguousFilePath.IsDicomReference(f.Name))
                {
                    skippedEntries++;
                    continue;
                }

                try
                {
                    var stream = new MemoryStream(ByteStreamHelper.ReadFully(f.Stream));
                    ProcessFile(stream, dt, $"{zipFileName}!{f.Name}", listener);
                }
                catch (Exception e)
                {
                    corruptedEntries++;
                    RecordError($"Zip entry '{f.Name}'", e);

                    if (corruptedEntries <= 3) continue;
                    listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning,
                        $"Skipping the rest of '{zipFileName}'", e));
                    break;
                }
            }
        }
        catch (InvalidDataException e)
        {
            listener.OnNotify(this,new(ProgressEventType.Warning,
                $"Error processing zip file '{zipFileName}'", e));
        }
                
        if(skippedEntries>0)
            listener.OnNotify(this,new(ProgressEventType.Warning,
                $"Skipped '{skippedEntries}' in zip archive '{zipFileName}' because they did not have .dcm extensions"));

        UpdateProgressListeners();
    }

    private void RecordError(string filenameOrZipEntry, Exception exception)
    {                    
        _totalErrors ++;
        _listener.OnNotify(this, new(ProgressEventType.Warning,
            $"{filenameOrZipEntry} could not be processed", exception));

        if (_totalErrors > ErrorThreshold)
            throw new("Maximum number of errors reached (ErrorThreshold)", exception);
    }


    List<Task>  tasks = new();
    readonly object oTasksLock = new();
        
        
    private void ProcessDirectoryAsync(DataTable dt,DirectoryInfo directoryInfo, IDataLoadEventListener listener)
    {
        bool tooManyRunningTasks;
            
        lock (oTasksLock)
            tooManyRunningTasks = tasks.Count(t => !t.IsCompleted) >= ThreadCount;

        //if the maximum number of tasks are already executing
        if(tooManyRunningTasks)
            Task.WaitAll(tasks.ToArray());
            
        lock (oTasksLock)
            tasks = tasks.Where(t=>!t.IsCompleted).ToList();
            
        //start asynchronous processing of this directory
        var newT = Task.Run(() => ProcessDirectory(dt, directoryInfo, listener));
        tasks.Add(newT);
            
        //but then continue to process the subdirectories 
        DirectoryInfo[] directories;
        try
        {
            directories = directoryInfo.EnumerateDirectories().ToArray();
        }
        catch (Exception e)
        {
            RecordError(directoryInfo.FullName,e);
            return;
        }

        //process all subdirectories
        foreach (var subDir in directories)
            ProcessDirectoryAsync(dt, subDir, listener);
    }

    private void ProcessDirectory(DataTable dt, DirectoryInfo directoryInfo,IDataLoadEventListener listener)
    {
        listener.OnNotify(this, new(ProgressEventType.Information,
            $"Started Directory '{directoryInfo.FullName}' on Thread {Environment.CurrentManagedThreadId}"));

        //process all dcm files and archives in current directory
        foreach (var file in directoryInfo.EnumerateFiles())
        {
            try
            {
                if (!AmbiguousFilePath.IsDicomReference(file.FullName))
                {
                    ProcessZipArchive(dt, file.FullName, listener);
                    continue;
                }
                using var fs = file.OpenRead();
                ProcessFile(fs,dt,file.FullName,listener);
            }
            catch (Exception e)
            {
                RecordError(file.FullName, e);
            }
        }
        UpdateProgressListeners();
    }

    private void ProcessFile(Stream stream, DataTable dt, string filename, IDataLoadEventListener listener)
    {
        try
        {
            DicomFile file;
            try
            {
                Interlocked.Increment(ref _filesProcessedSoFar);
                file = DicomFile.Open(stream);
            }
            catch (Exception e)
            {
                RecordError(filename,e);
                return;
            }

            if(file == null)
            {

                listener.OnNotify(this, new(ProgressEventType.Warning,
                    $"Skipping file '{filename}' because DicomFile.Open returned null"));
                return;
            }

            var ds = file.Dataset;
            ProcessDataset(filename,ds,dt,listener);
                
        }
        finally
        {
            stream.Dispose();
        }
    }

    public void PreInitialize(IDicomWorklist value, IDataLoadEventListener listener)
    {
        if (value == null)
        {
            listener.OnNotify(this,
                new(ProgressEventType.Warning, "Could not check IDicomProcessListProvider because it was null (only valid at Design Time)"));
            return;
        }

        _fileWorklist = value as IDicomFileWorklist;
            
        if(_fileWorklist == null)
            listener.OnNotify(this,new(ProgressEventType.Warning,
                $"Expected IDicomWorklist to be of Type IDicomProcessListProvider (but it was {value.GetType().Name}).  This component will be skipped"));
    }
        
    public override DataTable TryGetPreview()
    {
        try
        {
            //todo timeout 10s
            return GetChunk(new ThrowImmediatelyDataLoadEventListener(), new());
        }
        finally
        {
            _stopwatch = new();
        }
    }

    public override void Dispose(IDataLoadEventListener listener, Exception pipelineFailureExceptionIfAny)
    {
        _zipPool?.Dispose();

        base.Dispose(listener, pipelineFailureExceptionIfAny);

    }
}
