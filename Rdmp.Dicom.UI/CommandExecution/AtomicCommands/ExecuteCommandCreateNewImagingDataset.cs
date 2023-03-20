using Rdmp.Core.Icons.IconProvision;
using Rdmp.UI.CommandExecution.AtomicCommands;
using Rdmp.UI.ItemActivation;
using Rdmp.Core.ReusableLibraryCode.Icons.IconProvision;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;

namespace Rdmp.Dicom.UI.CommandExecution.AtomicCommands;

public class ExecuteCommandCreateNewImagingDataset : BasicUICommandExecution
{
    public ExecuteCommandCreateNewImagingDataset(IActivateItems activator) : base(activator)
    {
            
    }

    public override string GetCommandName()
    {
        return "Create New Imaging Table";
    }

    public override Image<Rgba32> GetImage(IIconProvider iconProvider)
    {
        return iconProvider.GetImage(RDMPConcept.TableInfo,OverlayKind.Add);
    }

    public override void Execute()
    {
        new CreateNewImagingDatasetUI(Activator).ShowDialog();
    }
}