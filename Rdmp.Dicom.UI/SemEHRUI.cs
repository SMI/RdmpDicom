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

            tbUrl.Text = _configuration.Url;
            tbStartEndDateFormat.Text = _configuration.StartEndDateFormat;
            tbQuery.Text = _configuration.Query;
            SetCheckedListBox(cblTemporality, _configuration.Temporality);
            cbNegation.SelectedIndex = cbNegation.FindStringExact(_configuration.Negation);
            cbUseStartDate.Checked = _configuration.UseStartDate;
            dtpStartDate.Value = _configuration.StartDate;
            cbUseEndDate.Checked = _configuration.UseEndDate;
            dtpEndDate.Value = _configuration.EndDate;

            SetCheckedListBox(cblModalities, _configuration.Modalities);
            SetCheckedListBox(cblReturnFields, _configuration.ReturnFields);
        }

        private void SetCheckedListBox(CheckedListBox clb, List<string> checkedItems)
        {
            for (int i = 0; i < clb.Items.Count; i++)
            {
                if (checkedItems.Contains(clb.Items[i].ToString()))
                {
                    clb.SetItemChecked(i, true);
                }
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            // copy values from controls into config
            _configuration.Url = tbUrl.Text;
            _configuration.StartEndDateFormat = tbStartEndDateFormat.Text;
            _configuration.Query = tbQuery.Text;

            _configuration.Temporality = new List<string>();
            foreach (object item in cblTemporality.CheckedItems)
            {
                _configuration.Temporality.Add(item.ToString());
            }

            _configuration.Negation = "";
            if (cbNegation.SelectedItem != null)
            {
                _configuration.Negation = cbNegation.SelectedItem.ToString();
            }
            _configuration.UseStartDate = cbUseStartDate.Checked;
            _configuration.StartDate = dtpStartDate.Value;
            _configuration.UseEndDate = cbUseEndDate.Checked;
            _configuration.EndDate = dtpEndDate.Value;

            _configuration.Modalities = new List<string>();
            foreach (object item in cblModalities.CheckedItems)
            {
                _configuration.Modalities.Add(item.ToString());
            }

            _configuration.ReturnFields = new List<string>();
            foreach (object item in cblReturnFields.CheckedItems)
            {
                _configuration.ReturnFields.Add(item.ToString());
            }


            // save the config to the database
            Aggregate.Description = _configuration.Serialize();
            Aggregate.SaveToDatabase();

            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void cbUseStartDate_CheckedChanged(object sender, EventArgs e)
        {
            dtpStartDate.Enabled = cbUseStartDate.Checked;
        }

        private void cbUseEndDate_CheckedChanged(object sender, EventArgs e)
        {
            dtpEndDate.Enabled = cbUseEndDate.Checked;
        }
    }
}
