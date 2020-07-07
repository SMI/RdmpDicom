using Dicom;
using Dicom.Network;
using System.Collections.Generic;

namespace Rdmp.Dicom.Cache.Pipeline.Ordering
{
    class Item
    {
        public static readonly char separator = '-';

        public string PatientId { get; private set; }

        public string StudyInstanceUID { get; private set; }
        public string SeriesInstanceUID { get; private set; }
        public string SOPInstanceUID { get; private set; }
        public bool IsRequested { get; private set; }
        public bool IsFilled { get; private set; }
        
        public Item(string patientId, string studyUid, string seriesUid, string sopInstanceUid)
        {
            PatientId = patientId;
           StudyInstanceUID = studyUid;
            SeriesInstanceUID = seriesUid;
            SOPInstanceUID = sopInstanceUid;
        }

        //TODO discuss construtor chaining with Thomas
        public Item(DicomCStoreRequest storeRequest) :  this(
            storeRequest.Dataset.GetSingleValue<string>(DicomTag.PatientID),
            storeRequest.Dataset.GetSingleValue<string>(DicomTag.StudyInstanceUID),
            storeRequest.Dataset.GetSingleValue<string>(DicomTag.SeriesInstanceUID),
            storeRequest.Dataset.GetSingleValue<string>(DicomTag.SOPInstanceUID)
            )
        {
            
        }


        public static string MakeKeyFilter(Item item,OrderLevel orderLevel)
        {
            return MakeKeyFilter(item.PatientId, item.StudyInstanceUID, item.SeriesInstanceUID, item.SOPInstanceUID,orderLevel);
        }

        public static string MakeKeyFilter(string patId, string studyUid, string seriesUid, string sopInstance, OrderLevel orderLevel)
        {
            switch (orderLevel)
            {
                case OrderLevel.Patient:
                    return patId;
                //break;
                case OrderLevel.Study:
                    return patId + separator + studyUid;
                //break;
                case OrderLevel.Series:
                    return patId + separator + studyUid + separator + seriesUid;
                //break;
                case OrderLevel.Image:
                    return patId + separator + studyUid + separator + seriesUid + separator + sopInstance;
                default:
//                    Listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, "DicomRetriever.getDicomCMoveRequest Attempt to request item at unsupported level: " + orderLevel));
                    return null;

            }

        }

        public static string MakeItemKey(Item item)
        {
            return MakeItemKey(item.PatientId, item.StudyInstanceUID, item.SeriesInstanceUID, item.SOPInstanceUID);
        }

        public static string MakeItemKey(string patId, string studyUid, string seriesUid, string sopInstance)
        {
            return patId + separator + studyUid + separator + seriesUid + separator + sopInstance;
        }

        public void Fill()
        {
            IsFilled = true;
        }
        
        

        public void Request()
        {
            IsRequested=true;
        }

        public override bool Equals(object obj)
        {
            return obj is Item item &&
                   PatientId == item.PatientId &&
                   StudyInstanceUID == item.StudyInstanceUID &&
                   SeriesInstanceUID == item.SeriesInstanceUID &&
                   SOPInstanceUID == item.SOPInstanceUID;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = -916670253;
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(PatientId);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(StudyInstanceUID);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(SeriesInstanceUID);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(SOPInstanceUID);
                return hashCode;
            }
        }
    }
}
