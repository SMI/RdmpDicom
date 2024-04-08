
//------------------------------------------------------------------------------

//  <auto-generated>
//      This code was generated by:
//        TerminalGuiDesigner v1.0.9.0
//      You can make changes to this file and they will not be overwritten when saving.
//  </auto-generated>
// -----------------------------------------------------------------------------

using System.Collections.Generic;

namespace Rdmp.Dicom;

using Rdmp.Core.CommandExecution;
using Core.Curation.Data.Aggregation;
using ExternalApis;
using System.Linq;
using Terminal.Gui;

public partial class SemEHRConsoleUI {


    private readonly SemEHRConfiguration _configuration;
    public AggregateConfiguration Aggregate { get; }

    public SemEHRConsoleUI(IBasicActivateItems activator, SemEHRApiCaller api, AggregateConfiguration aggregate) {
        InitializeComponent();

        _configuration = SemEHRConfiguration.LoadFrom(aggregate);
        Aggregate = aggregate;

        // Load list data
        cbRecent.Checked = _configuration.Temporality.Contains("Recent");
        cbHistorical.Checked = _configuration.Temporality.Contains("Historical");
        cbHypothetical.Checked = _configuration.Temporality.Contains("Hypothetical");

        rgNegation.SelectedItem = rgNegation.RadioLabels.ToList().IndexOf(_configuration.Negation);
        tbModalities.Text = string.Join(',', _configuration.Modalities.ToArray());

        rgReturnField.SelectedItem = rgReturnField.RadioLabels.ToList().IndexOf(_configuration.ReturnField);

        //Set stored values
        tbApiEndpointUrl.Text = _configuration.Url;
        cbValidateServerCertificate.Checked = _configuration.ValidateServerCert;
        tbApiEndpointPassphrase.Text = _configuration.Passphrase;
        tbTimeout.Text = _configuration.RequestTimeout.ToString();

        tbBasicAuthCredentialsId.Text = _configuration.ApiHttpDataAccessCredentials.ToString();

        tbDateFormat.Text = _configuration.StartEndDateFormat;
        tbQuery.Text = _configuration.Query;

        cbUseStartDate.Checked = _configuration.UseStartDate;
        dptStartDate.Date = _configuration.StartDate;
        cbUseEndDate.Checked = _configuration.UseEndDate;
        dptEndDate.Date = _configuration.EndDate;

        btnSave.Clicked += BtnSave_Clicked;
        btnCancel.Clicked += BtnCancel_Clicked;
    }


    private void BtnSave_Clicked()
    {
        // copy values from controls into config
        _configuration.Url = tbApiEndpointUrl.Text.ToString();
        _configuration.ValidateServerCert = cbValidateServerCertificate.Checked;
        _configuration.Passphrase = tbApiEndpointPassphrase.Text.ToString();
        _configuration.RequestTimeout = int.Parse(tbTimeout.Text.ToString());
        _configuration.ApiHttpDataAccessCredentials = int.Parse(tbBasicAuthCredentialsId.Text.ToString());
        _configuration.StartEndDateFormat = tbDateFormat.Text.ToString();
        _configuration.Query = tbQuery.Text.ToString();

        _configuration.Temporality = new List<string>();

        if (cbRecent.Checked)
            _configuration.Temporality.Add(cbRecent.Text.ToString());
        if (cbHistorical.Checked)
            _configuration.Temporality.Add(cbHistorical.Text.ToString());
        if (cbHypothetical.Checked)
            _configuration.Temporality.Add(cbHypothetical.Text.ToString());

        _configuration.Negation = rgNegation.RadioLabels[rgNegation.SelectedItem].ToString();

        _configuration.UseStartDate = cbUseStartDate.Checked;
        _configuration.StartDate = dptStartDate.Date;
        _configuration.UseEndDate = cbUseEndDate.Checked;
        _configuration.EndDate = dptEndDate.Date;

        _configuration.Modalities = new List<string>();
        if(!string.IsNullOrEmpty(tbModalities.Text.ToString()))
            foreach(var m in tbModalities.Text.ToString().Split(","))
            {
                _configuration.Modalities.Add(m);
            }

        _configuration.ReturnField = rgReturnField.RadioLabels[rgReturnField.SelectedItem].ToString();

        // save the config to the database
        Aggregate.Description = _configuration.Serialize();
        Aggregate.SaveToDatabase();

        Application.RequestStop();
    }
    private void BtnCancel_Clicked()
    {
        Application.RequestStop();
    }
}