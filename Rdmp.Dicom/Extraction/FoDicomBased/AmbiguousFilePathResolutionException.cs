using System;

namespace Rdmp.Dicom.Extraction.FoDicomBased
{
    public class AmbiguousFilePathResolutionException : Exception
    {
        public AmbiguousFilePathResolutionException(string msg):base(msg)
        {
            
        }

        public AmbiguousFilePathResolutionException(string msg, Exception inner):base(msg,inner)
        {
            
        }
    }
}