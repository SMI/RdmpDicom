using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dicom;
using FAnsi.Discovery;
using ReusableLibraryCode.Progress;
using Rdmp.Dicom.PACS;
using Rdmp.Dicom.PipelineComponents.DicomSources.Worklists;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.DataFlowPipeline.Requirements;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Dicom.Extraction.FoDicomBased;

namespace Rdmp.Dicom.PipelineComponents.DicomSources
{
    /// <summary>
    /// Turns dicom files into DataTables by processing tags
    /// </summary>
    public class DicomFileCollectionSource : DicomSource, IPipelineRequirement<IDicomWorklist>
    {
        [DemandsInitialization("Number of threads to use to process files",defaultValue:1,mandatory:true)]
        public int ThreadCount { get; set; }
        
        [DemandsInitialization("The number of failed zip/dcm files to skip before throwing an Exception instead of just warnings", defaultValue: 100, mandatory: true)]
        public int ErrorThreshold { get; set; }

        private int _filesProcessedSoFar = 0;
        private int _totalErrors = 0;

        private IDicomFileWorklist _fileWorklist;
        
        //start recording performance
        Stopwatch _stopwatch = new Stopwatch();

        private IDataLoadEventListener _listener;

        private readonly ZipPool _zipPool = new ZipPool();

        public override DataTable GetChunk(IDataLoadEventListener listener, GracefulCancellationToken cancellationToken)
        {
            _listener = listener;

            if (_fileWorklist == null)
            {
                listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning, "Skipping component because _fileWorklist is null"));
                return null;
            }

            _stopwatch.Start();
            
            var dt = base.GetDataTable();

            try
            {
                AmbiguousFilePath file;
                DirectoryInfo directory;
                
                if (!_fileWorklist.GetNextFileOrDirectoryToProcess(out directory, out file))
                    return null;

                if(file != null && directory == null)
                    dt.TableName = QuerySyntaxHelper.MakeHeaderNameSensible(Path.GetFileNameWithoutExtension(file.FullPath));
                else if (directory != null)
                    dt.TableName = QuerySyntaxHelper.MakeHeaderNameSensible(Path.GetFileNameWithoutExtension(directory.Name));
                else
                    throw new Exception("Expected IDicomProcessListProvider to return either a DirectoryInfo or a FileInfo not both/neither");
                
                if(directory != null)
                {
                    ProcessDirectoryAsync(dt, directory, listener);
                    Task.WaitAll(tasks.ToArray());
                }
                else
                //Input is a single zip file
                if (file.FullPath.EndsWith(".zip"))
                {
                    ProcessZipArchive(dt, listener, file.FullPath);
                }
                else
                {
                    var df = file.GetDataset(_zipPool);
                    ProcessDataset(file.FullPath, df.Dataset, dt, listener);
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
            _listener.OnProgress(this, new ProgressEventArgs("Processing Files", new ProgressMeasurement(_filesProcessedSoFar, ProgressType.Records), _stopwatch.Elapsed));
        }

        private void ProcessZipArchive(DataTable dt, IDataLoadEventListener listener, string zipFileName)
        {
            var skippedEntries = 0;
            var corruptedEntries = 0;
            
            try
            {
                using (var archive = ZipFile.Open(zipFileName, ZipArchiveMode.Read))
                    foreach (var f in archive.Entries)
                    {
                        //it's not a dicom file!
                        if(!f.FullName.EndsWith(".dcm",StringComparison.CurrentCultureIgnoreCase))
                        {
                            skippedEntries++;
                            continue;
                        }
                        byte[] buffer = null;
                    
                        try
                        {
                            buffer = ByteStreamHelper.ReadFully(f.Open());
                            
                            using (var memoryStream = new MemoryStream(buffer))
                                    ProcessFile(memoryStream, dt, zipFileName + "!" + f.FullName, listener);
                        }
                        catch (Exception e)
                        {
                            corruptedEntries++;
                            RecordError("Zip entry '" + f.FullName +"'",e);

                            if (corruptedEntries > 3)
                            {
                                listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning, "Skipping the rest of '" + f.FullName + "'", e));
                                break;
                            }
                        }
                    }
            }
            catch (InvalidDataException e)
            {
                listener.OnNotify(this,new NotifyEventArgs(ProgressEventType.Warning, "Error processing zip file '" + zipFileName + "'", e));
            }
                
            if(skippedEntries>0)
                listener.OnNotify(this,new NotifyEventArgs(ProgressEventType.Warning, "Skipped '" + skippedEntries +"' in zip archive '" + zipFileName +"' because they did not have .dcm extensions"));

            UpdateProgressListeners();
        }

        private void RecordError(string filenameOrZipEntry, Exception exception)
        {                    
            _totalErrors ++;
            _listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning, filenameOrZipEntry + " could not be processed", exception));

            if (_totalErrors > ErrorThreshold)
                throw new Exception("Maximum number of errors reached (ErrorThreshold)", exception);
        }


        List<Task>  tasks = new List<Task>();
        object oTasksLock = new object();
        
        
        private void ProcessDirectoryAsync(DataTable dt,DirectoryInfo directoryInfo, IDataLoadEventListener listener)
        {
            bool tooManyRunningTasks;
            
            lock (oTasksLock)
                tooManyRunningTasks = tasks.Count(t => !t.IsCompleted) >= ThreadCount;

            //if the maximum number of tasks are alredy executing
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
            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "Started Directory '" + directoryInfo.FullName + "' on Thread " + Thread.CurrentThread.ManagedThreadId));

            FileInfo[] dicomFiles;
            FileInfo[] zipFiles;

            try
            {
                dicomFiles = directoryInfo.EnumerateFiles("*.dcm").ToArray();
                zipFiles = directoryInfo.EnumerateFiles("*.zip").ToArray();
            }
            catch (Exception e)
            {
                RecordError(directoryInfo.FullName,e);
                return;
            }

            //process all dcm files in current directory
            foreach (var dcmFile in dicomFiles)
                try
                {
                    using (var fs = dcmFile.OpenRead())
                        ProcessFile(fs, dt, dcmFile.FullName, listener);
                }
                catch (Exception e)
                {
                    RecordError(dcmFile.FullName,e);
                }

            foreach (var zipFile in zipFiles)
                ProcessZipArchive(dt, listener, zipFile.FullName);

            UpdateProgressListeners();
        }

        private void ProcessFile(Stream stream, DataTable dt, string filename, IDataLoadEventListener listener)
        {
            DicomFile file;
            
            try
            {
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

                    listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning, "Skipping file '" + filename + "' because DicomFile.Open returned null"));
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
                    new NotifyEventArgs(ProgressEventType.Warning, "Could not check IDicomProcessListProvider because it's was null (only valid at Design Time)"));
                return;
            }

            _fileWorklist = value as IDicomFileWorklist;
            
            if(_fileWorklist == null)
                listener.OnNotify(this,new NotifyEventArgs(ProgressEventType.Warning, "Expected IDicomWorklist to be of Type IDicomProcessListProvider (but it was " + value.GetType().Name + ").  This component will be skipped"));
        }
        
        public override DataTable TryGetPreview()
        {
            try
            {
                //todo timeout 10s
                return GetChunk(new ThrowImmediatelyDataLoadEventListener(), new GracefulCancellationToken());
            }
            finally
            {
                _stopwatch = new Stopwatch();
            }
        }

        public override void Dispose(IDataLoadEventListener listener, Exception pipelineFailureExceptionIfAny)
        {
            _zipPool?.Dispose();

            base.Dispose(listener, pipelineFailureExceptionIfAny);

        }
    }
}
