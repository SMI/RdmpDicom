using System;
using System.Collections.Generic;
using FellowOakDicom;

namespace Rdmp.Dicom.Extraction
{
    public class UIDMapping
    {
        public string PrivateUID { get; set; }
        public string ReleaseUID { get; set; }
        public int ProjectNumber { get; set; }
        public UIDType UIDType { get; set; }
        public bool IsExternalReference { get; set; }

        public static Dictionary<DicomTag, UIDType> SupportedTags = new()
        {
            {DicomTag.SOPInstanceUID,UIDType.SOPInstanceUID},
            {DicomTag.SeriesInstanceUID,UIDType.SeriesInstanceUID},
            {DicomTag.StudyInstanceUID,UIDType.StudyInstanceUID},
            {DicomTag.FrameOfReferenceUID,UIDType.FrameOfReferenceUID},
            {DicomTag.MediaStorageSOPInstanceUID,UIDType.MediaStorageSOPInstanceUID},
        };

        public void SetUIDType(DicomTag tag)
        {
            if (SupportedTags.ContainsKey(tag))
                UIDType = SupportedTags[tag];
            else 
                throw new InvalidOperationException(
                    $"UIDMapping does not handle this tag type: {tag.DictionaryEntry.Keyword}");
        }
    }
}