namespace Rdmp.Dicom.PipelineComponents.DicomSources
{
    /// <summary>
    /// Determines how to treat invalid dicom tags data
    /// </summary>
    public enum InvalidDataHandling
    {
        /// <summary>
        ///  E.g. if a DecimalString cannot be turned into a decimal then throw an exception
        /// </summary>
        ThrowException,

        /// <summary>
        ///  E.g. if a DecimalString cannot be turned into a decimal store the cell value DBNull.Value instead
        /// </summary>
        ConvertToNullAndWarn,

        /// <summary>
        /// Entire image is marked as corrupt and Nacked/Not Loaded
        /// </summary>
        MarkCorrupt
    }
}