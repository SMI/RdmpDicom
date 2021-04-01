using Rdmp.Core.Curation.Data;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.DataFlowPipeline.Requirements;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.Progress;
using System;
using System.Data;
using System.IO;
using System.Xml;
using System.Xml.XPath;

namespace Rdmp.Dicom
{
    /// <summary>
    /// Source that reads an 'inventory file' which contains one or more directories. Directories
    /// should contain CFind xml formatted results e.g. as produced by dicom toolkit
    /// </summary>
    public class CFindDirSource : IPluginDataFlowSource<DataTable>, IPipelineRequirement<FlatFileToLoad>
    {
        private FlatFileToLoad _file;

        const string DefaultHeaders = "RetrieveAETitle,ModalitiesInStudy,StudyDescription,PatientID,TypeOfPatientID,StudyInstanceUID,StudyDate";


        [DemandsInitialization("Search pattern for locating CFind results in directories found", Mandatory = true, DefaultValue = "*.xml")]
        public string SearchPattern { get; set; } = "*.xml";


        [DemandsInitialization("Comma seperated list of dicom tags to read from the CFind results", Mandatory = true, DefaultValue = DefaultHeaders)]
        public string HeadersToRead { get; set; } = DefaultHeaders;

        public void Abort(IDataLoadEventListener listener)
        {
            
        }

        public void Check(ICheckNotifier notifier)
        {
            var dt = GenerateTable();

            if (dt.Columns.Count <= 0)
            {
                notifier.OnCheckPerformed(new CheckEventArgs($"Failed to build table.  Check {nameof(HeadersToRead)}", CheckResult.Fail));
            }   
            else
            {
                notifier.OnCheckPerformed(new CheckEventArgs($"Built table successfully", CheckResult.Success));
            }

            
        }

        public void Dispose(IDataLoadEventListener listener, Exception pipelineFailureExceptionIfAny)
        {
            
        }

        public DataTable GetChunk(IDataLoadEventListener listener, GracefulCancellationToken cancellationToken)
        {
            if (_file == null)
                throw new Exception("File has not been set");
            if (!_file.File.Exists)
                throw new FileNotFoundException("File did not exist:'" + _file.File.FullName + "'");

            var dt = GenerateTable();

            foreach(var f in File.ReadAllLines(_file.File.FullName))
            {
                if (string.IsNullOrWhiteSpace(f))
                    continue;

                ProcessDir(f, dt,listener);
            }

            return dt;
        }

        private DataTable GenerateTable()
        {
            var dt = new DataTable();

            foreach(var h in HeadersToRead.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                dt.Columns.Add(h);
            }

            return dt;
        }

        private void ProcessDir(string dir, DataTable dt, IDataLoadEventListener listener)
        {
            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, $"Starting '{dir}'"));

            if (File.Exists(dir))
            {
                // the inventory entry is a xml file directly :o
                XmlToRows(dir, dt, listener);
                return;
            }

            if (!Directory.Exists(dir))
            {
                listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, $"'{dir}' was not a Directory or File"));
                return;
            }

            var matches = Directory.GetFiles(dir, SearchPattern, SearchOption.AllDirectories);

            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, $"Found {matches.Length} CFind files in {dir}"));

            foreach(var file in matches)
            {
                XmlToRows(file, dt,listener);
            }
        }

        private void XmlToRows(string file, DataTable dt, IDataLoadEventListener listener)
        {
            using (var fileStream = File.Open(file, FileMode.Open))
            {
                //Load the file and create a navigator object. 
                var xDoc = new XmlDocument();
                xDoc.Load(fileStream);

                var datasets = xDoc.GetElementsByTagName("data-set");

                foreach(XmlElement d in datasets)
                {
                    var row = dt.NewRow();

                    foreach(XmlElement child in d.ChildNodes)
                    {
                        var name = child.GetAttribute("name");
                        if(dt.Columns.Contains(name))
                        {
                            row[name] = child.InnerText;
                        }
                    }

                    dt.Rows.Add(row);
                }

                
            }
        }

        public void PreInitialize(FlatFileToLoad value, IDataLoadEventListener listener)
        {
            _file = value;
        }

        public DataTable TryGetPreview()
        {
            return GetChunk(new ThrowImmediatelyDataLoadEventListener(), new GracefulCancellationToken());
        }
    }
}
