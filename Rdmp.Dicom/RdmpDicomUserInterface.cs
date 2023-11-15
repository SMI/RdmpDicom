using Rdmp.Core;
using Rdmp.Core.CommandExecution;
using Rdmp.Core.Curation.Data.Aggregation;
using Rdmp.Core.MapsDirectlyToDatabaseTable;
using Rdmp.Dicom.ExternalApis;
using Terminal.Gui;

namespace Rdmp.Dicom;

public class RdmpDicomConsoleUserInterface : PluginUserInterface
{
    private readonly IBasicActivateItems _activator;

    public RdmpDicomConsoleUserInterface(IBasicActivateItems itemActivator) : base(itemActivator)
    {
        _activator = itemActivator;
    }

    public override bool CustomActivate(IMapsDirectlyToDatabaseTable o)
    {
        // if it's not a terminal gui don't run a terminal gui UI!
        if(_activator?.GetType().Name.Equals("ConsoleGuiActivator")!=true)
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