using NUnit.Framework;
using Rdmp.Core.Curation.Data.Spontaneous;
using Rdmp.Core.DataExport.DataExtraction.Commands;
using Rdmp.Core.QueryBuilding;
using Rdmp.Core.ReusableLibraryCode.Progress;
using Rdmp.Dicom.Extraction.FoDicomBased.DirectoryDecisions;
using Rdmp.Dicom.Extraction.PixelAnonymisation;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Rdmp.Dicom.Tests.Integration;

public class DicomPixelAnonymiserTests
{

    [Test]
    public void TestPixelAnonymiser()
    {
        var anonymiser = new DicomPixelAnonymiser();
        anonymiser.RelativeArchiveColumnName = "dicomFile";
        anonymiser.PutterType = typeof(PutInRoot);
        anonymiser.PythonLocation = "C:\\Users\\jfriel001\\AppData\\Local\\Programs\\Python\\Python313\\Python313.dll";
        //    anonymiser.ImagesAlreadyInDestination = true;
        //    anonymiser.TesseractDataFolder = "C:\\temp\\tessdata";
        anonymiser.Language = "en";
        IExtractCommand cmd = MockExtractionCommand();

        var source = new CancellationTokenSource();
        //    //give the mock to anonymiser
        anonymiser.PreInitialize(cmd, ThrowImmediatelyDataLoadEventListener.Quiet);

        var dt = new DataTable();
        dt.Columns.Add("Pat");
        dt.Columns.Add("dicomFile");
        dt.Rows.Add(["1111111111", "C:\\temp\\dicoms\\002_with_ann.dcm\\dicom-00005.dcm"]);
        //    dt.Rows.Add(["1111111111", "C:\\temp\\dicoms\\002_with_ann.dcm\\dicom-00002.dcm"]);
        //    //dt.Rows.Add(["1111111111", "C:\\temp\\dicoms\\002_with_ann.dcm\\dicom-00003.dcm"]);
        //    //dt.Rows.Add(["1111111111", "C:\\temp\\dicoms\\002_with_ann.dcm\\dicom-00004.dcm"]);
        //    //dt.Rows.Add(["1111111111", "C:\\Users\\jfriel001\\Downloads\\I290.dcm"]);
        //    //dt.Rows.Add(["1111111111", "C:\\Users\\jfriel001\\Downloads\\sb.dcm"]);
        dt.Rows.Add(["1111111111", "C:\\Users\\jfriel001\\Downloads\\MF_-dicom-00001.dcm"]);
        dt.Rows.Add(["1111111111", "C:\\Users\\jfriel001\\Downloads\\ACUSON-24-YBR_FULL-RLE-b.dcm"]);
        dt.Rows.Add(["1111111111", "C:\\Users\\jfriel001\\Downloads\\form_example.png.dcm"]);

        anonymiser.ProcessPipelineData(dt, ThrowImmediatelyDataLoadEventListener.Quiet, new());
        //    Assert.That(1, Is.EqualTo(1));
    }

    private static IExtractDatasetCommand MockExtractionCommand()
    {
        return new DummyExtractDatasetCommand(TestContext.CurrentContext.WorkDirectory, 100);
    }
}
