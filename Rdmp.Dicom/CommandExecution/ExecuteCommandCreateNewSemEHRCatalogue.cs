using Rdmp.Core.CommandExecution;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Icons.IconProvision;
using Rdmp.Dicom.ExternalApis;
using ReusableLibraryCode.Icons.IconProvision;
using System.Drawing;

namespace Rdmp.Dicom.CommandExecution
{
    public class ExecuteCommandCreateNewSemEHRCatalogue : BasicCommandExecution
    {
        public ExecuteCommandCreateNewSemEHRCatalogue(IBasicActivateItems activator): base(activator)
        {

        }

        public override Image GetImage(IIconProvider iconProvider)
        {
            return iconProvider.GetImage(RDMPConcept.Catalogue,OverlayKind.Cloud);
        }
        public override void Execute()
        {
            base.Execute();

            // Create a new Catalogue named correctly to be identified as an API of the correct type
            var c = new Catalogue(BasicActivator.RepositoryLocator.CatalogueRepository, SemEHRApiCaller.SemEHRApiPrefix);

            Publish(c);
            Emphasise(c);
        }
    }
}
