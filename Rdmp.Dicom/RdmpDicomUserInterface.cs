using Rdmp.Core;
using Rdmp.Core.Curation.Data.Aggregation;
using Rdmp.Dicom.ExternalApis;
using Rdmp.Core.CommandExecution;
using Rdmp.Core.MapsDirectlyToDatabaseTable;
using Terminal.Gui;

namespace Rdmp.Dicom.UI;

public class RdmpDicomConsoleUserInterface : PluginUserInterface
{
    readonly IBasicActivateItems _activator;

    public RdmpDicomConsoleUserInterface(IBasicActivateItems itemActivator) : base(itemActivator)
    {
        _activator = itemActivator;
    }

    public override bool CustomActivate(IMapsDirectlyToDatabaseTable o)
    {
        // if its not a terminal gui don't run a terminal gui UI!
        if(_activator == null || !_activator.GetType().Name.Equals("ConsoleGuiActivator"))
        {
            return false;
        }

        if (o is not AggregateConfiguration ac) return base.CustomActivate(o);
        var api = new SemEHRApiCaller();

        if (!api.ShouldRun(ac)) return base.CustomActivate(o);
        var ui = new SemEHRConsoleUI(_activator, api, ac);
        Application.Run(ui);
        return true;

    }
}