using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.DataLoad;
using Rdmp.Core.DataLoad.Engine.Job;

namespace Rdmp.Dicom.Attachers.Routing;

class AutoRoutingAttacherWithPersistentRaw : AutoRoutingAttacher
{
    public AutoRoutingAttacherWithPersistentRaw():base(false)
    {
            
    }
    public override ExitCodeType Attach(IDataLoadJob job, GracefulCancellationToken token)
    {
        //Create RAW if it doesn't exist
        if (!_dbInfo.Exists())
            _dbInfo.Create();

        var tableCreator = new PersistentRawTableCreator();

        tableCreator.CreateRAWTablesInDatabase(_dbInfo, job);

        job.PushForDisposal(tableCreator);

        return base.Attach(job,token);
    }
}