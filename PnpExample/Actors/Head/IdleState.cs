using ControlBee.Models;
using log4net;
using Message = ControlBee.Models.Message;

namespace PnpExample.Actors.Head;

public class IdleState(HeadActor actor) : State<HeadActor>(actor)
{
    private static readonly ILog Logger = LogManager.GetLogger("Sequence");

    public override void Dispose()
    {
        Actor.SetStatus("_ready", false);
    }

    public override bool ProcessMessage(Message message)
    {
        switch (message.Name)
        {
            case StateEntryMessage.MessageName:
                Actor.SetStatus("_ready", true);
                return true;
            case "_inactivate":
                Actor.State = new InactiveState(Actor);
                return true;
            case "_status":
            case "_scan":
                if (Actor.GetPeerStatus(actor.Syncer, "_auto") is true)
                {
                    Actor.State = new AutoState(Actor);
                    return true;
                }
                Scan();
                return true;
            case "Pickup":
                Actor.State = new PickingState(Actor, (int)message.Payload!);
                return true;
            case "Place":
                Actor.State = new PlacingState(Actor);
                return true;
        }

        return false;
    }

    private void Scan()
    {
        // Empty
    }
}