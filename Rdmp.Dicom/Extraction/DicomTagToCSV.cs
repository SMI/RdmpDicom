using CsvHelper;
using CsvHelper.Configuration;
using FellowOakDicom;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.DataExport.DataExtraction.Commands;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.DataFlowPipeline.Requirements;
using Rdmp.Core.ReusableLibraryCode.Checks;
using Rdmp.Core.ReusableLibraryCode.Progress;
using Rdmp.Dicom.Extraction.FoDicomBased;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;

namespace Rdmp.Dicom.Extraction
{
    public class DicomTagToCSV : IPluginDataFlowComponent<DataTable>, IPipelineRequirement<IExtractCommand>
    {
        [DemandsInitialization("If the path filename contains relative file uris to images then this is the root directory")]
        public string ArchiveRootIfAny { get; set; }

        [DemandsInitialization("The column name in the extracted dataset which contains the location of the dicom files", Mandatory = true)]
        public string RelativeArchiveColumnName { get; set; }

        [DemandsInitialization("How many tries to allow for fetching the file. This setting may be useful on network drives or oversubscribed resources", DefaultValue = 0)]
        public int FileFetchRetryLimit { get; set; }

        [DemandsInitialization("How long to wait between file fetch retries in milliseconds.", DefaultValue = 100)]
        public int FileFetchRetryTimeout { get; set; }

        [DemandsInitialization("The number of errors (e.g. failed to find/anonymise file) to allow before abandoning the extraction", DefaultValue = 100)]
        public int ErrorThreshold { get; set; }

        private int _errors = 0;
        private IExtractDatasetCommand _extractCommand;

        private bool abort = false;

        public void Abort(IDataLoadEventListener listener)
        {
            abort = true;
        }

        public void Check(ICheckNotifier notifier)
        {
        }

        public void Dispose(IDataLoadEventListener listener, Exception pipelineFailureExceptionIfAny)
        {
        }

        public DataTable ProcessPipelineData(DataTable toProcess, IDataLoadEventListener listener, GracefulCancellationToken cancellationToken)
        {
            if (_extractCommand == null)
            {
                listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "Ignoring non dataset command "));
                return toProcess;
            }

            var dicomFiles = new List<(string, string)>();

            foreach (DataRow processRow in toProcess.Rows)
            {
                if (abort) return toProcess;
                if (_errors > 0 && _errors > ErrorThreshold)
                    throw new Exception($"Number of errors reported ({_errors}) reached the threshold ({ErrorThreshold})");

                var file = (string)processRow[RelativeArchiveColumnName];
                dicomFiles.Add((file, file));
            }


            var dicomFilePaths = new AmbiguousFilePath(ArchiveRootIfAny, dicomFiles).GetDataset(FileFetchRetryLimit, FileFetchRetryTimeout, listener);
            var destinationDirectory = new DirectoryInfo(Path.Combine(_extractCommand.GetExtractionDirectory().FullName));
            destinationDirectory.Create();
            var filepath = Path.Combine(destinationDirectory.FullName, "DicomTags.csv");

            using (var sw = new StreamWriter(filepath))
            {
                sw.WriteLine("Id,Name,Value");
                foreach (var dcm in dicomFilePaths)
                {
                    try
                    {
                        foreach (var record in DicomFile.Open(dcm.Item1, FileReadOption.ReadAll).Dataset.SelectMany(t => Entry.ProcessTag(dcm.Item1, t)))
                        {
                            sw.WriteLine($"{record.Id},{record.Name},{record.Value}");
                        }
                    }
                    catch (Exception e)
                    {
                        listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, $"Failed to extract tags from DICOM {dcm.Item1}", e));
                        _errors++;
                    }
                }
            }

            return toProcess;
        }

        internal class Entry
        {
            public string Id { get; }
            public string Name { get; }
            public string Value { get; }

            public Entry(string id, string name, string value)
            {
                Id = id;
                Name = name;
                Value = value;
            }

            public static IEnumerable<Entry> ProcessTag(string id, DicomItem item)
            {
                return item switch
                {
                    DicomAttributeTag aTag => aTag.Values.Select(v => new Entry(id, aTag.Tag.DictionaryEntry.Name, v.DictionaryEntry.Name)),
                    DicomStringElement s => StringEntries(id, s.Tag.DictionaryEntry.Name, s),
                    DicomSequence seq => seq.Items.SelectMany(ds => ds.SelectMany(i => ProcessTag(id, i))),
                    _ => new[] { new Entry(id, item.Tag.DictionaryEntry.Name, item.ToString()) }
                };
            }
            private static IEnumerable<Entry> StringEntries(string id, string tag, DicomStringElement e)
            {
                for (int i = 0; i < e.Count; i++)
                    yield return new Entry(id, tag, e.Get<string>(i));
            }
        }
        public void PreInitialize(IExtractCommand value, IDataLoadEventListener listener)
        {
            _extractCommand = value as IExtractDatasetCommand;
        }
    }


}
