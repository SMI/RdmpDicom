using System.Collections.Generic;

namespace Rdmp.Dicom.Cache.Pipeline
{
    public class StudyToFetch
    {
        /// <summary>
        /// The unique UID of the study that is to be fetched
        /// </summary>
        public string StudyUid {get; }

        /// <summary>
        /// The number of times this study has been reported as unavailable or errors have manifested downloading it during
        /// a single fetching session
        /// </summary>
        public int RetryCount {get;set;}

        public StudyToFetch(string studyUid)
        {
            this.StudyUid = studyUid;
        }

        public override bool Equals(object obj)
        {
            return obj is StudyToFetch fetch &&
                   StudyUid == fetch.StudyUid;
        }

        public override int GetHashCode()
        {
            return -1281949388 + EqualityComparer<string>.Default.GetHashCode(StudyUid);
        }
    }
}