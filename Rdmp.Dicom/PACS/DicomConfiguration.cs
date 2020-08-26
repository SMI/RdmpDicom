using System;
using Dicom.Network;

namespace Rdmp.Dicom.PACS
{


    public class DicomConfiguration
    {

        //ToDo Not ideal? -static factory?
        #region MakeUriUsePort
        public static Uri MakeUriUsePort(Uri uri, int? port)
        {
            //no explicit port specified
            if (!port.HasValue)
                return uri;

            var builder = new UriBuilder(uri)
            {
                Port = port.Value
            };
            return builder.Uri;
        }
        #endregion


        public Uri RemoteAetUri { get; set; }
        public string RemoteAetTitle { get; set; }
        public Uri LocalAetUri { get; set; }
        public string LocalAetTitle { get; set; }
        public int RequestCooldownInMilliseconds { get; set; }
        public int TransferCooldownInMilliseconds { get; set; }
        public int TransferPollingInMilliseconds { get; set; }
        public int TransferTimeOutInMilliseconds { get; set; }
        public DicomPriority Priority { get; set; }

    }
}