using System;
using System.Windows.Forms;
using Rdmp.Core.Curation.Data;
using Rdmp.Dicom.TagPromotionSchema;

namespace Rdmp.Dicom.UI
{
    public partial class TagColumnAdderUI : Form
    {
        private readonly TableInfo _tableInfo;

        public TagColumnAdderUI(TableInfo tableInfo)
        {
            _tableInfo = tableInfo;
            InitializeComponent();
            
            cbxTag.AutoCompleteSource = AutoCompleteSource.ListItems;
            cbxTag.DataSource = TagColumnAdder.GetAvailableTags();
        }

        private void cbxTag_SelectedIndexChanged(object sender, EventArgs e)
        {
            ragSmiley1.Reset();
            try
            {
                var keyword = cbxTag.Text;
                var type = TagColumnAdder.GetDataTypeForTag(keyword, _tableInfo.GetQuerySyntaxHelper().TypeTranslater);

                var multiplicity = TagColumnAdder.GetTag(keyword).ValueMultiplicity;
                lblMultiplicity.Text = multiplicity == null ? "(Multiplicity:None)" : $"(Multiplicity: Min {multiplicity.Minimum} Max {multiplicity.Maximum} M {multiplicity.Multiplicity})";

                tbDataType.Text = type;
            }
            catch (Exception exception)
            {
                ragSmiley1.Fatal(exception);
            }
        }

        public string ColumnName { get; private set; }
        public string ColumnDataType { get; private set; }

        private void btnOk_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;



            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void cbxTag_TextChanged(object sender, EventArgs e)
        {
            ColumnName = cbxTag.Text;
        }

        private void tbDataType_TextChanged(object sender, EventArgs e)
        {
            ColumnDataType = tbDataType.Text;
        }
    }
}
