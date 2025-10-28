using ControlBee.Models;
using log4net;
using Message = ControlBee.Models.Message;

namespace PnpExample.Actors.Empty;

public class IdleState(EmptyActor actor) : State<EmptyActor>(actor)
{
    private static readonly ILog Logger = LogManager.GetLogger(nameof(IdleState));

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
                if (Actor.GetPeerStatus(Actor.Syncer, "_auto") is true)
                {
                    Actor.State = new AutoState(Actor);
                    return true;
                }

                break;
        }

        return false;
    }
}