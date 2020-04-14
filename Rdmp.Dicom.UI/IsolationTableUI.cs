using System.Windows.Forms;
using Rdmp.Core.Curation;
using Rdmp.Core.Curation.Data.DataLoad;
using Rdmp.Core.DataLoad.Engine.LoadExecution.Components.Arguments;
using Rdmp.Core.DataLoad.Engine.LoadExecution.Components.Runtime;
using Rdmp.Core.Repositories;
using Rdmp.Dicom.PipelineComponents;

namespace Rdmp.Dicom.UI
{
    public partial class IsolationTableUI : Form
    {
        private readonly IsolationReview _reviewer;

        public IsolationTableUI(IsolationReview reviewer)
        {
            _reviewer = reviewer;
            InitializeComponent();
        }
    }
}
