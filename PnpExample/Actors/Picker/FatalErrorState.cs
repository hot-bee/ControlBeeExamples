using ControlBee.Models;
using Message = ControlBee.Models.Message;

namespace PnpExample.Actors.Picker;

public class FatalErrorState(PickerActor actor) : State<PickerActor>(actor)
{
    public override bool ProcessMessage(Message message)
    {
        switch (message.Name)
        {
            case StateEntryMessage.MessageName:
                Actor.SetStatus("_error", true);
                Actor.SetStatus("_fatal", true);
                return true;
            case "_resetError":
                Actor.State = new InactiveState(Actor);
                return true;
        }

        return false;
    }

    public override void Dispose()
    {
        Actor.SetStatus("_error", false);
        Actor.SetStatus("_fatal", false);
    }
}