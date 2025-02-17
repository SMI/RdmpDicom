using FAnsi;
using FellowOakDicom;
using NPOI.SS.Formula.Functions;
using NUnit.Framework;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.DataExport.DataExtraction.Commands;
using Rdmp.Core.ReusableLibraryCode.Checks;
using Rdmp.Core.ReusableLibraryCode.Progress;
using Rdmp.Dicom.Extraction;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tests.Common;

namespace Rdmp.Dicom.Tests.Integration
{
    public class DicomTagToCSVTests : DatabaseTests
    {
        [OneTimeSetUp]
        public void Init()
        {
            TidyUpImages();
        }

        [TearDown]
        public void Dispose()
        {
            TidyUpImages();
        }
        private static void TidyUpImages()
        {
            var imagesDir = new DirectoryInfo(Path.Combine(TestContext.CurrentContext.WorkDirectory, "Images"));
            if (imagesDir.Exists)
                imagesDir.Delete(true);
        }

        [Test]
        public void TestTagExtraction()
        {
            var tagExtractor = new DicomTagToCSV();
            IExtractCommand cmd = MockExtractionCommand();
            //give the mock to anonymiser
            tagExtractor.PreInitialize(cmd, ThrowImmediatelyDataLoadEventListener.Quiet);
            tagExtractor.ArchiveRootIfAny = TestContext.CurrentContext.WorkDirectory;
            tagExtractor.RelativeArchiveColumnName = "Filepath";

            Dictionary<DicomTag, string> tags = new()
        {
            {DicomTag.PatientName,"Moscow"},
            {DicomTag.PatientBirthDate,"20010101"},
            {DicomTag.StudyDescription,"Frank has lots of problems, he lives at 60 Pancake road"},
            {DicomTag.SeriesDescription,"Coconuts"},
            {DicomTag.AlgorithmName,"Chessnuts"},
            {DicomTag.StudyDate,"20020101"}
        };


            var dicom = new DicomDataset
        {
            {DicomTag.SOPInstanceUID, "123.4.4"},
            {DicomTag.SeriesInstanceUID, "123.4.5"},
            {DicomTag.StudyInstanceUID, "123.4.6"},
            {DicomTag.SOPClassUID,"1"}
        };

            foreach (var (key, value) in tags)
                dicom.AddOrUpdate(key, value);

            dicom.AddOrUpdate(DicomTag.StudyDate, new DateTime(2002, 01, 01));

            var fi = new FileInfo(Path.Combine(TestContext.CurrentContext.WorkDirectory, "madness.dcm"));

            DicomFile df = new(dicom);
            df.Save(fi.FullName);

            using var dt = new DataTable();
            dt.Columns.Add("Filepath");
            dt.Columns.Add("SOPInstanceUID");
            dt.Columns.Add("SeriesInstanceUID");
            dt.Columns.Add("StudyInstanceUID");
            dt.Columns.Add("Pat");

            dt.Rows.Add(fi.Name, "123.4.4", "123.4.5", "123.4.6", "Hank");

            Assert.DoesNotThrow(()=>tagExtractor.ProcessPipelineData(dt, ThrowImmediatelyDataLoadEventListener.Quiet, new()));
            var path = cmd.GetExtractionDirectory().FullName;
            var tagFile = Path.Combine(path, "DicomTags.csv");
            Assert.That(File.Exists(tagFile), Is.True);
            var exportedTags= ConvertCSVtoDataTable(tagFile);
            Assert.That(exportedTags.Rows.Count, Is.EqualTo(10));

        }

        private static IExtractDatasetCommand MockExtractionCommand()
        {
            return new DummyExtractDatasetCommand(TestContext.CurrentContext.WorkDirectory, 100);
        }

        private static DataTable ConvertCSVtoDataTable(string strFilePath)
        {
            DataTable dt = new DataTable();
            using (StreamReader sr = new StreamReader(strFilePath))
            {
                string[] headers = sr.ReadLine().Split(',');
                foreach (string header in headers)
                {
                    dt.Columns.Add(header);
                }
                while (!sr.EndOfStream)
                {
                    string[] rows = sr.ReadLine().Split(',');
                    DataRow dr = dt.NewRow();
                    for (int i = 0; i < headers.Length; i++)
                    {
                        dr[i] = rows[i];
                    }
                    dt.Rows.Add(dr);
                }
            }
            return dt;
        }
    }
}
