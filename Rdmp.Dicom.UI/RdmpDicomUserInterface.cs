using System.Drawing;
using System.Windows.Forms;
using ReusableLibraryCode.Icons.IconProvision;
using Rdmp.Dicom.UI.CommandExecution.AtomicCommands;
using Rdmp.UI.ItemActivation;
using Rdmp.UI.Refreshing;
using Rdmp.UI.PluginChildProvision;
using Rdmp.Core.Curation.Data;
using Rdmp.UI.Collections;
using Rdmp.UI.CommandExecution.AtomicCommands;
using Rdmp.Core.Providers.Nodes;
using Rdmp.Core.Curation.Data.Defaults;

namespace Rdmp.Dicom.UI
{
    public class RdmpDicomUserInterface : PluginUserInterface, IRefreshBusSubscriber
    {
        
        public RdmpDicomUserInterface(IActivateItems itemActivator) : base(itemActivator)
        {
            ItemActivator.RefreshBus.Subscribe(this);
        }
        
        public override ToolStripMenuItem[] GetAdditionalRightClickMenuItems(object o)
        {
            //IMPORTANT: if you are creating a menu array for a class in your own plugin instead create it as a Menu (See TagPromotionConfigurationMenu)

            var databaseEntity = o as DatabaseEntity;

            //allow clicking in Catalogue collection whitespace
            if (o is RDMPCollection && ((RDMPCollection)o) == RDMPCollection.Catalogue)
            {
                return GetMenuArray(new ExecuteCommandCreateNewImagingDataset(ItemActivator));
            }

            if (databaseEntity is Catalogue)
                return GetMenuArray(
                    new ExecuteCommandCreateNewImagingDataset(ItemActivator),
                    new ExecuteCommandPromoteNewTag(ItemActivator).SetTarget(databaseEntity));
            
            if (databaseEntity is TableInfo)
                return GetMenuArray(new ExecuteCommandPromoteNewTag(ItemActivator).SetTarget(databaseEntity));

            if (o is AllExternalServersNode)
                return GetMenuArray(new ExecuteCommandCreateNewExternalDatabaseServer(ItemActivator,new SMIDatabasePatcher(),PermissableDefaults.None));

            return null;
        }

        public override object[] GetChildren(object model)
        {
            return null;
        }

        public override Bitmap GetImage(object concept, OverlayKind kind = OverlayKind.None)
        {
            return null;
        }

        public void RefreshBus_RefreshObject(object sender, RefreshObjectEventArgs e)
        {
            
        }
    }
}
