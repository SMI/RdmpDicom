using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rdmp.Dicom.ExternalApis
{
    class SemEHRResponse
    {
        public bool success { get; set; }
        public int num_results { get; set; }
        public string message { get; set; } = "Message not set.";

        //public IList<SemneHDRReponseResult> results { get; set; }

        public IList<string> results { get; set; }

        /*public List<string> GetResultSopUids()
        {
            return (results.Select(t => t.sop_uid).ToList());
        }

        public List<string> GetResultStudyUids()
        {
            return (results.Select(t => t.study_uid).ToList());
        }

        public List<string> GetResultseriesUids()
        {
            return (results.Select(t => t.series_uid).ToList());
        }*/
    }

    /*class SemneHDRReponseResult
    {
        public string sop_uid { get; set; }
        public string study_uid { get; set; }
        public string series_uid { get; set; }
    }*/

}
