using System.Collections.Generic;

namespace Rdmp.Dicom.ExternalApis;

internal class SemEHRResponse
{
    public bool success { get; set; }
    public int num_results { get; set; }
    public string message { get; set; } = "Message not set.";

    public IList<string> results { get; set; }

    /*public List<string> GetResultSopUids()
    {
        return (results.Select(t => t.sop_uid).ToList());
    }

    public List<string> GetResultStudyUids()
    {
        return (results.Select(t => t.study_uid).ToList());
    }

    public List<string> GetResultSeriesUids()
    {
        return (results.Select(t => t.series_uid).ToList());
    }*/
}

/*class HDRResponseResult
{
    public string sop_uid { get; set; }
    public string study_uid { get; set; }
    public string series_uid { get; set; }
}*/