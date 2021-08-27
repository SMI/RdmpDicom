using Rdmp.Core.Curation.Data.Aggregation;
using Rdmp.Dicom.ExternalApis;
using Rdmp.UI.ItemActivation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Rdmp.Dicom.UI
{
    public partial class SemEHRUI : Form
    {
        private readonly SemEHRConfiguration _configuration;

        public AggregateConfiguration Aggregate { get; }

        public SemEHRUI(IActivateItems activator, SemEHRApiCaller api, AggregateConfiguration aggregate)
        {
            InitializeComponent();
            _configuration = SemEHRConfiguration.LoadFrom(aggregate);
            Aggregate = aggregate;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            Aggregate.Description = _configuration.Serialize();
            Aggregate.SaveToDatabase();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
