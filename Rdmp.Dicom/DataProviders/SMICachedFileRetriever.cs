using System;
using System.Linq;
using FAnsi.Discovery;
using Rdmp.Core.Curation;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.DataLoad;
using Rdmp.Core.DataLoad.Engine.DataProvider.FromCache;
using Rdmp.Core.DataLoad.Engine.Job;
using Rdmp.Core.DataLoad.Engine.Job.Scheduling;

namespace Rdmp.Dicom.DataProviders
{
    [Obsolete("Not clear what this does")]
    public class SMICachedFileRetriever : CachedFileRetriever
    {
        public override void Initialize(ILoadDirectory hicProjectDirectory, DiscoveredDatabase dbInfo)
        {
            
        }
        public override ExitCodeType Fetch(IDataLoadJob dataLoadJob, GracefulCancellationToken cancellationToken)
        {
            var scheduledJob = ConvertToScheduledJob(dataLoadJob);

            var jobs = GetDataLoadWorkload(scheduledJob);

            if (!jobs.Any())
                return ExitCodeType.OperationNotRequired;

            ExtractJobs(scheduledJob);

            // for the time being we will not delete files from the cache, need to make this configurable
            scheduledJob.PushForDisposal(new DeleteCachedFilesOperation(scheduledJob, jobs));
            scheduledJob.PushForDisposal(new UpdateProgressIfLoadsuccessful(scheduledJob));

            return ExitCodeType.Success;
        }
    }
}