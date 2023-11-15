using Rdmp.Dicom.Attachers.Routing;

namespace Rdmp.Dicom.PipelineComponents.DicomSources.Worklists;

/// <summary>
/// Shared worklist interface for anything that can be turned into dicom datasets.  This is used to define the context and compatibility of sources
/// for the dicom load pipeline <see cref="AutoRoutingAttacher"/> 
/// </summary>
public interface IDicomWorklist
{
}