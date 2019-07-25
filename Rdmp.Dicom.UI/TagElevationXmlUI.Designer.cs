using ReusableUIComponents.ChecksUI;
using System.Windows.Forms;

namespace Rdmp.Dicom.UI
{
    partial class TagElevationXmlUI
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
            this.pEditor = new System.Windows.Forms.Panel();
            this.panel2 = new System.Windows.Forms.Panel();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnRunChecks = new System.Windows.Forms.Button();
            this.RagSmiley1 = new ReusableUIComponents.ChecksUI.RAGSmiley();
            this.btnOk = new System.Windows.Forms.Button();
            this.panel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // pEditor
            // 
            this.pEditor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pEditor.Location = new System.Drawing.Point(0, 0);
            this.pEditor.Name = "pEditor";
            this.pEditor.Size = new System.Drawing.Size(612, 423);
            this.pEditor.TabIndex = 0;
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.btnCancel);
            this.panel2.Controls.Add(this.btnRunChecks);
            this.panel2.Controls.Add(this.RagSmiley1);
            this.panel2.Controls.Add(this.btnOk);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel2.Location = new System.Drawing.Point(0, 423);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(612, 35);
            this.panel2.TabIndex = 0;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.btnCancel.Location = new System.Drawing.Point(299, 6);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 0;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnRunChecks
            // 
            this.btnRunChecks.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.btnRunChecks.Location = new System.Drawing.Point(525, 6);
            this.btnRunChecks.Name = "btnRunChecks";
            this.btnRunChecks.Size = new System.Drawing.Size(75, 23);
            this.btnRunChecks.TabIndex = 0;
            this.btnRunChecks.Text = "Run Checks";
            this.btnRunChecks.UseVisualStyleBackColor = true;
            // 
            // RagSmiley1
            // 
            this.RagSmiley1.AlwaysShowHandCursor = false;
            this.RagSmiley1.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.RagSmiley1.BackColor = System.Drawing.Color.Transparent;
            this.RagSmiley1.Location = new System.Drawing.Point(496, 6);
            this.RagSmiley1.Name = "RagSmiley1";
            this.RagSmiley1.Size = new System.Drawing.Size(23, 23);
            this.RagSmiley1.TabIndex = 0;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.btnOk.Location = new System.Drawing.Point(218, 6);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 0;
            this.btnOk.Text = "Ok";
            this.btnOk.UseVisualStyleBackColor = true;
            // 
            // TagElevationXmlUI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(612, 458);
            this.Controls.Add(this.pEditor);
            this.Controls.Add(this.panel2);
            this.Name = "TagElevationXmlUI";
            this.Text = "TagElevationXmlUI";
            this.panel2.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private RAGSmiley RagSmiley1;
        private Panel panel2;
        private Button btnRunChecks;
        private Button btnCancel;
        private Button btnOk;
        private Panel pEditor;
    }
}