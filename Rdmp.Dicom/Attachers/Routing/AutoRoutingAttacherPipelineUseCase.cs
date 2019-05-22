using System.Data;
using Rdmp.Core.Curation.Data.Pipelines;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.DataFlowPipeline.Requirements;
using Rdmp.Dicom.PipelineComponents.DicomSources.Worklists;

namespace Rdmp.Dicom.Attachers.Routing
{
    /// <summary>
    /// Defines the input objects / and explicit destination for the pipeline which the user must create for use with an <see cref="AutoRoutingAttacher"/>.
    /// </summary>
    public sealed class AutoRoutingAttacherPipelineUseCase:PipelineUseCase
    {
        public AutoRoutingAttacherPipelineUseCase(AutoRoutingAttacher attacher, IDicomWorklist worklist)
        {
            ExplicitDestination = attacher;

            AddInitializationObject(worklist);

            GenerateContext();
        }

        protected override IDataFlowPipelineContext GenerateContextImpl()
        {
            var context = new DataFlowPipelineContextFactory<DataTable>().Create(PipelineUsage.FixedDestination);
            context.MustHaveSource = typeof(IDataFlowSource<DataTable>);

            return context;
        }

        private AutoRoutingAttacherPipelineUseCase(AutoRoutingAttacher attacher)
            : base(new[] { typeof(IDicomWorklist), typeof(IDicomDatasetWorklist), typeof(IDicomFileWorklist) })
        {
            ExplicitDestination = attacher;
            GenerateContext();
        }

        public static AutoRoutingAttacherPipelineUseCase GetDesignTimeUseCase(AutoRoutingAttacher attacher)
        {
            return new AutoRoutingAttacherPipelineUseCase(attacher);
        }
    }
}
