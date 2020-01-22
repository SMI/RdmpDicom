using Rdmp.Core.DataFlowPipeline.Requirements;
using System;
using System.IO;
using System.Linq;
using Rdmp.Dicom.Extraction.FoDicomBased;

namespace Rdmp.Dicom.PipelineComponents.DicomSources.Worklists
{
    public class FlatFileToLoadDicomFileWorklist : IDicomFileWorklist
    {
        private readonly FlatFileToLoad _file;

        private string[] _lines;
        private int _linesCurrent;
        private bool _dataExhausted = false;

        public FlatFileToLoadDicomFileWorklist(FlatFileToLoad file)
        {
            _file = file;
            
            if(file.File == null)
                return;
                
            //input is a textual list of files/zips
            if (file.File.Extension == ".txt")
                if (_lines == null)
                {
                    _lines = File.ReadAllLines(file.File.FullName).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
                    _linesCurrent = 0;
                }
            
        }
        
        public bool GetNextFileOrDirectoryToProcess(out DirectoryInfo directory, out AmbiguousFilePath file)
        {
            file = null;
            directory = null;

            if (_dataExhausted)
                return false;
            
            //input is a single dicom file/zip
            if(_lines == null)
            {
                _dataExhausted = true;

                file = new AmbiguousFilePath(_file.File.FullName);
                return true;
            }

            //input was a text file full of other things to load
            if(_linesCurrent < _lines.Length)
            {
                var line = _lines[_linesCurrent];

                if (File.Exists(line.Trim()))
                {
                    _linesCurrent++;
                    file = new AmbiguousFilePath(new FileInfo(line.Trim()).FullName);
                    return true;
                }
                    
                if (Directory.Exists(line.Trim()))
                {
                    _linesCurrent++;
                    directory = new DirectoryInfo(line);
                    return true;
                }

                if (AmbiguousFilePath.IsZipReference(line))
                {
                    _linesCurrent++;
                    file = new AmbiguousFilePath(line);
                    return true;
                }

                throw new Exception("Text file '" + _file.File.Name +"' contained a line that was neither a File or a Directory:'" + line + "'");
            }

            _dataExhausted = true;
            return false;
        }
    }
}