using DicomTypeTranslation.TableCreation;
using FAnsi.Discovery;
using Rdmp.Core.CommandExecution;
using Rdmp.Core.CommandExecution.AtomicCommands;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.DataLoad;
using System;
using System.IO;
using System.Linq;

namespace Rdmp.Dicom.CommandExecution
{
    public class ExecuteCommandAddModality : BasicCommandExecution
    {
        private readonly DiscoveredDatabase _live;
        private readonly LoadMetadata _lmd;
        private readonly string _prefix;
        private ExecuteCommandCreateNewImagingDatasetSuite _schemaCreationCommand;

        private const string StudyTable = "StudyTable";
        private const string SeriesTable = "SeriesTable";
        private const string ImageTable = "ImageTable";

        public ExecuteCommandAddModality(IBasicActivateItems activator,
            [DemandsInitialization("The schema template (.it) file that will be used to determine image tables' columns")]
            FileInfo template,
            [DemandsInitialization("The prefix name for the new modality e.g. CT")]
            string modality,
            [DemandsInitialization("The existing imaging load you want to update to load these new tables.  Must closely match expectations of this command")]
            LoadMetadata lmd) : base(activator)
        {
            _lmd = lmd;
            try
            {
                _live = lmd.GetDistinctLiveDatabaseServer().GetCurrentDatabase();
            }
            catch(Exception ex)
            {
                SetImpossible($"Could not get single live server from the LoadMetadata {lmd} :" + ex);
                return;
            }

            if(_live == null || !_live.Exists())
            {
                SetImpossible($"Live database for {lmd} was null or did not exist, cannot update it for a new modality");
                return;
            }

            if(modality.EndsWith("_"))
            {
                _prefix = modality;
            }
            else
            {
                _prefix = modality + "_";
            }
            
            // the underlying command that does most of the work (creating tables), the rest is tinkering (join creation for study/series/image etc)
            _schemaCreationCommand = new ExecuteCommandCreateNewImagingDatasetSuite(activator.RepositoryLocator, _live, null, null, _prefix, template, false, false);

            if (_schemaCreationCommand.IsImpossible)
            {
                SetImpossible(_schemaCreationCommand.ReasonCommandImpossible);
                return;
            }

            ValidateTemplate(_schemaCreationCommand.Template);
        }

        private void ValidateTemplate(ImageTableTemplateCollection template)
        {
            if(template.Tables.Count != 3)
            {
                SetImpossible("Expected template to contain exactly 3 tables (StudyTable, SeriesTable and ImageTable)");
                return;
            }

            foreach(var n in new[] { StudyTable,SeriesTable,ImageTable})
            {
                if (template.Tables.Any(t => t.TableName.Equals(n)))
                {
                    SetImpossible($"Template did not contain an expected table name: {n}.  Table names in template were {string.Join(",",template.Tables.Select(t=>t.TableName))}");
                    return;
                }
            }
        }

        public override void Execute()
        {
            base.Execute();

            _schemaCreationCommand.Execute();

            var studyCata = (Catalogue) _schemaCreationCommand.NewCataloguesCreated.Single(c => c.Name.Contains(StudyTable));
            var seriesCata = (Catalogue)_schemaCreationCommand.NewCataloguesCreated.Single(c => c.Name.Contains(SeriesTable));
            var imageCata = (Catalogue)_schemaCreationCommand.NewCataloguesCreated.Single(c => c.Name.Contains(ImageTable));

            var cmdAssoc = new ExecuteCommandAssociateCatalogueWithLoadMetadata(BasicActivator, _lmd, new[] { studyCata, seriesCata, imageCata });
            cmdAssoc.Execute();

            // TODO: create JoinInfos
            // TODO: mark study TableInfo IsPrimary 
            // TODO: update Distincters in load
            // TODO: update Coalsecers in load
            // TODO: update/add a new Isolation mutilation if lmd has them already

        }
    }
}

