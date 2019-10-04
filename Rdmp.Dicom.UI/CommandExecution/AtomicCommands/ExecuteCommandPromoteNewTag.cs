using System.Drawing;
using System.Windows.Forms;
using ReusableLibraryCode.Icons.IconProvision;
using Rdmp.Dicom.TagPromotionSchema;
using Rdmp.UI.CommandExecution.AtomicCommands;
using Rdmp.Core.CommandExecution.AtomicCommands;
using Rdmp.UI.ItemActivation;
using Rdmp.Core.Curation.Data;
using Rdmp.UI.ChecksUI;
using Rdmp.UI.Icons.IconProvision;

namespace Rdmp.Dicom.UI.CommandExecution.AtomicCommands
{
    public class ExecuteCommandPromoteNewTag:BasicUICommandExecution,IAtomicCommandWithTarget
    {
        private readonly bool _includeLoadedField;
        private TableInfo _tableInfo;

        public ExecuteCommandPromoteNewTag(IActivateItems activator, bool includeLoadedField = false) : base(activator)
        {
            _includeLoadedField = includeLoadedField;
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
                var popup = new PopupChecksUI("Adding Column", false);
                var columnAdder = new TagColumnAdder(ui.ColumnName, ui.ColumnDataType, _tableInfo, popup,_includeLoadedField);

                columnAdder.Execute();
                Publish(_tableInfo);
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
