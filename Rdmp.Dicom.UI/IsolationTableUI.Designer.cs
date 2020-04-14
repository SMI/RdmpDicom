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
            this.flpTables = new System.Windows.Forms.FlowLayoutPanel();
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // flpTables
            // 
            this.flpTables.Dock = System.Windows.Forms.DockStyle.Top;
            this.flpTables.Location = new System.Drawing.Point(0, 13);
            this.flpTables.Name = "flpTables";
            this.flpTables.Size = new System.Drawing.Size(974, 30);
            this.flpTables.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Dock = System.Windows.Forms.DockStyle.Top;
            this.label1.Location = new System.Drawing.Point(0, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(84, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Isolation Tables:";
            // 
            // IsolationTableUI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(974, 672);
            this.Controls.Add(this.flpTables);
            this.Controls.Add(this.label1);
            this.Name = "IsolationTableUI";
            this.Text = "Isolation Table Reviewer";
            this.Load += new System.EventHandler(this.IsolationTableUI_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.FlowLayoutPanel flpTables;
        private System.Windows.Forms.Label label1;
    }
}