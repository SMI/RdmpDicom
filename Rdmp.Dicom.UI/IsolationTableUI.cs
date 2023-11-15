using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FAnsi.Discovery;
using Rdmp.Core.Curation.Data;
using Rdmp.Dicom.PipelineComponents;

namespace Rdmp.Dicom.UI;

public partial class IsolationTableUI : Form
{
    private readonly IsolationReview _reviewer;
    private List<IsolationDifference> _currentDiffs;
    private DataTable _currentDataTable;

    public IsolationTableUI(IsolationReview reviewer)
    {
        _reviewer = reviewer;
        InitializeComponent();

        dataGridView1.CellFormatting += DataGridView1OnCellFormatting;
    }

    private void IsolationTableUI_Load(object sender, EventArgs e)
    {
        foreach (var kvp in _reviewer.GetIsolationTables())
        {
            var btn = new Button {Text = kvp.Value.GetRuntimeName()};
            btn.Click += (a, b) => HandleClick(kvp);
            flpTables.Controls.Add(btn);
        }
    }

    private void HandleClick(KeyValuePair<TableInfo, DiscoveredTable> kvp)
    {
        dataGridView1.DataSource = _currentDataTable = _reviewer.GetDifferences(kvp, out _currentDiffs);
    }
        
    private void DataGridView1OnCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
    {
        var diff = _currentDiffs?.FirstOrDefault(d => d.RowIndex == e.RowIndex);

        if (diff == null || diff.IsMaster || _currentDataTable == null) return;
        var colName = _currentDataTable.Columns[e.ColumnIndex].ColumnName;
                
        if(diff.ConflictingColumns.Contains(colName))
            e.CellStyle.BackColor = Color.LightCyan;
    }

    private void tbTimeout_Click(object sender, EventArgs e)
    {
        tbTimeout.ForeColor = Color.Black;

        try
        {
            _reviewer.Timeout = string.IsNullOrWhiteSpace(tbTimeout.TextBox?.Text) ? 0 : int.Parse(tbTimeout.TextBox.Text);
        }
        catch (Exception)
        {
            tbTimeout.ForeColor = Color.Red;
        }
    }

    private void tbTop_Click(object sender, EventArgs e)
    {
            
        tbTop.ForeColor = Color.Black;

        try
        {
            _reviewer.Top = string.IsNullOrWhiteSpace(tbTop.TextBox?.Text) ? 0 : int.Parse(tbTop.TextBox.Text);
        }
        catch (Exception)
        {
            tbTop.ForeColor = Color.Red;
        }
    }
}