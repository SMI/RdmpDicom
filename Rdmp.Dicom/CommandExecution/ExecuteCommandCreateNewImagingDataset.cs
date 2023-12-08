using System.Linq;
using FAnsi.Discovery;
using Rdmp.Core.Repositories;
using Rdmp.Core.Curation;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.DataExport.Data;
using DicomTypeTranslation.TableCreation;
using Rdmp.Core.CommandExecution;

namespace Rdmp.Dicom.CommandExecution;

public class ExecuteCommandCreateNewImagingDataset:BasicCommandExecution
{
    private readonly ImageTableTemplate _tableTemplate;
    private readonly IRDMPPlatformRepositoryServiceLocator _repositoryLocator;
    private readonly DiscoveredTable _expectedTable;

    public ICatalogue NewCatalogueCreated { get; private set; }

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
        importer.DoImport(out var tis, out var cis);

        var engineer = new ForwardEngineerCatalogue(tis, cis);
        engineer.ExecuteForwardEngineering(out var cata, out _, out var eis);

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