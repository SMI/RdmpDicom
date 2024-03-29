
//------------------------------------------------------------------------------

//  <auto-generated>
//      This code was generated by:
//        TerminalGuiDesigner v1.0.9.0
//      Changes to this file may cause incorrect behavior and will be lost if
//      the code is regenerated.
//  </auto-generated>
// -----------------------------------------------------------------------------
namespace Rdmp.Dicom {
    using System;
    using Terminal.Gui;
    
    
    public partial class SemEHRConsoleUI : Terminal.Gui.Window {
        
        private Terminal.Gui.TabView tabview1;
        
        private Terminal.Gui.Label label1;
        
        private Terminal.Gui.Label label2;
        
        private Terminal.Gui.TextField tbQuery;
        
        private Terminal.Gui.Label label3;
        
        private Terminal.Gui.Label label4;
        
        private Terminal.Gui.Label label5;
        
        private Terminal.Gui.DateField dptStartDate;
        
        private Terminal.Gui.DateField dptEndDate;
        
        private Terminal.Gui.CheckBox cbUseStartDate;
        
        private Terminal.Gui.CheckBox cbUseEndDate;
        
        private Terminal.Gui.RadioGroup rgNegation;
        
        private Terminal.Gui.Label label6;
        
        private Terminal.Gui.RadioGroup rgReturnField;
        
        private Terminal.Gui.Label label7;
        
        private Terminal.Gui.TextField tbModalities;
        
        private Terminal.Gui.Label label10;
        
        private Terminal.Gui.CheckBox cbRecent;
        
        private Terminal.Gui.CheckBox cbHistorical;
        
        private Terminal.Gui.CheckBox cbHypothetical;
        
        private Terminal.Gui.Label label8;
        
        private Terminal.Gui.Label label82;
        
        private Terminal.Gui.Label label83;
        
        private Terminal.Gui.Label label832;
        
        private Terminal.Gui.Label label9;
        
        private Terminal.Gui.CheckBox cbValidateServerCertificate;
        
        private Terminal.Gui.TextField tbApiEndpointUrl;
        
        private Terminal.Gui.TextField tbApiEndpointPassphrase;
        
        private Terminal.Gui.TextField tbTimeout;
        
        private Terminal.Gui.TextField tbDateFormat;
        
        private Terminal.Gui.TextField tbBasicAuthCredentialsId;
        
        private Terminal.Gui.Button btnSave;
        
        private Terminal.Gui.Button btnCancel;
        
        private void InitializeComponent() {
            this.Width = Dim.Fill(0);
            this.Height = Dim.Fill(0);
            this.X = 0;
            this.Y = 0;
            this.Modal = false;
            this.Text = "";
            this.Border.BorderStyle = Terminal.Gui.BorderStyle.Single;
            this.Border.BorderBrush = Terminal.Gui.Color.Blue;
            this.Border.Effect3D = false;
            this.TextAlignment = Terminal.Gui.TextAlignment.Left;
            this.Title = "SemEHR Query Builder";
            this.tabview1 = new Terminal.Gui.TabView();
            this.tabview1.Width = Dim.Fill(0);
            this.tabview1.Height = Dim.Fill(1);
            this.tabview1.X = -1;
            this.tabview1.Y = 0;
            this.tabview1.Data = "tabview1";
            this.tabview1.Text = "";
            this.tabview1.TextAlignment = Terminal.Gui.TextAlignment.Left;
            this.tabview1.MaxTabTextWidth = 30u;
            this.tabview1.Style.ShowBorder = true;
            this.tabview1.Style.ShowTopLine = true;
            this.tabview1.Style.TabsOnBottom = false;
            Terminal.Gui.TabView.Tab tabview1Query;
            tabview1Query = new Terminal.Gui.TabView.Tab("Query", new View());
            tabview1Query.View.Width = Dim.Fill();
            tabview1Query.View.Height = Dim.Fill();
            this.label1 = new Terminal.Gui.Label();
            this.label1.Width = 6;
            this.label1.Height = 1;
            this.label1.X = 8;
            this.label1.Y = 0;
            this.label1.Data = "label1";
            this.label1.Text = "Query:";
            this.label1.TextAlignment = Terminal.Gui.TextAlignment.Left;
            tabview1Query.View.Add(this.label1);
            this.label2 = new Terminal.Gui.Label();
            this.label2.Width = 12;
            this.label2.Height = 1;
            this.label2.X = 2;
            this.label2.Y = 2;
            this.label2.Data = "label2";
            this.label2.Text = "Temporality:";
            this.label2.TextAlignment = Terminal.Gui.TextAlignment.Left;
            tabview1Query.View.Add(this.label2);
            this.tbQuery = new Terminal.Gui.TextField();
            this.tbQuery.Width = Dim.Fill(0);
            this.tbQuery.Height = 1;
            this.tbQuery.X = 15;
            this.tbQuery.Y = 0;
            this.tbQuery.Secret = false;
            this.tbQuery.Data = "tbQuery";
            this.tbQuery.Text = "";
            this.tbQuery.TextAlignment = Terminal.Gui.TextAlignment.Left;
            tabview1Query.View.Add(this.tbQuery);
            this.label3 = new Terminal.Gui.Label();
            this.label3.Width = 9;
            this.label3.Height = 1;
            this.label3.X = 31;
            this.label3.Y = 2;
            this.label3.Data = "label3";
            this.label3.Text = "Negation:";
            this.label3.TextAlignment = Terminal.Gui.TextAlignment.Left;
            tabview1Query.View.Add(this.label3);
            this.label4 = new Terminal.Gui.Label();
            this.label4.Width = 5;
            this.label4.Height = 1;
            this.label4.X = 8;
            this.label4.Y = 6;
            this.label4.Data = "label4";
            this.label4.Text = "From:";
            this.label4.TextAlignment = Terminal.Gui.TextAlignment.Left;
            tabview1Query.View.Add(this.label4);
            this.label5 = new Terminal.Gui.Label();
            this.label5.Width = 3;
            this.label5.Height = 1;
            this.label5.X = 28;
            this.label5.Y = 6;
            this.label5.Data = "label5";
            this.label5.Text = "To:";
            this.label5.TextAlignment = Terminal.Gui.TextAlignment.Left;
            tabview1Query.View.Add(this.label5);
            this.dptStartDate = new Terminal.Gui.DateField();
            this.dptStartDate.Width = 12;
            this.dptStartDate.Height = 1;
            this.dptStartDate.X = 14;
            this.dptStartDate.Y = 6;
            this.dptStartDate.Secret = false;
            this.dptStartDate.Data = "dptStartDate";
            this.dptStartDate.Text = " 01/01/0001";
            this.dptStartDate.TextAlignment = Terminal.Gui.TextAlignment.Left;
            tabview1Query.View.Add(this.dptStartDate);
            this.dptEndDate = new Terminal.Gui.DateField();
            this.dptEndDate.Width = 12;
            this.dptEndDate.Height = 1;
            this.dptEndDate.X = 32;
            this.dptEndDate.Y = 6;
            this.dptEndDate.Secret = false;
            this.dptEndDate.Data = "dptEndDate";
            this.dptEndDate.Text = " 01/01/0001";
            this.dptEndDate.TextAlignment = Terminal.Gui.TextAlignment.Left;
            tabview1Query.View.Add(this.dptEndDate);
            this.cbUseStartDate = new Terminal.Gui.CheckBox();
            this.cbUseStartDate.Width = 3;
            this.cbUseStartDate.Height = 3;
            this.cbUseStartDate.X = 12;
            this.cbUseStartDate.Y = 8;
            this.cbUseStartDate.Data = "cbUseStartDate";
            this.cbUseStartDate.Text = "Use Start Date";
            this.cbUseStartDate.TextAlignment = Terminal.Gui.TextAlignment.Left;
            this.cbUseStartDate.Checked = true;
            tabview1Query.View.Add(this.cbUseStartDate);
            this.cbUseEndDate = new Terminal.Gui.CheckBox();
            this.cbUseEndDate.Width = 3;
            this.cbUseEndDate.Height = 3;
            this.cbUseEndDate.X = 30;
            this.cbUseEndDate.Y = 8;
            this.cbUseEndDate.Data = "cbUseEndDate";
            this.cbUseEndDate.Text = "Use End Date";
            this.cbUseEndDate.TextAlignment = Terminal.Gui.TextAlignment.Left;
            this.cbUseEndDate.Checked = true;
            tabview1Query.View.Add(this.cbUseEndDate);
            this.rgNegation = new Terminal.Gui.RadioGroup();
            this.rgNegation.Width = 11;
            this.rgNegation.Height = 3;
            this.rgNegation.X = 41;
            this.rgNegation.Y = 2;
            this.rgNegation.Data = "rgNegation";
            this.rgNegation.Text = "";
            this.rgNegation.TextAlignment = Terminal.Gui.TextAlignment.Left;
            this.rgNegation.RadioLabels = new NStack.ustring[] {
                    "Any",
                    "Negated",
                    "Affirmed"};
            tabview1Query.View.Add(this.rgNegation);
            this.label6 = new Terminal.Gui.Label();
            this.label6.Width = 13;
            this.label6.Height = 1;
            this.label6.X = 0;
            this.label6.Y = 10;
            this.label6.Data = "label6";
            this.label6.Text = "Return Field:";
            this.label6.TextAlignment = Terminal.Gui.TextAlignment.Left;
            tabview1Query.View.Add(this.label6);
            this.rgReturnField = new Terminal.Gui.RadioGroup();
            this.rgReturnField.Width = 20;
            this.rgReturnField.Height = 3;
            this.rgReturnField.X = 15;
            this.rgReturnField.Y = 10;
            this.rgReturnField.Data = "rgReturnField";
            this.rgReturnField.Text = "";
            this.rgReturnField.TextAlignment = Terminal.Gui.TextAlignment.Left;
            this.rgReturnField.RadioLabels = new NStack.ustring[] {
                    "StudyInstanceUID",
                    "SeriesInstanceUID",
                    "SOPInstanceUID"};
            tabview1Query.View.Add(this.rgReturnField);
            this.label7 = new Terminal.Gui.Label();
            this.label7.Width = 11;
            this.label7.Height = 1;
            this.label7.X = 1;
            this.label7.Y = 14;
            this.label7.Data = "label7";
            this.label7.Text = "Modalities:";
            this.label7.TextAlignment = Terminal.Gui.TextAlignment.Left;
            tabview1Query.View.Add(this.label7);
            this.tbModalities = new Terminal.Gui.TextField();
            this.tbModalities.Width = Dim.Fill(0);
            this.tbModalities.Height = 1;
            this.tbModalities.X = 15;
            this.tbModalities.Y = 14;
            this.tbModalities.Secret = false;
            this.tbModalities.Data = "tbModalities";
            this.tbModalities.Text = "";
            this.tbModalities.TextAlignment = Terminal.Gui.TextAlignment.Left;
            tabview1Query.View.Add(this.tbModalities);
            this.label10 = new Terminal.Gui.Label();
            this.label10.Width = 45;
            this.label10.Height = 1;
            this.label10.X = 15;
            this.label10.Y = 15;
            this.label10.Data = "label10";
            this.label10.Text = "(Enter as comma separated list e.g. CT,MR,DX)";
            this.label10.TextAlignment = Terminal.Gui.TextAlignment.Left;
            tabview1Query.View.Add(this.label10);
            this.cbRecent = new Terminal.Gui.CheckBox();
            this.cbRecent.Width = 18;
            this.cbRecent.Height = 5;
            this.cbRecent.X = 15;
            this.cbRecent.Y = 2;
            this.cbRecent.Data = "cbRecent";
            this.cbRecent.Text = "Recent";
            this.cbRecent.TextAlignment = Terminal.Gui.TextAlignment.Left;
            this.cbRecent.Checked = false;
            tabview1Query.View.Add(this.cbRecent);
            this.cbHistorical = new Terminal.Gui.CheckBox();
            this.cbHistorical.Width = 18;
            this.cbHistorical.Height = 5;
            this.cbHistorical.X = 15;
            this.cbHistorical.Y = 3;
            this.cbHistorical.Data = "cbHistorical";
            this.cbHistorical.Text = "Historical";
            this.cbHistorical.TextAlignment = Terminal.Gui.TextAlignment.Left;
            this.cbHistorical.Checked = false;
            tabview1Query.View.Add(this.cbHistorical);
            this.cbHypothetical = new Terminal.Gui.CheckBox();
            this.cbHypothetical.Width = 18;
            this.cbHypothetical.Height = 5;
            this.cbHypothetical.X = 15;
            this.cbHypothetical.Y = 4;
            this.cbHypothetical.Data = "cbHypothetical";
            this.cbHypothetical.Text = "Hypothetical";
            this.cbHypothetical.TextAlignment = Terminal.Gui.TextAlignment.Left;
            this.cbHypothetical.Checked = false;
            tabview1Query.View.Add(this.cbHypothetical);
            tabview1.AddTab(tabview1Query, false);
            Terminal.Gui.TabView.Tab tabview1Settings;
            tabview1Settings = new Terminal.Gui.TabView.Tab("Settings", new View());
            tabview1Settings.View.Width = Dim.Fill();
            tabview1Settings.View.Height = Dim.Fill();
            this.label8 = new Terminal.Gui.Label();
            this.label8.Width = 17;
            this.label8.Height = 1;
            this.label8.X = 14;
            this.label8.Y = 0;
            this.label8.Data = "label8";
            this.label8.Text = "API Endpoint URL:";
            this.label8.TextAlignment = Terminal.Gui.TextAlignment.Left;
            tabview1Settings.View.Add(this.label8);
            this.label82 = new Terminal.Gui.Label();
            this.label82.Width = 24;
            this.label82.Height = 1;
            this.label82.X = 7;
            this.label82.Y = 2;
            this.label82.Data = "label82";
            this.label82.Text = "API Endpoint Passphrase:";
            this.label82.TextAlignment = Terminal.Gui.TextAlignment.Left;
            tabview1Settings.View.Add(this.label82);
            this.label83 = new Terminal.Gui.Label();
            this.label83.Width = 30;
            this.label83.Height = 1;
            this.label83.X = 1;
            this.label83.Y = 4;
            this.label83.Data = "label83";
            this.label83.Text = "API Request Timeout (seconds):";
            this.label83.TextAlignment = Terminal.Gui.TextAlignment.Left;
            tabview1Settings.View.Add(this.label83);
            this.label832 = new Terminal.Gui.Label();
            this.label832.Width = 28;
            this.label832.Height = 1;
            this.label832.X = 3;
            this.label832.Y = 6;
            this.label832.Data = "label832";
            this.label832.Text = "HTTP Basic Auth Credentials:";
            this.label832.TextAlignment = Terminal.Gui.TextAlignment.Left;
            tabview1Settings.View.Add(this.label832);
            this.label9 = new Terminal.Gui.Label();
            this.label9.Width = 20;
            this.label9.Height = 1;
            this.label9.X = 11;
            this.label9.Y = 8;
            this.label9.Data = "label9";
            this.label9.Text = "Date From/To Format:";
            this.label9.TextAlignment = Terminal.Gui.TextAlignment.Left;
            tabview1Settings.View.Add(this.label9);
            this.cbValidateServerCertificate = new Terminal.Gui.CheckBox();
            this.cbValidateServerCertificate.Width = 4;
            this.cbValidateServerCertificate.Height = 1;
            this.cbValidateServerCertificate.X = 19;
            this.cbValidateServerCertificate.Y = 10;
            this.cbValidateServerCertificate.Data = "cbValidateServerCertificate";
            this.cbValidateServerCertificate.Text = "Validate Server Certificate";
            this.cbValidateServerCertificate.TextAlignment = Terminal.Gui.TextAlignment.Left;
            this.cbValidateServerCertificate.Checked = true;
            tabview1Settings.View.Add(this.cbValidateServerCertificate);
            this.tbApiEndpointUrl = new Terminal.Gui.TextField();
            this.tbApiEndpointUrl.Width = Dim.Fill(0);
            this.tbApiEndpointUrl.Height = 1;
            this.tbApiEndpointUrl.X = 32;
            this.tbApiEndpointUrl.Y = 0;
            this.tbApiEndpointUrl.Secret = false;
            this.tbApiEndpointUrl.Data = "tbApiEndpointUrl";
            this.tbApiEndpointUrl.Text = "";
            this.tbApiEndpointUrl.TextAlignment = Terminal.Gui.TextAlignment.Left;
            tabview1Settings.View.Add(this.tbApiEndpointUrl);
            this.tbApiEndpointPassphrase = new Terminal.Gui.TextField();
            this.tbApiEndpointPassphrase.Width = Dim.Fill(0);
            this.tbApiEndpointPassphrase.Height = 1;
            this.tbApiEndpointPassphrase.X = 32;
            this.tbApiEndpointPassphrase.Y = 2;
            this.tbApiEndpointPassphrase.Secret = true;
            this.tbApiEndpointPassphrase.Data = "tbApiEndpointPassphrase";
            this.tbApiEndpointPassphrase.Text = "";
            this.tbApiEndpointPassphrase.TextAlignment = Terminal.Gui.TextAlignment.Left;
            tabview1Settings.View.Add(this.tbApiEndpointPassphrase);
            this.tbTimeout = new Terminal.Gui.TextField();
            this.tbTimeout.Width = 20;
            this.tbTimeout.Height = 1;
            this.tbTimeout.X = 32;
            this.tbTimeout.Y = 4;
            this.tbTimeout.Secret = false;
            this.tbTimeout.Data = "tbTimeout";
            this.tbTimeout.Text = "";
            this.tbTimeout.TextAlignment = Terminal.Gui.TextAlignment.Left;
            tabview1Settings.View.Add(this.tbTimeout);
            this.tbDateFormat = new Terminal.Gui.TextField();
            this.tbDateFormat.Width = Dim.Fill(0);
            this.tbDateFormat.Height = 1;
            this.tbDateFormat.X = 32;
            this.tbDateFormat.Y = 8;
            this.tbDateFormat.Secret = false;
            this.tbDateFormat.Data = "tbDateFormat";
            this.tbDateFormat.Text = "";
            this.tbDateFormat.TextAlignment = Terminal.Gui.TextAlignment.Left;
            tabview1Settings.View.Add(this.tbDateFormat);
            this.tbBasicAuthCredentialsId = new Terminal.Gui.TextField();
            this.tbBasicAuthCredentialsId.Width = 20;
            this.tbBasicAuthCredentialsId.Height = 1;
            this.tbBasicAuthCredentialsId.X = 32;
            this.tbBasicAuthCredentialsId.Y = 6;
            this.tbBasicAuthCredentialsId.Secret = false;
            this.tbBasicAuthCredentialsId.Data = "tbBasicAuthCredentialsId";
            this.tbBasicAuthCredentialsId.Text = "";
            this.tbBasicAuthCredentialsId.TextAlignment = Terminal.Gui.TextAlignment.Left;
            tabview1Settings.View.Add(this.tbBasicAuthCredentialsId);
            tabview1.AddTab(tabview1Settings, false);
            this.tabview1.ApplyStyleChanges();
            this.Add(this.tabview1);
            this.btnSave = new Terminal.Gui.Button();
            this.btnSave.Width = 8;
            this.btnSave.Height = 1;
            this.btnSave.X = 0;
            this.btnSave.Y = Pos.Bottom(tabview1);
            this.btnSave.Data = "btnSave";
            this.btnSave.Text = "Save";
            this.btnSave.TextAlignment = Terminal.Gui.TextAlignment.Centered;
            this.btnSave.IsDefault = false;
            this.Add(this.btnSave);
            this.btnCancel = new Terminal.Gui.Button();
            this.btnCancel.Width = 10;
            this.btnCancel.Height = 1;
            this.btnCancel.X = 10;
            this.btnCancel.Y = Pos.Bottom(tabview1);
            this.btnCancel.Data = "btnCancel";
            this.btnCancel.Text = "Cancel";
            this.btnCancel.TextAlignment = Terminal.Gui.TextAlignment.Centered;
            this.btnCancel.IsDefault = false;
            this.Add(this.btnCancel);
        }
    }
}
