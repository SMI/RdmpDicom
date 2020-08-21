using System.Drawing;
using System.Windows.Forms;
using ReusableLibraryCode.Icons.IconProvision;
using Rdmp.Dicom.TagPromotionSchema;
using Rdmp.UI.CommandExecution.AtomicCommands;
using Rdmp.Core.CommandExecution.AtomicCommands;
using Rdmp.UI.ItemActivation;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Icons.IconProvision;
using Rdmp.UI.ChecksUI;

namespace Rdmp.Dicom.UI.CommandExecution.AtomicCommands
{
    public class ExecuteCommandPromoteNewTag:BasicUICommandExecution,IAtomicCommandWithTarget
    {
        private TableInfo _tableInfo;

        public ExecuteCommandPromoteNewTag(IActivateItems activator) : base(activator)
        {
        }

        public override Image GetImage(IIconProvider iconProvider)
        {
            return iconProvider.GetImage(RDMPConcept.ColumnInfo,OverlayKind.Add);
        }

        public override void Execute()
        {
            if(_tableInfo == null)
                SetImpossible("No TableInfo has been set, use SetTarget(TableInfo or Catalogue)");

            base.Execute();

            var ui = new TagColumnAdderUI(_tableInfo);

            if(ui.ShowDialog() == DialogResult.OK)
            {
                var checks = new PopupChecksUI("Adding Column", false);
                
                var columnAdder = new TagColumnAdder(ui.ColumnName, ui.ColumnDataType, _tableInfo, checks);

                columnAdder.Execute();
                Publish(_tableInfo);

                //Checks have likely been popped up as a non modal dialogue by the execute action (allowing the user to review the events)
                if(checks.Visible)
                    checks.FormClosed += (s,e)=>
                    {
                        //schedule disposal of the control for when the user closes it
                        if(!checks.IsDisposed)
                            checks.Dispose();
                    };                        
                else
                    checks.Dispose(); // for some reason the checks were not spawned so explicitly dispose of the resources
            }
        }

        public IAtomicCommandWithTarget SetTarget(DatabaseEntity target)
        {
            _tableInfo = target as TableInfo;

            var catalogue = target as Catalogue;

            if(catalogue != null)
            {
                var tables = catalogue.GetTableInfoList(false);

                if (tables.Length == 1)
                    _tableInfo = (TableInfo)tables[0];
            }

            return this;
        }
    }
}
