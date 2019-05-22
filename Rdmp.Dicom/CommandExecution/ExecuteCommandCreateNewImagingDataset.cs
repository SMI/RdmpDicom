using System.Linq;
using ReusableLibraryCode.CommandExecution;
using FAnsi.Discovery;
using Rdmp.Core.Repositories;
using Rdmp.Core.Curation;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.DataExport.Data;
using DicomTypeTranslation.TableCreation;

namespace Rdmp.Dicom.CommandExecution
{
    public class ExecuteCommandCreateNewImagingDataset:BasicCommandExecution
    {
        private readonly ImageTableTemplate _tableTemplate;
        private readonly IRDMPPlatformRepositoryServiceLocator _repositoryLocator;
        private readonly DiscoveredTable _expectedTable;
        
        public Catalogue NewCatalogueCreated { get; private set; }
        
        public ExecuteCommandCreateNewImagingDataset(IRDMPPlatformRepositoryServiceLocator repositoryLocator, DiscoveredTable expectedTable, ImageTableTemplate tableTemplate)
        {
            _repositoryLocator = repositoryLocator;
            _expectedTable = expectedTable;
            _tableTemplate = tableTemplate;
        }

        public override void Execute()
        {
            base.Execute();

            var tableCreator = new ImagingTableCreation(_expectedTable.Database.Server.GetQuerySyntaxHelper());
            tableCreator.CreateTable(_expectedTable, _tableTemplate);

            var importer = new TableInfoImporter(_repositoryLocator.CatalogueRepository, _expectedTable);
            TableInfo tis;
            ColumnInfo[] cis;
            importer.DoImport(out tis, out cis);

            var engineer = new ForwardEngineerCatalogue(tis, cis, true);
            Catalogue cata;
            CatalogueItem[] cataItems;
            ExtractionInformation[] eis;
            engineer.ExecuteForwardEngineering(out cata, out cataItems, out eis);

            var patientIdentifier = eis.SingleOrDefault(e => e.GetRuntimeName().Equals("PatientID"));

            if(patientIdentifier != null)
            {
                patientIdentifier.IsExtractionIdentifier = true;
                patientIdentifier.SaveToDatabase();
            }
            var seriesEi = eis.SingleOrDefault(e => e.GetRuntimeName().Equals("SeriesInstanceUID"));
            if (seriesEi != null)
            {
                seriesEi.IsExtractionIdentifier = true;
                seriesEi.SaveToDatabase();
            }
            
            //make it extractable
            new ExtractableDataSet(_repositoryLocator.DataExportRepository, cata);

            NewCatalogueCreated = cata;
        }
    }
}