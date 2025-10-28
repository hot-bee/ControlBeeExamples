using ControlBee.Models;
using log4net;
using Message = ControlBee.Models.Message;

namespace PnpExample.Actors.Empty;

public class AutoState(EmptyActor actor) : State<EmptyActor>(actor)
{
    private static readonly ILog Logger = LogManager.GetLogger("Sequence");

    public override void Dispose()
    {
        base.Dispose();
        Actor.SetStatus("_auto", false);
    }

    public override bool ProcessMessage(Message message)
    {
        switch (message.Name)
        {
            case StateEntryMessage.MessageName:
                Actor.SetStatus("_auto", true);
                Scan();
                return true;
            case "_status":
                return Scan();
        }

        return false;
    }

    public bool Scan()
    {
        if (Actor.GetPeerStatus(Actor.Parent, "_auto") is not true)
        {
            Actor.State = new IdleState(Actor);
            return true;
        }

        return false;
    }
}