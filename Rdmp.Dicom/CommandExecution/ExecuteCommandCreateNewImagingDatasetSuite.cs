using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ReusableLibraryCode.Checks;
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
using Rdmp.Core.DataLoad.Engine.DatabaseManagement.EntityNaming;
using Rdmp.Core.DataLoad;
using Rdmp.Core.DataLoad.Engine.LoadProcess;
using DicomTypeTranslation.TableCreation;
using Rdmp.Core.CommandExecution;
using Rdmp.Core.Repositories.Construction;
using Rdmp.Dicom.PipelineComponents.DicomSources;
using ReusableLibraryCode.Annotations;

namespace Rdmp.Dicom.CommandExecution
{
    public class ExecuteCommandCreateNewImagingDatasetSuite : BasicCommandExecution
    {
        
        private readonly DiscoveredDatabase _databaseToCreateInto;
        private readonly DirectoryInfo _projectDirectory;
        private readonly IExternalDatabaseServer _loggingServer;
        private readonly IRDMPPlatformRepositoryServiceLocator _repositoryLocator;
        private readonly ICatalogueRepository _catalogueRepository;

        public List<ICatalogue> NewCataloguesCreated { get; }
        public LoadMetadata NewLoadMetadata { get; private set; }
        
        /// <summary>
        /// The component of the data load that will handle reading the Dicom files / json and converting it into DataTables (only populated after Execute has been called).  
        /// Note that this is a PipelineComponent meaning it is the template which gets stamped out into a hydrated instance at runtime.  The DicomSourcePipelineComponent.Path Should
        /// contain the DicomSourceType.Name and when the DLE is run the DicomSourceType is the Type that will be created from the template
        /// </summary>
        public PipelineComponent DicomSourcePipelineComponent { get; private set; }

        /// <summary>
        /// The DicomSource component Type to use for the Loadmetadata pipeline responsible for loading the dicom metadata into the Catalogues (e.g. DicomDatasetCollectionSource 
        /// for Json or DicomFileCollectionSource for files)
        /// </summary>
        public Type DicomSourceType { get; set; }

        public bool CreateCoalescer { get; set; }
        
        /// <summary>
        /// Optional text to put at the beginning of the Catalogues / Pipeline etc
        /// </summary>
        public string TablePrefix { get; set; }

        /// <summary>
        /// The columns/tables to create instead of <see cref="ImageTableSchema"/>
        /// </summary>
        public ImageTableTemplateCollection Template { get; set; }

        /// <summary>
        /// True to create an AutoRoutingAttacherWithPersistentRaw instead of a AutoRoutingAttacher
        /// </summary>
        public bool PersistentRaw { get; set; }

        /// <summary>
        /// True to create the LoadMetadata load for the tables created.  False to create only the Tables/Catalogues.  Defaults to true
        /// </summary>
        public bool CreateLoad { get; set; }

        public ExecuteCommandCreateNewImagingDatasetSuite(IRDMPPlatformRepositoryServiceLocator repositoryLocator, DiscoveredDatabase databaseToCreateInto, DirectoryInfo projectDirectory)
        {
            _repositoryLocator = repositoryLocator;
            _catalogueRepository = repositoryLocator.CatalogueRepository;
            _databaseToCreateInto = databaseToCreateInto;
            _projectDirectory = projectDirectory;
            NewCataloguesCreated = new();

            _loggingServer = _catalogueRepository.GetDefaultFor(PermissableDefaults.LiveLoggingServer_ID);

            if(_loggingServer == null)
                SetImpossible("No default logging server has been configured in your Catalogue database");
            
            CreateLoad = true;
        }

        [UsedImplicitly]
        [UseWithObjectConstructor]
        public ExecuteCommandCreateNewImagingDatasetSuite(
            IRDMPPlatformRepositoryServiceLocator repositoryLocator,
            DiscoveredDatabase databaseToCreateInto,
            DirectoryInfo projectDirectory,

            [DemandsInitialization("The pipeline source for reading dicom tags from e.g. from files or from serialized JSON",TypeOf = typeof(DicomSource))]
            Type dicomSourceType,
            string tablePrefix,
            FileInfo templateFile,
            bool persistentRaw,
            bool createLoad
            ):this(repositoryLocator, databaseToCreateInto, projectDirectory)
        {
            DicomSourceType = dicomSourceType ?? typeof(DicomFileCollectionSource);
            TablePrefix = tablePrefix;
            Template = ImageTableTemplateCollection.LoadFrom(File.ReadAllText(templateFile.FullName));
            PersistentRaw = persistentRaw;
            CreateLoad = createLoad;
        }

        public override void Execute()
        {
            if (DicomSourceType == null)
            {
                SetImpossible("You must specify a Type for DicomSourceType");
                throw new ImpossibleCommandException(this, ReasonCommandImpossible);
            }

            base.Execute();

            List<DiscoveredTable> tablesCreated = new();

            // create the database if it does not exist
            if(!_databaseToCreateInto.Exists())
            {
                _databaseToCreateInto.Server.CreateDatabase(_databaseToCreateInto.GetRuntimeName());
            }

            //Create with template?
            if(Template != null)
            {
                foreach (ImageTableTemplate table in Template.Tables)
                {
                    string tblName = GetNameWithPrefix(table.TableName);

                    var tbl = _databaseToCreateInto.ExpectTable(tblName);
                    var cmd = new ExecuteCommandCreateNewImagingDataset(_repositoryLocator, tbl,table);
                    cmd.Execute();

                    NewCataloguesCreated.Add(cmd.NewCatalogueCreated);
                    tablesCreated.Add(tbl);
                }
            }
            else
                throw new("No Template provided");

            //that's us done if we aren't creating a load
            if(!CreateLoad)
                return;

            string loadName = GetNameWithPrefixInBracketsIfAny("SMI Image Loading");

            NewLoadMetadata = new(_catalogueRepository, loadName);

            //tell all the catalogues that they are part of this load and where to log under the same task
            foreach (var c in NewCataloguesCreated)
            {
                c.LoadMetadata_ID = NewLoadMetadata.ID;
                c.LoggingDataTask = loadName;
                c.LiveLoggingServer_ID = _loggingServer.ID;
                c.SaveToDatabase();
            }

            //create the logging task
            new Core.Logging.LogManager(_loggingServer).CreateNewLoggingTaskIfNotExists(loadName);

            var projDir = LoadDirectory.CreateDirectoryStructure(_projectDirectory, "ImageLoading",true);
            NewLoadMetadata.LocationOfFlatFiles = projDir.RootPath.FullName;
            NewLoadMetadata.SaveToDatabase();

            /////////////////////////////////////////////Attacher////////////////////////////

            
            //Create a pipeline for reading from Dicom files and writing to any destination component (which must be fixed)
            var pipe = new Pipeline(_catalogueRepository, GetNameWithPrefixInBracketsIfAny("Image Loading Pipe"));
            DicomSourcePipelineComponent = new(_catalogueRepository, pipe, DicomSourceType, 0, DicomSourceType.Name);
            DicomSourcePipelineComponent.CreateArgumentsForClassIfNotExists(DicomSourceType);

            // Set the argument for only populating tags who appear in the end tables of the load (no need for source to read all the tags only those we are actually loading)
            var arg = DicomSourcePipelineComponent.GetAllArguments().FirstOrDefault(a=>a.Name.Equals(nameof(DicomSource.UseAllTableInfoInLoadAsFieldMap)));
            if(arg != null)
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

            var args = PersistentRaw? pt.CreateArgumentsForClassIfNotExists<AutoRoutingAttacherWithPersistentRaw>() : pt.CreateArgumentsForClassIfNotExists<AutoRoutingAttacher>();
            SetArgument(args, "LoadPipeline", pipe);
            
            /////////////////////////////////////// Distinct tables on load /////////////////////////

            
            var distincter = new ProcessTask(_catalogueRepository,NewLoadMetadata,LoadStage.AdjustRaw);
            var distincterArgs = distincter.CreateArgumentsForClassIfNotExists<Distincter>();

            distincter.Name = "Distincter";
            distincter.ProcessTaskType = ProcessTaskType.MutilateDataTable;
            distincter.Path = typeof(Distincter).FullName;
            distincter.Order = 2;
            distincter.SaveToDatabase();
            SetArgument(distincterArgs, "TableRegexPattern", ".*");

            /////////////////////////////////////////////////////////////////////////////////////

            if(CreateCoalescer)
            {
                var coalescer = new ProcessTask(_catalogueRepository, NewLoadMetadata, LoadStage.AdjustRaw)
                {
                    Name = "Coalescer",
                    ProcessTaskType = ProcessTaskType.MutilateDataTable,
                    Path = typeof(Coalescer).FullName,
                    Order = 3
                };
                coalescer.SaveToDatabase();

                StringBuilder regexPattern = new();

                foreach (var tbl in tablesCreated.Where(tbl => !tbl.DiscoverColumns().Any(c=>c.GetRuntimeName().Equals("SOPInstanceUID",StringComparison.CurrentCultureIgnoreCase))))
                    regexPattern.Append($"({tbl.GetRuntimeName()})|");
                

                var coalArgs = coalescer.CreateArgumentsForClassIfNotExists<Coalescer>();
                SetArgument(coalArgs, "TableRegexPattern", regexPattern.ToString().TrimEnd('|'));
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
            
            var checker = new CheckEntireDataLoadProcess(NewLoadMetadata, new(NewLoadMetadata), new(), _catalogueRepository.MEF);
            checker.Check(new AcceptAllCheckNotifier());
        }
        
        private string GetNameWithPrefixInBracketsIfAny(string name)
        {
            return string.IsNullOrWhiteSpace(TablePrefix) ? name : $"{name}({TablePrefix.Trim('_')})";
        }
        private string GetNameWithPrefix(string name)
        {
            if (string.IsNullOrWhiteSpace(TablePrefix))
                return name;

            return TablePrefix + name;
        }

        private void SetArgument(IArgument[] args, string property, object value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            var arg = args.Single(a => a.Name.Equals(property));

            var mef = ((CatalogueRepository) arg.Repository).MEF;
            if (mef.GetType(value.GetType().FullName) == null)
                throw new ArgumentException($"No type found for { value.GetType().FullName }");

            //if this fails, look to see if GetType returned null (indicates that your Type is not loaded by MEF).  Look at mef.DescribeBadAssembliesIfAny() to investigate this issue
            arg.SetValue(value);
            arg.SaveToDatabase();
        }
    }
}
