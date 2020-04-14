using System.Collections.Generic;
using System.Windows.Forms;
using FAnsi.Discovery;
using Rdmp.Core.Curation;
using Rdmp.Core.Curation.Data;
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

        private void IsolationTableUI_Load(object sender, System.EventArgs e)
        {
            foreach (var kvp in _reviewer.GetIsolationTables())
            {
                var btn = new Button();
                btn.Text = kvp.Value.GetRuntimeName();
                btn.Click += (a, b) => HandleClick(kvp);
                flpTables.Controls.Add(btn);
            }
        }

        private void HandleClick(KeyValuePair<TableInfo, DiscoveredTable> kvp)
        {
            
        }
    }
}
