using AutocompleteMenuNS;
using Dicom;
using DicomTypeTranslation.Elevation.Serialization;
using Rdmp.Core.Curation.Data.DataLoad;
using Rdmp.Core.Repositories;
using Rdmp.Dicom.PipelineComponents.DicomSources;
using ReusableLibraryCode.Checks;
using ScintillaNET;
using System;
using System.Linq;
using System.Windows.Forms;
using Rdmp.Core.Icons.IconProvision;
using Rdmp.UI.ScintillaHelper;

namespace Rdmp.Dicom.UI
{
    public partial class TagElevationXmlUI : Form,ICustomUI<DicomSource.TagElevationXml>
    {
        private Scintilla queryEditor;


        public const string ExampleElevationFile
            = @"<!DOCTYPE TagElevationRequestCollection
[
  <!ELEMENT TagElevationRequestCollection (TagElevationRequest*)>
  <!ELEMENT TagElevationRequest (ColumnName,ElevationPathway,Conditional?)>
  <!ELEMENT ColumnName (#PCDATA)>
  <!ELEMENT ElevationPathway (#PCDATA)>
  <!ELEMENT Conditional (ConditionalPathway,ConditionalRegex)>
  <!ELEMENT ConditionalPathway (#PCDATA)>
  <!ELEMENT ConditionalRegex (#PCDATA)>
]>
<TagElevationRequestCollection>
  <TagElevationRequest>
    <ColumnName>TODO:YourColumnName</ColumnName>
    <ElevationPathway>TODO:SequenceTag->TODO:NonSequenceTag</ElevationPathway>
    <!-- TODO: Uncomment for conditionals
<Conditional>
      <ConditionalPathway>.->ConceptNameCodeSequence->CodeMeaning</ConditionalPathway>
      <ConditionalRegex>Tr.*[e-a]{2}tment</ConditionalRegex>
    </Conditional>
-->
  </TagElevationRequest>
</TagElevationRequestCollection>";

        public TagElevationXmlUI()
        {
            InitializeComponent();

            btnOk.Click += Btn_Click;
            btnCancel.Click += Btn_Click;

            var factory = new ScintillaTextEditorFactory();
            queryEditor = factory.Create(null,"xml");
            pEditor.Controls.Add(queryEditor);


            var autoComplete = new AutocompleteMenu {ImageList = new ImageList()};

            autoComplete.ImageList.Images.Add(CatalogueIcons.File);
            autoComplete.MaximumSize = new System.Drawing.Size(300,500);
            
            autoComplete.AddItem(new SubstringAutocompleteItem("TagElevationRequest") { ImageIndex = 0 });
            autoComplete.AddItem(new SubstringAutocompleteItem("ColumnName") { ImageIndex = 0 });
            autoComplete.AddItem(new SubstringAutocompleteItem("ElevationPathway") { ImageIndex = 0 });
            autoComplete.AddItem(new SubstringAutocompleteItem("Conditional") { ImageIndex = 0 });
            autoComplete.AddItem(new SubstringAutocompleteItem("ConditionalPathway"){ ImageIndex = 0 });
            autoComplete.AddItem(new SubstringAutocompleteItem("ConditionalRegex"){ImageIndex = 0 });

            foreach (string keyword in DicomDictionary.Default.Select(e => e.Keyword).Distinct())
                autoComplete.AddItem(new SubstringAutocompleteItem(keyword));

            autoComplete.TargetControlWrapper = new ScintillaWrapper(queryEditor);

            btnRunChecks.Click += (s,e)=>RunChecks();

        }

        private void RunChecks()
        {
            RagSmiley1.Reset();

            try
            {
                new TagElevationRequestCollection(queryEditor.Text);
                RagSmiley1.OnCheckPerformed(new CheckEventArgs("Succesfully created elevator",CheckResult.Success));
            }
            catch(Exception ex)
            {
                RagSmiley1.Fatal(ex);
            }
        }

        public ICatalogueRepository CatalogueRepository { get;set; }

        public ICustomUIDrivenClass GetFinalStateOfUnderlyingObject()
        {
            return new DicomSource.TagElevationXml() { xml = queryEditor.Text};
        }

        public void SetGenericUnderlyingObjectTo(ICustomUIDrivenClass value)
        {
            SetUnderlyingObjectTo((DicomSource.TagElevationXml)value);
        }

        public void SetUnderlyingObjectTo(DicomSource.TagElevationXml value)
        {
            if(value != null && value.xml != null)
            {
                queryEditor.Text = value.xml;
                RunChecks();
            }
            else
                queryEditor.Text = ExampleElevationFile; 
        }

        private void Btn_Click(object sender, EventArgs e)
        {
            DialogResult = sender == btnOk ? DialogResult.OK: DialogResult.Cancel;
            Close();
        }
    }
}
