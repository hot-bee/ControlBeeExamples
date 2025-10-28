using ControlBee.Models;
using Message = ControlBee.Models.Message;

namespace PnpExample.Actors.Syncer;

public class ErrorState(SyncerActor actor) : State<SyncerActor>(actor)
{
    public override bool ProcessMessage(Message message)
    {
        switch (message.Name)
        {
            case StateEntryMessage.MessageName:
                Actor.SetStatus("_error", true);
                Actor.ChangeIndicatorLights(SyncerActor.MachineStateEnum.Error);
                return true;
            case TimerMessage.MessageName:
                if (Actor.ResetSwitchDet.IsOn())
                    message.Sender.Send(new Message(Actor, "_resetError"));
                return true;
            case "_resetError":
                foreach (var peer in Actor.InternalPeers.ToArray())
                    if (Actor.HasPeerError(peer) ||
                        Actor.GetPeerStatus(peer, "_ready") is true ||
                        Actor.GetPeerStatus(peer, "_inactive") is true)
                    {
                    }
                    else
                    {
                        return false;
                    }

                foreach (var peer in Actor.InternalPeers.ToArray())
                    peer.Send(new Message(Actor, "_resetError"));
                return true;
            case "_status":
                if (Actor
                    .InternalPeers.ToArray().Any(peer => Actor.HasPeerError(peer)))
                    return false;
                Actor.State = Actor.CreateIdleState();
                return true;
        }

        return false;
    }

    public override void Dispose()
    {
        Actor.SetStatus("_error", false);
    }
}