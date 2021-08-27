using Rdmp.Core.CohortCreation.Execution;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.Aggregation;
using Rdmp.Core.QueryCaching.Aggregation;
using System;
using System.Threading;

namespace Rdmp.Dicom.ExternalApis
{
    public class SemEHRApiCaller : PluginCohortCompiler
    {
        public const string SemEHRApiPrefix = ApiPrefix + "SemEHR";

        public override void Run(AggregateConfiguration ac, CachedAggregateConfigurationResultsManager cache, CancellationToken token)
        {
            var config = SemEHRConfiguration.LoadFrom(ac);

            // TODO fetch data

            SubmitIdentifierList("PatientID",new string[] { "0101010101" },ac,cache);
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
