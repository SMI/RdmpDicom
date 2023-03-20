using Rdmp.Core.CohortCreation.Execution;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.Aggregation;
using Rdmp.Core.QueryCaching.Aggregation;
using System;
using System.Threading;
using System.Net;
using System.Net.Http;
using System.Linq;
using System.Text;
using System.Text.Json;
using Rdmp.Core.Repositories;

namespace Rdmp.Dicom.ExternalApis;

public class SemEHRApiCaller : PluginCohortCompiler
{
    public const string SemEHRApiPrefix = $"{ApiPrefix}SemEHR";

    public override void Run(AggregateConfiguration ac, CachedAggregateConfigurationResultsManager cache, CancellationToken token)
    {
        Run(ac, cache, token, SemEHRConfiguration.LoadFrom(ac));
    }

    private string GetAuthString(ICatalogueRepository repo, SemEHRConfiguration config)
    {
        if (config.ApiHttpDataAccessCredentials == 0)
            return null;

        var creds = repo.GetObjectByID<DataAccessCredentials>(config.ApiHttpDataAccessCredentials);

        return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{creds.Username}:{creds.GetDecryptedPassword()}"));
    }

    internal void Run(AggregateConfiguration ac, CachedAggregateConfigurationResultsManager cache, CancellationToken token, SemEHRConfiguration config)
    {
        var httpClientHandler = new HttpClientHandler();

        //Get data as post - not currently used
        //In the future we'll enable getting the data with POST maybe. Here for reference
        #region Get data as POST
        //StringContent httpContent = new StringContent(config.GetQueryJsonAsString(), System.Text.Encoding.UTF8, "application/json");
        //HttpResponseMessage response = httpClient.PostAsync(config.Url, httpContent).Result;
        #endregion


        //Get data as querystring
        //Currently the API only support data in the querystring so adding to the URL
        #region Get data as GET

        //If the ValidateServerCert isn't required then set the handeler.ServerCertificateCustomValidationCallback to DangerousAcceptAnyServerCertificateValidator
        if (!config.ValidateServerCert)
        {
            httpClientHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }
        HttpClient httpClient = new(httpClientHandler);
        if(config.ApiUsingHttpAuth())
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", GetAuthString(ac.CatalogueRepository,config));
        httpClient.Timeout = TimeSpan.FromSeconds(config.RequestTimeout);

        //Make the request to the API
        var response = httpClient.GetAsync(config.GetUrlWithQuerystring(), token).Result;

        //Check the status code is 200 success
        if (response.StatusCode == HttpStatusCode.OK)
        {
            //Get the result object and use built in DeserializeObject to concert to SemEHRResponse
            var responseData = response.Content.ReadAsStringAsync(token).Result;
            var semEHRResponse = JsonSerializer.Deserialize<SemEHRResponse>(responseData);

            if (semEHRResponse.success)
            {
                SubmitIdentifierList(config.ReturnField,
                    semEHRResponse.results.Count == 0 ? new string[] { } : semEHRResponse.results.ToArray(), ac,
                    cache);

                /*If we can cope with the return field with multiple types this will handle that
                /*if (string.IsNullOrEmpty(config.ReturnField))
                {
                    SubmitIdentifierList("sopinstanceuid", semEHRResponse.GetResultSopUids().ToArray(), ac, cache);
                }
                else
                {
                    switch(config.ReturnField)
                    {
                        case ("sopinstanceuid"):
                            SubmitIdentifierList("sopinstanceuid", semEHRResponse.GetResultSopUids().ToArray(), ac, cache);
                            break;
                        case ("seriesinstanceuid"):
                            SubmitIdentifierList("seriesinstanceuid", semEHRResponse.GetResultseriesUids().ToArray(), ac, cache);
                            break;
                        case ("studyinstanceuid"):
                            SubmitIdentifierList("studyinstanceuid", semEHRResponse.GetResultStudyUids().ToArray(), ac, cache);
                            break;
                    }
                }*/
            }
            else
            {
                //If we failed, get the failing error message
                throw new($"The SemEHR API has failed: {semEHRResponse.message}");
            }
               
        }
        else
        {
            throw new(
                $"The API response returned a HTTP Status Code: ({(int)response.StatusCode}) {response.StatusCode}");
        }
        #endregion

        httpClientHandler.Dispose();
        httpClient.Dispose();
    }

    public override bool ShouldRun(ICatalogue catalogue)
    {
        return catalogue.Name.StartsWith(SemEHRApiPrefix);
    }

    protected override string GetJoinColumnNameFor(AggregateConfiguration joinedTo)
    {
        // When the time comes to allow multiple columns back (use as a patient index table) then
        // this will have to be implemented (tell the user which field to link on)
        throw new NotSupportedException();
    }
}