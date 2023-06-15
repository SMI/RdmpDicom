using FellowOakDicom;
using FellowOakDicom.Network;
using Rdmp.Core.ReusableLibraryCode.Progress;
using Rdmp.Dicom.Cache.Pipeline.Dicom;
using Rdmp.Core.Caching.Requests;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.Curation;
using CsvHelper;
using System.IO;
using System.Globalization;
using System.Threading;
using DicomTypeTranslation;
using FellowOakDicom.Network.Client;

namespace Rdmp.Dicom.Cache.Pipeline;

/// <summary>
/// Queries a remote PACS and downloads metadata into CSV ready for loading to a relational database.  This component does not fetch actual images
/// Combine this with <see cref="SMICacheDestination"/> but make sure to set the <see cref="SMICacheDestination.Extension"/> to "*.csv"
/// </summary>
public class CFindSource : SMICacheSource
{
    private readonly DicomTag[] _tagsToWrite = {
        DicomTag.StudyInstanceUID,
        DicomTag.PatientID,
        DicomTag.StudyDate,
        DicomTag.StudyTime,
        DicomTag.StudyDescription,
        DicomTag.ModalitiesInStudy
    };

    public override SMIDataChunk DoGetChunk(ICacheFetchRequest cacheRequest, IDataLoadEventListener listener,GracefulCancellationToken cancellationToken)
    {
        listener.OnNotify(this,new(ProgressEventType.Information,$"CFindSource version is {typeof(CFindSource).Assembly.GetName().Version}.  Assembly is {typeof(PACSSource).Assembly} " ));
        listener.OnNotify(this,new(ProgressEventType.Information,$"Fo-Dicom version is {typeof(DicomClient).Assembly.GetName().Version}.  Assembly is {typeof(DicomClient).Assembly} " ));

        var dicomConfiguration = GetConfiguration();
        var requestSender = new DicomRequestSender(dicomConfiguration, listener,true);
        var dateFrom = Request.Start;
        var dateTo = Request.End;
        CachingSCP.LocalAet = LocalAETitle;
        CachingSCP.Listener = listener;


        if (PatientIdWhitelistColumnInfo != null && !IgnoreWhiteList)
            GetWhitelist(listener);

        //temp dir
        var cacheDir = new LoadDirectory(Request.CacheProgress.LoadProgress.LoadMetadata.LocationOfFlatFiles).Cache;
        var cacheLayout = new SMICacheLayout(cacheDir, new(Modality));
            
        Chunk = new(Request)
        {
            FetchDate = dateFrom,
            Modality = Modality,
            Layout = cacheLayout
        };

        // Create filepath for the results of the C-Find
        var workingDirectory = cacheLayout.GetLoadCacheDirectory(listener);
        var filename = $"{dateFrom:yyyyMMddhhmmss}.csv";
        var filepath = Path.Combine(workingDirectory.FullName, filename);

        var sw = new StreamWriter(filepath);
        using var writer = new CsvWriter(sw,CultureInfo.CurrentCulture);

        WriteHeaders(writer);

        var client = DicomClientFactory.Create(dicomConfiguration.RemoteAetUri.Host,
            dicomConfiguration.RemoteAetUri.Port, false, dicomConfiguration.LocalAetTitle,
            dicomConfiguration.RemoteAetTitle);

        // Find a list of studies
        #region Query

        listener.OnNotify(this,
            new(ProgressEventType.Information,
                $"Requesting Studies from {dateFrom} to {dateTo}"));
        var responses = 0;

        var request = CreateStudyRequestByDateRangeForModality(dateFrom, dateTo, Modality);
        request.OnResponseReceived += (req, response) =>
        {
            if (!Filter(Whitelist, response)) return;
            Interlocked.Increment(ref responses);
            WriteResult(writer,response);

        };
        requestSender.ThrottleRequest(request,client, cancellationToken.AbortToken);
        listener.OnNotify(this,
            new(ProgressEventType.Debug,
                $"Total filtered studies for {dateFrom} to {dateTo} is {responses}"));
        #endregion

        return Chunk;
    }

    private void WriteHeaders(CsvWriter writer)
    {
        foreach(var t in _tagsToWrite)
        {
            var colName = DicomTypeTranslaterReader.GetColumnNameForTag(t,false);
            writer.WriteField(colName);
        }

        writer.NextRecord();
    }

    private void WriteResult(CsvWriter writer, DicomCFindResponse response)
    {
        if (!response.HasDataset)
            return;

        foreach (var t in _tagsToWrite)
        {
            WriteValue(writer, response,t);
        }

        writer.NextRecord();
    }

    private void WriteValue(CsvWriter writer, DicomCFindResponse response, DicomTag tag)
    {
        var val = DicomTypeTranslaterReader.GetCSharpValue(response.Dataset, tag);

        if(val == null)
        {
            writer.WriteField("");
        }
        else
        {
            writer.WriteField(DicomTypeTranslater.Flatten(val));                
        }
    }
}