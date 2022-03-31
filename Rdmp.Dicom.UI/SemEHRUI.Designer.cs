
namespace Rdmp.Dicom.UI
{
    partial class SemEHRUI
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
            this.components = new System.ComponentModel.Container();
            this.label1 = new System.Windows.Forms.Label();
            this.tbUrl = new System.Windows.Forms.TextBox();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.cblTemporality = new System.Windows.Forms.CheckedListBox();
            this.cbNegation = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.tbQuery = new System.Windows.Forms.TextBox();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.cbReturnFeild = new System.Windows.Forms.ComboBox();
            this.label9 = new System.Windows.Forms.Label();
            this.cbUseEndDate = new System.Windows.Forms.CheckBox();
            this.cbUseStartDate = new System.Windows.Forms.CheckBox();
            this.label7 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.cblModalities = new System.Windows.Forms.CheckedListBox();
            this.label5 = new System.Windows.Forms.Label();
            this.dtpEndDate = new System.Windows.Forms.DateTimePicker();
            this.dtpStartDate = new System.Windows.Forms.DateTimePicker();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.tbPassphrase = new System.Windows.Forms.TextBox();
            this.label10 = new System.Windows.Forms.Label();
            this.cbValidateServerCert = new System.Windows.Forms.CheckBox();
            this.label8 = new System.Windows.Forms.Label();
            this.tbStartEndDateFormat = new System.Windows.Forms.TextBox();
            this.checkStateRenderer1 = new BrightIdeasSoftware.CheckStateRenderer();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.flowLayoutPanel1.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(15, 15);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(103, 15);
            this.label1.TabIndex = 0;
            this.label1.Text = "API Endpoint URL:";
            // 
            // tbUrl
            // 
            this.tbUrl.Location = new System.Drawing.Point(15, 33);
            this.tbUrl.Name = "tbUrl";
            this.tbUrl.Size = new System.Drawing.Size(407, 23);
            this.tbUrl.TabIndex = 10;
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(3, 3);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(75, 23);
            this.btnSave.TabIndex = 100;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(84, 3);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 101;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Controls.Add(this.btnSave);
            this.flowLayoutPanel1.Controls.Add(this.btnCancel);
            this.flowLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.flowLayoutPanel1.Location = new System.Drawing.Point(0, 647);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(467, 31);
            this.flowLayoutPanel1.TabIndex = 4;
            // 
            // cblTemporality
            // 
            this.cblTemporality.FormattingEnabled = true;
            this.cblTemporality.Location = new System.Drawing.Point(17, 89);
            this.cblTemporality.Name = "cblTemporality";
            this.cblTemporality.Size = new System.Drawing.Size(406, 58);
            this.cblTemporality.TabIndex = 2;
            // 
            // cbNegation
            // 
            this.cbNegation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbNegation.FormattingEnabled = true;
            this.cbNegation.Location = new System.Drawing.Point(17, 181);
            this.cbNegation.Name = "cbNegation";
            this.cbNegation.Size = new System.Drawing.Size(406, 23);
            this.cbNegation.TabIndex = 3;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(16, 71);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(72, 15);
            this.label2.TabIndex = 7;
            this.label2.Text = "Temporality:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(17, 163);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(59, 15);
            this.label3.TabIndex = 8;
            this.label3.Text = "Negation:";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(16, 15);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(42, 15);
            this.label4.TabIndex = 9;
            this.label4.Text = "Query:";
            // 
            // tbQuery
            // 
            this.tbQuery.Location = new System.Drawing.Point(16, 33);
            this.tbQuery.Name = "tbQuery";
            this.tbQuery.Size = new System.Drawing.Size(407, 23);
            this.tbQuery.TabIndex = 1;
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Location = new System.Drawing.Point(12, 12);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(445, 631);
            this.tabControl1.TabIndex = 200;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.cbReturnFeild);
            this.tabPage1.Controls.Add(this.label9);
            this.tabPage1.Controls.Add(this.cbUseEndDate);
            this.tabPage1.Controls.Add(this.cbUseStartDate);
            this.tabPage1.Controls.Add(this.label7);
            this.tabPage1.Controls.Add(this.label6);
            this.tabPage1.Controls.Add(this.cblModalities);
            this.tabPage1.Controls.Add(this.label5);
            this.tabPage1.Controls.Add(this.dtpEndDate);
            this.tabPage1.Controls.Add(this.dtpStartDate);
            this.tabPage1.Controls.Add(this.label4);
            this.tabPage1.Controls.Add(this.tbQuery);
            this.tabPage1.Controls.Add(this.cblTemporality);
            this.tabPage1.Controls.Add(this.cbNegation);
            this.tabPage1.Controls.Add(this.label3);
            this.tabPage1.Controls.Add(this.label2);
            this.tabPage1.Location = new System.Drawing.Point(4, 24);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(437, 603);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Query";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // cbReturnFeild
            // 
            this.cbReturnFeild.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbReturnFeild.FormattingEnabled = true;
            this.cbReturnFeild.Location = new System.Drawing.Point(16, 537);
            this.cbReturnFeild.Name = "cbReturnFeild";
            this.cbReturnFeild.Size = new System.Drawing.Size(407, 23);
            this.cbReturnFeild.TabIndex = 9;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(16, 519);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(73, 15);
            this.label9.TabIndex = 20;
            this.label9.Text = "Return Field:";
            // 
            // cbUseEndDate
            // 
            this.cbUseEndDate.AutoSize = true;
            this.cbUseEndDate.Location = new System.Drawing.Point(228, 243);
            this.cbUseEndDate.Name = "cbUseEndDate";
            this.cbUseEndDate.Size = new System.Drawing.Size(15, 14);
            this.cbUseEndDate.TabIndex = 6;
            this.cbUseEndDate.UseVisualStyleBackColor = true;
            this.cbUseEndDate.CheckedChanged += new System.EventHandler(this.cbUseEndDate_CheckedChanged);
            // 
            // cbUseStartDate
            // 
            this.cbUseStartDate.AutoSize = true;
            this.cbUseStartDate.Location = new System.Drawing.Point(24, 243);
            this.cbUseStartDate.Name = "cbUseStartDate";
            this.cbUseStartDate.Size = new System.Drawing.Size(15, 14);
            this.cbUseStartDate.TabIndex = 4;
            this.cbUseStartDate.UseVisualStyleBackColor = true;
            this.cbUseStartDate.CheckedChanged += new System.EventHandler(this.cbUseStartDate_CheckedChanged);
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(223, 221);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(22, 15);
            this.label7.TabIndex = 16;
            this.label7.Text = "To:";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(16, 273);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(65, 15);
            this.label6.TabIndex = 15;
            this.label6.Text = "Modalities:";
            // 
            // cblModalities
            // 
            this.cblModalities.FormattingEnabled = true;
            this.cblModalities.Location = new System.Drawing.Point(16, 291);
            this.cblModalities.Name = "cblModalities";
            this.cblModalities.Size = new System.Drawing.Size(407, 220);
            this.cblModalities.TabIndex = 8;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(17, 221);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(38, 15);
            this.label5.TabIndex = 13;
            this.label5.Text = "From:";
            // 
            // dtpEndDate
            // 
            this.dtpEndDate.Enabled = false;
            this.dtpEndDate.Location = new System.Drawing.Point(249, 239);
            this.dtpEndDate.Name = "dtpEndDate";
            this.dtpEndDate.Size = new System.Drawing.Size(174, 23);
            this.dtpEndDate.TabIndex = 7;
            // 
            // dtpStartDate
            // 
            this.dtpStartDate.Enabled = false;
            this.dtpStartDate.Location = new System.Drawing.Point(43, 239);
            this.dtpStartDate.Name = "dtpStartDate";
            this.dtpStartDate.Size = new System.Drawing.Size(174, 23);
            this.dtpStartDate.TabIndex = 5;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.tbPassphrase);
            this.tabPage2.Controls.Add(this.label10);
            this.tabPage2.Controls.Add(this.cbValidateServerCert);
            this.tabPage2.Controls.Add(this.label8);
            this.tabPage2.Controls.Add(this.tbStartEndDateFormat);
            this.tabPage2.Controls.Add(this.label1);
            this.tabPage2.Controls.Add(this.tbUrl);
            this.tabPage2.Location = new System.Drawing.Point(4, 24);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(437, 603);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Settings";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // tbPassphrase
            // 
            this.tbPassphrase.Location = new System.Drawing.Point(15, 77);
            this.tbPassphrase.Name = "tbPassphrase";
            this.tbPassphrase.PasswordChar = '*';
            this.tbPassphrase.Size = new System.Drawing.Size(407, 23);
            this.tbPassphrase.TabIndex = 12;
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(15, 59);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(137, 15);
            this.label10.TabIndex = 16;
            this.label10.Text = "API Endpoint Passphrase";
            // 
            // cbValidateServerCert
            // 
            this.cbValidateServerCert.AutoSize = true;
            this.cbValidateServerCert.Location = new System.Drawing.Point(263, 11);
            this.cbValidateServerCert.Name = "cbValidateServerCert";
            this.cbValidateServerCert.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.cbValidateServerCert.Size = new System.Drawing.Size(159, 19);
            this.cbValidateServerCert.TabIndex = 11;
            this.cbValidateServerCert.Text = "Validate Server Certificate";
            this.toolTip1.SetToolTip(this.cbValidateServerCert, "Ensure the certificate trust chain is valid when making a connection to this serv" +
        "er.");
            this.cbValidateServerCert.UseVisualStyleBackColor = true;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(15, 121);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(123, 15);
            this.label8.TabIndex = 13;
            this.label8.Text = "Date From/To Format:";
            // 
            // tbStartEndDateFormat
            // 
            this.tbStartEndDateFormat.Location = new System.Drawing.Point(15, 139);
            this.tbStartEndDateFormat.Name = "tbStartEndDateFormat";
            this.tbStartEndDateFormat.Size = new System.Drawing.Size(407, 23);
            this.tbStartEndDateFormat.TabIndex = 13;
            // 
            // SemEHRUI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(467, 678);
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.flowLayoutPanel1);
            this.Name = "SemEHRUI";
            this.Text = "SemEHR Query Builder";
            this.flowLayoutPanel1.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            this.tabPage2.ResumeLayout(false);
            this.tabPage2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbUrl;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.CheckedListBox cblTemporality;
        private System.Windows.Forms.ComboBox cbNegation;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox tbQuery;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.CheckedListBox cblModalities;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.DateTimePicker dtpEndDate;
        private System.Windows.Forms.DateTimePicker dtpStartDate;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.CheckBox cbUseEndDate;
        private System.Windows.Forms.CheckBox cbUseStartDate;
        private BrightIdeasSoftware.CheckStateRenderer checkStateRenderer1;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox tbStartEndDateFormat;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.ComboBox cbReturnFeild;
        private System.Windows.Forms.CheckBox cbValidateServerCert;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.TextBox tbPassphrase;
        private System.Windows.Forms.Label label10;
    }
}