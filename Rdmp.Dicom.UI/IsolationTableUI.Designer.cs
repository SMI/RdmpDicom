using Rdmp.UI.ChecksUI;

namespace Rdmp.Dicom.UI
{
    partial class IsolationTableUI
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            flpTables = new System.Windows.Forms.FlowLayoutPanel();
            label1 = new System.Windows.Forms.Label();
            toolStrip1 = new System.Windows.Forms.ToolStrip();
            toolStripLabel1 = new System.Windows.Forms.ToolStripLabel();
            tbTop = new System.Windows.Forms.ToolStripTextBox();
            toolStripLabel2 = new System.Windows.Forms.ToolStripLabel();
            tbTimeout = new System.Windows.Forms.ToolStripTextBox();
            dataGridView1 = new System.Windows.Forms.DataGridView();
            toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            SuspendLayout();
            // 
            // flpTables
            // 
            flpTables.Dock = System.Windows.Forms.DockStyle.Top;
            flpTables.Location = new System.Drawing.Point(0, 71);
            flpTables.Margin = new System.Windows.Forms.Padding(6, 7, 6, 7);
            flpTables.Name = "flpTables";
            flpTables.Size = new System.Drawing.Size(2110, 74);
            flpTables.TabIndex = 0;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Dock = System.Windows.Forms.DockStyle.Top;
            label1.Location = new System.Drawing.Point(0, 0);
            label1.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(181, 32);
            label1.TabIndex = 1;
            label1.Text = "Isolation Tables:";
            // 
            // toolStrip1
            // 
            toolStrip1.ImageScalingSize = new System.Drawing.Size(32, 32);
            toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { toolStripLabel1, tbTop, toolStripLabel2, tbTimeout });
            toolStrip1.Location = new System.Drawing.Point(0, 32);
            toolStrip1.Name = "toolStrip1";
            toolStrip1.Padding = new System.Windows.Forms.Padding(0, 0, 4, 0);
            toolStrip1.Size = new System.Drawing.Size(2110, 39);
            toolStrip1.TabIndex = 4;
            toolStrip1.Text = "toolStrip1";
            // 
            // toolStripLabel1
            // 
            toolStripLabel1.Name = "toolStripLabel1";
            toolStripLabel1.Size = new System.Drawing.Size(58, 33);
            toolStripLabel1.Text = "Top:";
            // 
            // tbTop
            // 
            tbTop.Name = "tbTop";
            tbTop.Size = new System.Drawing.Size(212, 39);
            tbTop.Click += tbTop_Click;
            // 
            // toolStripLabel2
            // 
            toolStripLabel2.Name = "toolStripLabel2";
            toolStripLabel2.Size = new System.Drawing.Size(108, 33);
            toolStripLabel2.Text = "Timeout:";
            // 
            // tbTimeout
            // 
            tbTimeout.Name = "tbTimeout";
            tbTimeout.Size = new System.Drawing.Size(212, 39);
            tbTimeout.Click += tbTimeout_Click;
            // 
            // dataGridView1
            // 
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            dataGridView1.Location = new System.Drawing.Point(0, 145);
            dataGridView1.Margin = new System.Windows.Forms.Padding(6, 7, 6, 7);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.ReadOnly = true;
            dataGridView1.RowHeadersWidth = 82;
            dataGridView1.Size = new System.Drawing.Size(2110, 1509);
            dataGridView1.TabIndex = 5;
            // 
            // IsolationTableUI
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(13F, 32F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(2110, 1654);
            Controls.Add(dataGridView1);
            Controls.Add(flpTables);
            Controls.Add(toolStrip1);
            Controls.Add(label1);
            Margin = new System.Windows.Forms.Padding(6, 7, 6, 7);
            Name = "IsolationTableUI";
            Text = "Isolation Table Reviewer";
            Load += IsolationTableUI_Load;
            toolStrip1.ResumeLayout(false);
            toolStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.FlowLayoutPanel flpTables;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripLabel toolStripLabel1;
        private System.Windows.Forms.ToolStripTextBox tbTop;
        private System.Windows.Forms.ToolStripLabel toolStripLabel2;
        private System.Windows.Forms.ToolStripTextBox tbTimeout;
        private System.Windows.Forms.DataGridView dataGridView1;
    }
}