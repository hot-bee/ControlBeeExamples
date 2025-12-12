using ControlBee.Models;
using log4net;
using Message = ControlBee.Models.Message;

namespace PnpExample.Actors.Head;

public class InactiveState(HeadActor actor) : State<HeadActor>(actor)
{
    private static readonly ILog Logger = LogManager.GetLogger("Sequence");

    public override bool ProcessMessage(Message message)
    {
        switch (message.Name)
        {
            case StateEntryMessage.MessageName:
                Actor.SetStatus("_inactive", true);
                return true;
            case "_initialize":
            {
                Logger.Info("Start initialization.");
                Actor.SafetyCheck();

                Actor.Y.Initialize();

                message.Sender.Send(new Message(Actor, "_initialized"));
                Actor.SetStatus("_inactive", false);
                Actor.State = new IdleState(Actor);
                Logger.Info("Finished initialization.");
                return true;
            }
        }

        return false;
    }
}
