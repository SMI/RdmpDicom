using System.IO;
using System.Linq;
using System.Text;
using FAnsi.Discovery;
using Rdmp.Core.Curation;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.DataLoad;
using Rdmp.Core.DataLoad.Engine.DataProvider.FromCache;
using Rdmp.Core.DataLoad.Engine.Job;
using Rdmp.Core.DataLoad.Engine.Job.Scheduling;

namespace Rdmp.Dicom.Cache;

/// <summary>
/// Looks in the cache folder and generates a file "LoadMe.txt" which lists all the zip files matching the
/// fetch date
/// </summary>
internal class SMICacheTextFileGenerator:CachedFileRetriever
{
    private DirectoryInfo _forLoading;

    public override void Initialize(ILoadDirectory hicProjectDirectory, DiscoveredDatabase dbInfo)
    {
        _forLoading = hicProjectDirectory.ForLoading;
    }

    public override ExitCodeType Fetch(IDataLoadJob dataLoadJob, GracefulCancellationToken cancellationToken)
    {

        var scheduledJob = ConvertToScheduledJob(dataLoadJob);

        var jobs = GetDataLoadWorkload(scheduledJob);

        if (!jobs.Any())
            return ExitCodeType.OperationNotRequired;

            
        StringBuilder sb = new();

        foreach (var file in jobs.Values)
            sb.AppendLine(file.FullName);

        File.WriteAllText(Path.Combine(_forLoading.FullName, "LoadMe.txt"), sb.ToString());

        scheduledJob.PushForDisposal(new UpdateProgressIfLoadsuccessful(scheduledJob));
        return ExitCodeType.Success;
    }

}