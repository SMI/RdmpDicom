namespace Rdmp.Dicom.PipelineComponents.DicomSources
{
    public enum DataTooWideHandling
    {
        None,

        TruncateAndWarn,

        MarkCorrupt,

        ConvertToNullAndWarn
    }
}