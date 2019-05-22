using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FAnsi.Discovery;
using ReusableUIComponents.Dialogs;
using Rdmp.Dicom.CommandExecution;
using Rdmp.Dicom.PipelineComponents.DicomSources;
using Rdmp.UI.ItemActivation;
using DicomTypeTranslation.TableCreation;
using Rdmp.UI.Refreshing;

namespace Rdmp.Dicom.UI
{
    public partial class CreateNewImagingDatasetUI : Form
    {
        private readonly IActivateItems _activator;

        public CreateNewImagingDatasetUI(IActivateItems activator)
        {
            _activator = activator;

            InitializeComponent();
            serverDatabaseTableSelector1.HideTableComponents();
        }

        private void serverDatabaseTableSelector1_Load(object sender, EventArgs e)
        {

        }

        private bool CreateDatabaseIfNotExists(DiscoveredDatabase db)
        {
            if (db == null)
            {
                MessageBox.Show("Choose a database");
                return false;
            }

            if (!db.Exists())
            {
                if (MessageBox.Show("Create database '" + db + "'", "Create", MessageBoxButtons.YesNo) ==
                    DialogResult.Yes)
                    db.Create();
                else
                    return false;
            }

            return true;
        }


        private void btnCreateEntireSuite_Click(object sender, EventArgs e)
        {
            CreateSuite(null);
        }

        private void btnCreateSuiteWithTemplate_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Imaging Template|*.it";

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var yaml = File.ReadAllText(ofd.FileName);

                    var template = ImageTableTemplateCollection.LoadFrom(yaml);

                    CreateSuite(template);
                }
                catch (Exception exception)
                {
                    ExceptionViewer.Show(exception);
                }
            }
        }

        private void CreateSuite(ImageTableTemplateCollection template)
        {
            var db = serverDatabaseTableSelector1.GetDiscoveredDatabase();

            if (!CreateDatabaseIfNotExists(db))
                return;

            
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            DirectoryInfo dir = null;
            dialog.Description = "Select Project Directory (For Sql scripts/Executables etc)";

            //if we are creating a load we need to know where to store load scripts etc
            if(cbCreateLoad.Checked)
                if (dialog.ShowDialog() == DialogResult.OK)
                    dir = new DirectoryInfo(dialog.SelectedPath);
                else
                    return;

            var cmd = new ExecuteCommandCreateNewImagingDatasetSuite(_activator.RepositoryLocator, db,dir);
            cmd.DicomSourceType = rbJsonSources.Checked ? typeof(DicomDatasetCollectionSource) : typeof(DicomFileCollectionSource);
            cmd.CreateCoalescer = cbMergeNullability.Checked;
            cmd.CreateLoad = cbCreateLoad.Checked;
            cmd.TablePrefix = tbPrefix.Text;
            cmd.Template = template;

            cmd.Execute();

            var firstCata = cmd.NewCataloguesCreated.First();
            if (firstCata != null)
                _activator.RefreshBus.Publish(this, new RefreshObjectEventArgs(firstCata));

            MessageBox.Show("Create Suite Completed");
        }

        private void cbCreateLoad_CheckedChanged(object sender, EventArgs e)
        {
            cbMergeNullability.Enabled = cbCreateLoad.Checked;
            rbFileSources.Enabled = cbCreateLoad.Checked;
            rbJsonSources.Enabled = cbCreateLoad.Checked;
        }

    }
}
