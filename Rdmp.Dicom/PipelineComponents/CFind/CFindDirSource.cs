using Rdmp.Core.Curation.Data;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.DataFlowPipeline.Requirements;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.Progress;
using System;
using System.Data;
using System.IO;
using System.Xml;

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
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;

            using (var fileStream = File.OpenText(file))
            using (XmlReader reader = XmlReader.Create(fileStream, settings))
            {
                DataRow currentRow = null;
                int depth = -2;

                while (reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:

                            if (string.Equals(reader.Name, "data-set"))
                            {
                                // add old result
                                if (currentRow != null)
                                {
                                    dt.Rows.Add(currentRow);
                                    depth = -2;
                                }

                                currentRow = dt.NewRow();
                                depth = reader.Depth;
                            }

                            // if it's a dicom element
                            if (string.Equals(reader.Name, "element") && reader.Depth == depth+1 && reader.HasAttributes)
                            {
                                var elementName = reader.GetAttribute("name");

                                // if we want this tag
                                if (dt.Columns.Contains(elementName))
                                {
                                    currentRow[elementName] = reader.ReadElementContentAsString();
                                }
                            }
                            break;
                        case XmlNodeType.Text:
                            Console.WriteLine($"Inner Text: {reader.Value}");
                            break;
                        case XmlNodeType.EndElement:


                            if (string.Equals(reader.Name, "data-set"))
                            {
                                if (currentRow != null)
                                {
                                    dt.Rows.Add(currentRow);
                                }
                            }
                            break;
                        default:
                            Console.WriteLine($"Unknown: {reader.NodeType}");
                            break;
                    }
                }

                if (currentRow != null)
                {
                    dt.Rows.Add(currentRow);
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
