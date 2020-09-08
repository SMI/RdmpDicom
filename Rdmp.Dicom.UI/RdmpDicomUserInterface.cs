using System.Drawing;
using System.Windows.Forms;
using ReusableLibraryCode.Icons.IconProvision;
using Rdmp.Dicom.UI.CommandExecution.AtomicCommands;
using Rdmp.UI.ItemActivation;
using Rdmp.UI.Refreshing;
using Rdmp.UI.PluginChildProvision;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.DataLoad;
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
            if (o is RDMPCollection collection && collection == RDMPCollection.Catalogue)
            {
                return GetMenuArray(new ExecuteCommandCreateNewImagingDataset(ItemActivator));
            }

            switch (databaseEntity)
            {
                case Catalogue c:
                    return GetMenuArray(
                        new ExecuteCommandCreateNewImagingDataset(ItemActivator),
                        new ExecuteCommandPromoteNewTag(ItemActivator).SetTarget(databaseEntity),
                        new ExecuteCommandCompareImagingSchemas(ItemActivator,c));
                case ProcessTask pt:
                    return GetMenuArray(new ExecuteCommandReviewIsolations(ItemActivator, pt));
                case TableInfo _:
                    return GetMenuArray(new ExecuteCommandPromoteNewTag(ItemActivator).SetTarget(databaseEntity));
            }

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
