using System;
using System.Collections.Generic;
using System.Text;
using CommandLine;
using Rdmp.Core.Curation.Data.DataLoad;

namespace Rdmp.Dicom.PipelineComponents
{
    public class IsolationReview
    {
        public string Error { get; }

        public IsolationReview(ProcessTask processTask)
        {
            if(processTask == null)
                throw new ArgumentNullException(nameof(processTask));

            if(!processTask.IsPluginType() || processTask.ProcessTaskType != ProcessTaskType.MutilateDataTable || processTask.Path != typeof(PrimaryKeyCollisionIsolationMutilation).FullName)
                Error = "ProcessTask is not an isolation mutilation";


            // TODO: get stuff to find tables etc
            processTask.GetAllArguments();

        }
    }
}
