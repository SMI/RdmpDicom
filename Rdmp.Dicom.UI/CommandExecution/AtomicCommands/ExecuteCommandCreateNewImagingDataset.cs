using System.Drawing;
using Rdmp.UI.CommandExecution.AtomicCommands;
using Rdmp.UI.Icons.IconOverlays;
using Rdmp.UI.Icons.IconProvision;
using Rdmp.UI.ItemActivation;
using ReusableLibraryCode.CommandExecution.AtomicCommands;
using ReusableLibraryCode.Icons.IconProvision;

namespace Rdmp.Dicom.UI.CommandExecution.AtomicCommands
{
    public class ExecuteCommandCreateNewImagingDataset : BasicUICommandExecution, IAtomicCommand
    {
        public ExecuteCommandCreateNewImagingDataset(IActivateItems activator) : base(activator)
        {
            
        }

        public override string GetCommandName()
        {
            return "Create New Imaging Table";
        }

        public override Image GetImage(IIconProvider iconProvider)
        {
            return iconProvider.GetImage(RDMPConcept.TableInfo,OverlayKind.Add);
        }

        public override void Execute()
        {
            new CreateNewImagingDatasetUI(Activator).ShowDialog();
        }
    }
}