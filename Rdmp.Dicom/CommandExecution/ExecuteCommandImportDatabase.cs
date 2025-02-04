using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Rdmp.Core.ReusableLibraryCode.Checks;
using FAnsi.Discovery;
using Rdmp.Dicom.Attachers.Routing;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Repositories;
using Rdmp.Core.Curation.Data.DataLoad;
using Rdmp.Core.Curation.Data.Pipelines;
using Rdmp.Core.DataLoad.Modules.Mutilators;
using Rdmp.Core.Curation.Data.Defaults;
using Rdmp.Core.Curation;
using Rdmp.Core.DataLoad.Engine.Checks;
using Rdmp.Core.DataLoad;
using Rdmp.Core.CommandExecution;
using Rdmp.Core.Repositories.Construction;
using Rdmp.Dicom.PipelineComponents.DicomSources;
using Rdmp.Core.ReusableLibraryCode.Annotations;
using Rdmp.Core.DataExport.Data;
using Rdmp.Core.DataLoad.Engine.DatabaseManagement.EntityNaming;
using Rdmp.Core.DataLoad.Engine.LoadProcess;

namespace Rdmp.Dicom.CommandExecution;

public partial class ExecuteCommandImportDatabase : BasicCommandExecution
{
    private readonly DiscoveredDatabase _databaseToCreateInto;
    private readonly DirectoryInfo _projectDirectory;
    private readonly IExternalDatabaseServer _loggingServer;
    private readonly IRDMPPlatformRepositoryServiceLocator _repositoryLocator;
    private readonly ICatalogueRepository _catalogueRepository;

    private List<ICatalogue> NewCataloguesCreated { get; }
    private LoadMetadata NewLoadMetadata { get; set; }

    /// <summary>
    /// The component of the data load that will handle reading the Dicom files / json and converting it into DataTables (only populated after Execute has been called).
    /// Note that this is a PipelineComponent meaning it is the template which gets stamped out into a hydrated instance at runtime.  The DicomSourcePipelineComponent.Path Should
    /// contain the DicomSourceType.Name and when the DLE is run the DicomSourceType is the Type that will be created from the template
    /// </summary>
    private PipelineComponent DicomSourcePipelineComponent { get; set; }

    /// <summary>
    /// The DicomSource component Type to use for the Loadmetadata pipeline responsible for loading the dicom metadata into the Catalogues (e.g. DicomDatasetCollectionSource
    /// for Json or DicomFileCollectionSource for files)
    /// </summary>
    private Type DicomSourceType { get; set; }

    /// <summary>
    /// True to create an AutoRoutingAttacherWithPersistentRaw instead of a AutoRoutingAttacher
    /// </summary>
    private bool PersistentRaw { get; set; }

    public ExecuteCommandImportDatabase(IRDMPPlatformRepositoryServiceLocator repositoryLocator, DiscoveredDatabase databaseToCreateInto, DirectoryInfo projectDirectory)
    {
        _repositoryLocator = repositoryLocator;
        _catalogueRepository = repositoryLocator.CatalogueRepository;
        _databaseToCreateInto = databaseToCreateInto;
        _projectDirectory = projectDirectory;
        NewCataloguesCreated = new List<ICatalogue>();

        _loggingServer = _catalogueRepository.GetDefaultFor(PermissableDefaults.LiveLoggingServer_ID);

        if (_loggingServer == null)
            SetImpossible("No default logging server has been configured in your Catalogue database");
    }

    [UsedImplicitly]
    [UseWithObjectConstructor]
    public ExecuteCommandImportDatabase(
        IRDMPPlatformRepositoryServiceLocator repositoryLocator,
        DiscoveredDatabase databaseToCreateInto,
        DirectoryInfo projectDirectory,
        [DemandsInitialization("The pipeline source for reading dicom tags from e.g. from files or from serialized JSON",TypeOf = typeof(DicomSource))]
        Type dicomSourceType,
        bool persistentRaw
    ) : this(repositoryLocator, databaseToCreateInto, projectDirectory)
    {
        DicomSourceType = dicomSourceType ?? typeof(DicomFileCollectionSource);
        PersistentRaw = persistentRaw;
    }

    public override void Execute()
    {
        if (DicomSourceType == null)
        {
            SetImpossible("You must specify a Type for DicomSourceType");
            throw new ImpossibleCommandException(this, ReasonCommandImpossible);
        }

        base.Execute();

        // create the database if it does not exist
        if (!_databaseToCreateInto.Exists() || !_databaseToCreateInto.Server.Exists())
            throw new Exception($"Database '{_databaseToCreateInto.GetRuntimeName()}' did not exist");

        List<DiscoveredTable> tables = [];
        foreach (var t in _databaseToCreateInto.DiscoverTables(false))
        {
            var tableBits = TableNameRegex().Match(t.GetRuntimeName());
            if (!tableBits.Success)
                continue;

            tables.Add(t);

            var importer = new TableInfoImporter(_repositoryLocator.CatalogueRepository, t);
            importer.DoImport(out var tis, out var cis);

            // Mark table as primary extraction table for this modality if it is the Study table, or the only SR/OTHER table
            if (tableBits.Groups[0].Value.Equals("SR", StringComparison.Ordinal)
                || tableBits.Groups[0].Value.Equals("OTHER", StringComparison.Ordinal)
                || tableBits.Groups[1].Value.Equals("Study", StringComparison.Ordinal))
                tis.IsPrimaryExtractionTable = true;

            // TODO: Create JoinInfo for Image<->Series<->Study tables
            //   - newobject joininfo columninfo:*${modality}_*ImageTable\`*Series*UID* columninfo:*${modality}_*SeriesTable\`*Series*UID* right null
            //   - newobject joininfo columninfo:*${modality}_*SeriesTable\`*Study*UID* columninfo:*${modality}_*StudyTable\`*Study*UID* right null

            var engineer = new ForwardEngineerCatalogue(tis, cis);
            engineer.ExecuteForwardEngineering(out var cata, out _, out var eis);
            var patientIdentifier = eis.SingleOrDefault(static e => e.GetRuntimeName()?.Equals("PatientID") == true);

            if (patientIdentifier != null)
            {
                patientIdentifier.IsExtractionIdentifier = true;
                patientIdentifier.SaveToDatabase();
            }

            var seriesEi = eis.SingleOrDefault(static e => e.GetRuntimeName()?.Equals("SeriesInstanceUID") == true);
            if (seriesEi != null)
            {
                seriesEi.IsExtractionIdentifier = true;
                seriesEi.SaveToDatabase();
            }

            //make it extractable
            _ = new ExtractableDataSet(_repositoryLocator.DataExportRepository, cata);

            NewCataloguesCreated.Add(cata);
        }

        const string loadName = "SMI Image Loading";

        NewLoadMetadata = new LoadMetadata(_catalogueRepository, loadName);

        //tell all the catalogues that they are part of this load and where to log under the same task
        foreach (var c in NewCataloguesCreated)
        {
            NewLoadMetadata.LinkToCatalogue(c);
            c.LoggingDataTask = loadName;
            c.LiveLoggingServer_ID = _loggingServer.ID;
            c.SaveToDatabase();
        }

        //create the logging task
        new Core.Logging.LogManager(_loggingServer).CreateNewLoggingTaskIfNotExists(loadName);

        var projDir = LoadDirectory.CreateDirectoryStructure(_projectDirectory, "ImageLoading", true);
        NewLoadMetadata.LocationOfForLoadingDirectory = projDir.ForLoading.FullName;
        NewLoadMetadata.LocationOfForArchivingDirectory = projDir.ForArchiving.FullName;
        NewLoadMetadata.LocationOfExecutablesDirectory = projDir.ExecutablesPath.FullName;
        NewLoadMetadata.LocationOfCacheDirectory = projDir.Cache.FullName;
        NewLoadMetadata.SaveToDatabase();

        /////////////////////////////////////////////Attacher////////////////////////////


        //Create a pipeline for reading from Dicom files and writing to any destination component (which must be fixed)
        var name = "Image Loading Pipe";
        name = MakeUniqueName(_catalogueRepository.GetAllObjects<Pipeline>().Select(static p => p.Name).ToArray(), name);

        var pipe = new Pipeline(_catalogueRepository, name);
        DicomSourcePipelineComponent = new PipelineComponent(_catalogueRepository, pipe, DicomSourceType, 0, DicomSourceType.Name);
        DicomSourcePipelineComponent.CreateArgumentsForClassIfNotExists(DicomSourceType);

        // Set the argument for only populating tags who appear in the end tables of the load (no need for source to read all the tags only those we are actually loading)
        var arg = DicomSourcePipelineComponent.GetAllArguments().FirstOrDefault(static a => a.Name.Equals(nameof(DicomSource.UseAllTableInfoInLoadAsFieldMap)));
        if (arg != null)
        {
            arg.SetValue(NewLoadMetadata);
            arg.SaveToDatabase();
        }

        pipe.SourcePipelineComponent_ID = DicomSourcePipelineComponent.ID;
        pipe.SaveToDatabase();


        //Create the load process task that uses the pipe to load RAW tables with data from the dicom files
        var pt = new ProcessTask(_catalogueRepository, NewLoadMetadata, LoadStage.Mounting)
        {
            Name = "Auto Routing Attacher",
            ProcessTaskType = ProcessTaskType.Attacher,
            Path = PersistentRaw
                ? typeof(AutoRoutingAttacherWithPersistentRaw).FullName
                : typeof(AutoRoutingAttacher).FullName,
            Order = 1
        };


        pt.SaveToDatabase();

        var args = PersistentRaw ? pt.CreateArgumentsForClassIfNotExists<AutoRoutingAttacherWithPersistentRaw>() : pt.CreateArgumentsForClassIfNotExists<AutoRoutingAttacher>();
        SetArgument(args, "LoadPipeline", pipe);

        /////////////////////////////////////// Distinct tables on load /////////////////////////


        var distincter = new ProcessTask(_catalogueRepository, NewLoadMetadata, LoadStage.AdjustRaw);
        var distincterArgs = distincter.CreateArgumentsForClassIfNotExists<Distincter>();

        distincter.Name = "Distincter";
        distincter.ProcessTaskType = ProcessTaskType.MutilateDataTable;
        distincter.Path = typeof(Distincter).FullName;
        distincter.Order = 2;
        distincter.SaveToDatabase();
        SetArgument(distincterArgs, "TableRegexPattern", ".*");

        /////////////////////////////////////////////////////////////////////////////////////

        if (true)
        {
            var coalescer = new ProcessTask(_catalogueRepository, NewLoadMetadata, LoadStage.AdjustRaw)
            {
                Name = "Coalescer",
                ProcessTaskType = ProcessTaskType.MutilateDataTable,
                Path = typeof(Coalescer).FullName,
                Order = 3
            };
            coalescer.SaveToDatabase();

            var regexPattern = tables
                .Where(static tbl => !tbl.DiscoverColumns().Any(static c =>
                    c.GetRuntimeName().Equals("SOPInstanceUID", StringComparison.CurrentCultureIgnoreCase)))
                .Select(static tbl => $"({tbl.GetRuntimeName()})");


            var coalArgs = coalescer.CreateArgumentsForClassIfNotExists<Coalescer>();
            SetArgument(coalArgs, "TableRegexPattern", string.Join('|', regexPattern));
            SetArgument(coalArgs, "CreateIndex", true);
        }

        ////////////////////////////////Load Ender (if no rows in load) ////////////////////////////

        var prematureLoadEnder = new ProcessTask(_catalogueRepository, NewLoadMetadata, LoadStage.Mounting)
        {
            Name = "Premature Load Ender",
            ProcessTaskType = ProcessTaskType.MutilateDataTable,
            Path = typeof(PrematureLoadEnder).FullName,
            Order = 4
        };
        prematureLoadEnder.SaveToDatabase();

        args = prematureLoadEnder.CreateArgumentsForClassIfNotExists<PrematureLoadEnder>();
        SetArgument(args, "ExitCodeToReturnIfConditionMet", ExitCodeType.OperationNotRequired);
        SetArgument(args, "ConditionsToTerminateUnder", PrematureLoadEndCondition.NoRecordsInAnyTablesInDatabase);

        ////////////////////////////////////////////////////////////////////////////////////////////////

        var checker = new CheckEntireDataLoadProcess(BasicActivator, NewLoadMetadata, new HICDatabaseConfiguration(NewLoadMetadata), new HICLoadConfigurationFlags());
        checker.Check(new AcceptAllCheckNotifier());
    }

    private static string MakeUniqueName(string[] existingUsedNames, string candidate)
    {
        // if name is unique then keep candidate name
        if (!existingUsedNames.Any(p => p.Equals(candidate, StringComparison.CurrentCultureIgnoreCase)))
            return candidate;

        // otherwise give it a suffix
        var suffix = 2;
        while (existingUsedNames.Any(p => p.Equals(candidate + suffix, StringComparison.CurrentCultureIgnoreCase)))
        {
            suffix++;
        }

        return candidate + suffix;
    }

    private static void SetArgument(IArgument[] args, string property, object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var arg = args.Single(a => a.Name.Equals(property));

        if (MEF.GetType(value.GetType().FullName) == null)
            throw new ArgumentException($"No type found for {value.GetType().FullName}");

        //if this fails, look to see if GetType returned null (indicates that your Type is not loaded by MEF).  Look at mef.DescribeBadAssembliesIfAny() to investigate this issue
        arg.SetValue(value);
        arg.SaveToDatabase();
    }

    [GeneratedRegex("^([A-Z]+)_(Image|Series|Study)Table$")]
    private static partial Regex TableNameRegex();
}
