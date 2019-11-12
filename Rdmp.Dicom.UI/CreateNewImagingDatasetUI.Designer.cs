using Rdmp.UI.SimpleControls;

namespace Rdmp.Dicom.UI
{
    partial class CreateNewImagingDatasetUI
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
            this.serverDatabaseTableSelector1 = new ServerDatabaseTableSelector();
            this.rbJsonSources = new System.Windows.Forms.RadioButton();
            this.rbFileSources = new System.Windows.Forms.RadioButton();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.cbMergeNullability = new System.Windows.Forms.CheckBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.cbCreateLoad = new System.Windows.Forms.CheckBox();
            this.btnCreateSuiteWithTemplate = new System.Windows.Forms.Button();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.tbPrefix = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.groupBox1.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.SuspendLayout();
            // 
            // serverDatabaseTableSelector1
            // 
            this.serverDatabaseTableSelector1.AllowTableValuedFunctionSelection = false;
            this.serverDatabaseTableSelector1.AutoSize = true;
            this.serverDatabaseTableSelector1.Database = "";
            this.serverDatabaseTableSelector1.DatabaseType = FAnsi.DatabaseType.MicrosoftSQLServer;
            this.serverDatabaseTableSelector1.Dock = System.Windows.Forms.DockStyle.Top;
            this.serverDatabaseTableSelector1.Location = new System.Drawing.Point(0, 0);
            this.serverDatabaseTableSelector1.Name = "serverDatabaseTableSelector1";
            this.serverDatabaseTableSelector1.Password = "";
            this.serverDatabaseTableSelector1.Server = "";
            this.serverDatabaseTableSelector1.Size = new System.Drawing.Size(1029, 143);
            this.serverDatabaseTableSelector1.TabIndex = 0;
            this.serverDatabaseTableSelector1.Username = "";
            this.serverDatabaseTableSelector1.Load += new System.EventHandler(this.serverDatabaseTableSelector1_Load);
            // 
            // rbJsonSources
            // 
            this.rbJsonSources.AutoSize = true;
            this.rbJsonSources.Location = new System.Drawing.Point(6, 23);
            this.rbJsonSources.Name = "rbJsonSources";
            this.rbJsonSources.Size = new System.Drawing.Size(174, 17);
            this.rbJsonSources.TabIndex = 6;
            this.rbJsonSources.Text = "Json Sources For Load Pipeline";
            this.rbJsonSources.UseVisualStyleBackColor = true;
            // 
            // rbFileSources
            // 
            this.rbFileSources.AutoSize = true;
            this.rbFileSources.Checked = true;
            this.rbFileSources.Location = new System.Drawing.Point(6, 46);
            this.rbFileSources.Name = "rbFileSources";
            this.rbFileSources.Size = new System.Drawing.Size(168, 17);
            this.rbFileSources.TabIndex = 6;
            this.rbFileSources.TabStop = true;
            this.rbFileSources.Text = "File Sources For Load Pipeline";
            this.rbFileSources.UseVisualStyleBackColor = true;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.cbMergeNullability);
            this.groupBox1.Location = new System.Drawing.Point(304, 57);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(200, 63);
            this.groupBox1.TabIndex = 7;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Corruption Resolution Strategies";
            // 
            // cbMergeNullability
            // 
            this.cbMergeNullability.AutoSize = true;
            this.cbMergeNullability.Checked = true;
            this.cbMergeNullability.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbMergeNullability.Location = new System.Drawing.Point(6, 19);
            this.cbMergeNullability.Name = "cbMergeNullability";
            this.cbMergeNullability.Size = new System.Drawing.Size(103, 17);
            this.cbMergeNullability.TabIndex = 0;
            this.cbMergeNullability.Text = "Merge Nullability";
            this.cbMergeNullability.UseVisualStyleBackColor = true;
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.cbCreateLoad);
            this.groupBox3.Controls.Add(this.btnCreateSuiteWithTemplate);
            this.groupBox3.Controls.Add(this.groupBox4);
            this.groupBox3.Controls.Add(this.groupBox1);
            this.groupBox3.Location = new System.Drawing.Point(9, 152);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(1008, 181);
            this.groupBox3.TabIndex = 9;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Entire Suite Creation  (All tables + Load configuration)";
            // 
            // cbCreateLoad
            // 
            this.cbCreateLoad.AutoSize = true;
            this.cbCreateLoad.Checked = true;
            this.cbCreateLoad.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbCreateLoad.Location = new System.Drawing.Point(3, 34);
            this.cbCreateLoad.Name = "cbCreateLoad";
            this.cbCreateLoad.Size = new System.Drawing.Size(110, 17);
            this.cbCreateLoad.TabIndex = 10;
            this.cbCreateLoad.Text = "Create Data Load";
            this.cbCreateLoad.UseVisualStyleBackColor = true;
            this.cbCreateLoad.CheckedChanged += new System.EventHandler(this.cbCreateLoad_CheckedChanged);
            // 
            // btnCreateSuiteWithTemplate
            // 
            this.btnCreateSuiteWithTemplate.Location = new System.Drawing.Point(6, 150);
            this.btnCreateSuiteWithTemplate.Name = "btnCreateSuiteWithTemplate";
            this.btnCreateSuiteWithTemplate.Size = new System.Drawing.Size(122, 23);
            this.btnCreateSuiteWithTemplate.TabIndex = 9;
            this.btnCreateSuiteWithTemplate.Text = "Create With Template";
            this.btnCreateSuiteWithTemplate.UseVisualStyleBackColor = true;
            this.btnCreateSuiteWithTemplate.Click += new System.EventHandler(this.btnCreateSuiteWithTemplate_Click);
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.tbPrefix);
            this.groupBox4.Controls.Add(this.label3);
            this.groupBox4.Controls.Add(this.rbJsonSources);
            this.groupBox4.Controls.Add(this.rbFileSources);
            this.groupBox4.Location = new System.Drawing.Point(6, 57);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(292, 88);
            this.groupBox4.TabIndex = 8;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Loader Source Options";
            // 
            // tbPrefix
            // 
            this.tbPrefix.Location = new System.Drawing.Point(135, 65);
            this.tbPrefix.Name = "tbPrefix";
            this.tbPrefix.Size = new System.Drawing.Size(151, 20);
            this.tbPrefix.TabIndex = 8;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(6, 68);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(119, 13);
            this.label3.TabIndex = 7;
            this.label3.Text = "Table Prefix (e.g. MR_):";
            // 
            // CreateNewImagingDatasetUI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1029, 337);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.serverDatabaseTableSelector1);
            this.Name = "CreateNewImagingDatasetUI";
            this.Text = "CreateNewImagingDatasetUI";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private ServerDatabaseTableSelector serverDatabaseTableSelector1;
        private System.Windows.Forms.RadioButton rbJsonSources;
        private System.Windows.Forms.RadioButton rbFileSources;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.CheckBox cbMergeNullability;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.TextBox tbPrefix;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button btnCreateSuiteWithTemplate;
        private System.Windows.Forms.CheckBox cbCreateLoad;
    }
}