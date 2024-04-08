using Rdmp.Core.Caching.Requests;
using System;

namespace Rdmp.Dicom.Cache;

public class SMIDataChunk : ICacheChunk
{
    public ICacheFetchRequest Request { get; }
    public string Modality { get; set; }
    public DateTime FetchDate { get; set; }
    public SMICacheLayout Layout { get; set; }

    public SMIDataChunk(ICacheFetchRequest request)
    {
        Request = request;
    }
}