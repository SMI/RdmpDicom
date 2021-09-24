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

        public List<KeyValuePair<string, string>> TemporalityOptions = new List<KeyValuePair<string, string>>()
        {
            new KeyValuePair<string, string>("Recent", "recent"),
            new KeyValuePair<string, string>("Historical", "historical"),
            new KeyValuePair<string, string>("Hypothetical", "hypothetical"),
        };

        public List<KeyValuePair<string, string>> NegationOptions = new List<KeyValuePair<string, string>>()
        {
            new KeyValuePair<string, string>("Any", "any"),
            new KeyValuePair<string, string>("Negated - The query term is mentioned in terms of absence.", "negated"),
            new KeyValuePair<string, string>("Affirmed - The query term is confirmed to be present.", "affirmed"),
        };

        public List<KeyValuePair<string, string>> ModalityOptions = new List<KeyValuePair<string, string>>()
        {
            new KeyValuePair<string, string>("CR - Computed Radiography", "CR"),
            new KeyValuePair<string, string>("CT - Computed Tomography", "CT"),
            new KeyValuePair<string, string>("DX - Digital Radiography", "DX"),
            new KeyValuePair<string, string>("MG - Mammography", "MG"),
            new KeyValuePair<string, string>("MR - Magnetic Resonance", "MR"),
            new KeyValuePair<string, string>("NM - Nuclear Medicine", "NM"),
            new KeyValuePair<string, string>("OT - Other", "OT"),
            new KeyValuePair<string, string>("PR - Presentation State", "PR"),
            new KeyValuePair<string, string>("PT - Positron emission tomography (PET)", "PT"),
            new KeyValuePair<string, string>("RF - Radio Fluoroscopy", "RF"),
            new KeyValuePair<string, string>("US - Ultrasound", "US"),
            new KeyValuePair<string, string>("XA - X-Ray Angiography", "XA"),
        };

        public List<KeyValuePair<string, string>> ReturnFieldOptions = new List<KeyValuePair<string, string>>()
        {
            new KeyValuePair<string, string>("SOP Instance UID", "sopinstanceuid"),
            new KeyValuePair<string, string>("Series Instance UID", "seriesInstanceuid"),
            new KeyValuePair<string, string>("Study Instance UID", "studyInstanceuid"),
        };

        public SemEHRUI(IActivateItems activator, SemEHRApiCaller api, AggregateConfiguration aggregate)
        {
            InitializeComponent();
            _configuration = SemEHRConfiguration.LoadFrom(aggregate);
            Aggregate = aggregate;

            // Load list data
            ((ListBox)cblTemporality).DataSource = TemporalityOptions;
            ((ListBox)cblTemporality).DisplayMember = "Key";
            ((ListBox)cblTemporality).ValueMember = "Value";

            cbNegation.DataSource = NegationOptions;
            cbNegation.DisplayMember = "Key";
            cbNegation.ValueMember = "Value";

            ((ListBox)cblModalities).DataSource = ModalityOptions;
            ((ListBox)cblModalities).DisplayMember = "Key";
            ((ListBox)cblModalities).ValueMember = "Value";

            ((ListBox)cblReturnFields).DataSource = ReturnFieldOptions;
            ((ListBox)cblReturnFields).DisplayMember = "Key";
            ((ListBox)cblReturnFields).ValueMember = "Value";

            //Set stored values
            tbUrl.Text = _configuration.Url;
            tbStartEndDateFormat.Text = _configuration.StartEndDateFormat;
            tbQuery.Text = _configuration.Query;
            SetCheckedListBox(cblTemporality, _configuration.Temporality);
            cbNegation.SelectedIndex = cbNegation.FindString(_configuration.Negation);
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

            _configuration.ReturnFields = new List<string>();
            foreach (KeyValuePair<string, string> item in cblReturnFields.CheckedItems)
            {
                _configuration.ReturnFields.Add(item.Value);
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
