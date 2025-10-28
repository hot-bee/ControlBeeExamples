using ControlBee.Models;
using log4net;
using Message = ControlBee.Models.Message;

namespace PnpExample.Actors.Empty;

public class InactiveState(EmptyActor actor) : State<EmptyActor>(actor)
{
    private static readonly ILog Logger = LogManager.GetLogger("Sequence");

    public override void Dispose()
    {
        base.Dispose();
        Actor.SetStatus("_inactive", false);
    }

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

                message.Sender.Send(new Message(Actor, "_initialized"));
                Actor.State = new IdleState(Actor);
                Logger.Info("Finished initialization.");
                return true;
            }
        }

        return false;
    }
}