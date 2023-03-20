using Rdmp.Core.Curation.Data.DataLoad;
using Rdmp.Dicom.PipelineComponents;
using Rdmp.UI.CommandExecution.AtomicCommands;
using Rdmp.UI.ItemActivation;

namespace Rdmp.Dicom.UI.CommandExecution.AtomicCommands;

class ExecuteCommandReviewIsolations: BasicUICommandExecution
{
    private readonly IsolationReview _reviewer;

    public ExecuteCommandReviewIsolations(IActivateItems activator, ProcessTask processTask) : base(activator)
    {
        _reviewer = new(processTask);

        if (_reviewer.Error != null) 
            SetImpossible(_reviewer.Error);

    }

    public override void Execute()
    {
        base.Execute();

        var ui = new IsolationTableUI(_reviewer);
        ui.Show();
    }
}