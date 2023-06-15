namespace Rdmp.Dicom.Cache.Pipeline;

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
        StudyUid = studyUid;
    }

    public override bool Equals(object obj) => obj?.GetType()==typeof(StudyToFetch) && ((StudyToFetch)obj).StudyUid==StudyUid;

    public override int GetHashCode() => System.HashCode.Combine(StudyUid);
}