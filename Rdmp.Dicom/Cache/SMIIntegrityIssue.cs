using System;

namespace Rdmp.Dicom.Cache;

public class SMIIntegrityIssue
{
    public string SeriesUID { get; set; }
    public int ExpectedInstances { get; set; }
    public int ActualInstances { get; set; }
    public DateTime ChunkDate { get; set; }
    public DateTime RequestDate { get; set; }
    public string Modality { get; set; }

    public SMIIntegrityIssue()
    {
        RequestDate = DateTime.Now;
    }
}