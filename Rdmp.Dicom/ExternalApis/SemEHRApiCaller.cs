using Rdmp.Core.CohortCreation.Execution;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.Aggregation;
using Rdmp.Core.QueryCaching.Aggregation;
using System;
using System.Threading;
using Newtonsoft.Json.Linq;

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Rdmp.Dicom.ExternalApis
{
    public class SemEHRApiCaller : PluginCohortCompiler
    {
        public const string SemEHRApiPrefix = ApiPrefix + "SemEHR";

        static HttpClient httpClient = new HttpClient();

        public override void Run(AggregateConfiguration ac, CachedAggregateConfigurationResultsManager cache, CancellationToken token)
        {
            var config = SemEHRConfiguration.LoadFrom(ac);

            //Get data as JSON for call to API
            JObject apiCallJson = config.GetQueryJson();
            StringContent httpContent = new StringContent(apiCallJson.ToString(), System.Text.Encoding.UTF8, "application/json");

            HttpResponseMessage response = httpClient.PostAsync(config.Url, httpContent).Result;
            if(response.StatusCode == HttpStatusCode.OK)
            {
                string responseData = response.Content.ReadAsStringAsync().Result;

                //TODO deal with returnfields
                SubmitIdentifierList("PatientID", new string[] { responseData }, ac, cache);
            }
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
}
