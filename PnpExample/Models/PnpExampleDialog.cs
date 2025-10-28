using ControlBee.Interfaces;
using ControlBee.Models;
using ControlBee.Services;

namespace PnpExample.Models;

public class PnpExampleDialog(DialogContextFactory dialogContextFactory, IEventManager eventManager) : Dialog(dialogContextFactory, eventManager)
{
    public override void InjectProperties(ISystemPropertiesDataSource dataSource)
    {
        base.InjectProperties(dataSource);
        if (
            dataSource.GetValue(ActorName, ItemPath, nameof(PnpExampleDialogContext.Image)) is string image
        )
            ((PnpExampleDialogContext)Context).Image = image;
    }
}
