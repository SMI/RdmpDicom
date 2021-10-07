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

            // Load list data
            ((ListBox)cblTemporality).DataSource = _configuration.TemporalityOptions.ToList();
            ((ListBox)cblTemporality).DisplayMember = "Key";
            ((ListBox)cblTemporality).ValueMember = "Value";

            cbNegation.DataSource = _configuration.NegationOptions.ToList();
            cbNegation.DisplayMember = "Key";
            cbNegation.ValueMember = "Value";

            ((ListBox)cblModalities).DataSource = _configuration.ModalityOptions.ToList();
            ((ListBox)cblModalities).DisplayMember = "Key";
            ((ListBox)cblModalities).ValueMember = "Value";

            /*((ListBox)cblReturnFields).DataSource = ReturnFieldOptions.ToList();
            ((ListBox)cblReturnFields).DisplayMember = "Key";
            ((ListBox)cblReturnFields).ValueMember = "Value";*/

            cbReturnFeild.DataSource = _configuration.ReturnFieldOptions.ToList();
            cbReturnFeild.DisplayMember = "Key";
            cbReturnFeild.ValueMember = "Value";

            //Set stored values
            tbUrl.Text = _configuration.Url;
            tbStartEndDateFormat.Text = _configuration.StartEndDateFormat;
            tbQuery.Text = _configuration.Query;
            SetCheckedListBox(cblTemporality, _configuration.Temporality.ToList());
            cbNegation.SelectedIndex = cbNegation.FindString(_configuration.Negation);
            cbUseStartDate.Checked = _configuration.UseStartDate;
            dtpStartDate.Value = _configuration.StartDate;
            cbUseEndDate.Checked = _configuration.UseEndDate;
            dtpEndDate.Value = _configuration.EndDate;
            SetCheckedListBox(cblModalities, _configuration.Modalities.ToList());
            //SetCheckedListBox(cblReturnFields, _configuration.ReturnFeilds);
            cbReturnFeild.SelectedIndex = cbReturnFeild.FindString(_configuration.ReturnField);
        }

        private void SetCheckedListBox(CheckedListBox clb, List<string> checkedItems)
        {
            for (int i = 0; i < clb.Items.Count; i++)
            {
                if (checkedItems.Contains(((KeyValuePair<string, string>)clb.Items[i]).Value))
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
            foreach (KeyValuePair<string, string> item in cblTemporality.CheckedItems)
            {
                _configuration.Temporality.Add(item.Value.ToString());                
            }

            _configuration.Negation = "";
            if (cbNegation.SelectedItem != null)
            {
                _configuration.Negation = ((KeyValuePair<string, string>)cbNegation.SelectedItem).Value;
            }

            _configuration.UseStartDate = cbUseStartDate.Checked;
            _configuration.StartDate = dtpStartDate.Value;
            _configuration.UseEndDate = cbUseEndDate.Checked;
            _configuration.EndDate = dtpEndDate.Value;

            _configuration.Modalities = new List<string>();
            foreach (KeyValuePair<string, string> item in cblModalities.CheckedItems)
            {
                _configuration.Modalities.Add(item.Value);
            }

            /*_configuration.ReturnFields = new List<string>();
            foreach (KeyValuePair<string, string> item in cblReturnFields.CheckedItems)
            {
                _configuration.ReturnFields.Add(item.Value);
            }*/

            _configuration.ReturnField = "";
            if (cbReturnFeild.SelectedItem != null)
            {
                _configuration.ReturnField = ((KeyValuePair<string, string>)cbReturnFeild.SelectedItem).Value;
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
