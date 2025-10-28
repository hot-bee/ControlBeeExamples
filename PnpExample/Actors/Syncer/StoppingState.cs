using ControlBee.Models;
using ControlBeeAbstract.Exceptions;
using log4net;
using Message = ControlBee.Models.Message;

namespace PnpExample.Actors.Syncer;

public class StoppingState(SyncerActor actor) : State<SyncerActor>(actor)
{
    private static readonly ILog Logger = LogManager.GetLogger("Sequence");

    public override void Dispose()
    {
        Actor.SetStatus("_stopping", false);
    }

    public override bool ProcessMessage(Message message)
    {
        switch (message.Name)
        {
            case StateEntryMessage.MessageName:
                Logger.Info("Stopping auto run.");
                Actor.SetStatus("_stopping", true);
                return true;
            case "_status":
            {
                foreach (var peer in Actor.InternalPeers.ToArray())
                    if (Actor.HasPeerError(peer))
                        throw new SequenceError();

                foreach (var peer in Actor.InternalPeers.ToArray())
                {
                    if (Actor.GetPeerStatus(peer, "_ready") is not true)
                        return false;
                }
                Logger.Info("Stopped auto run.");
                Actor.State = Actor.CreateIdleState();
                return true;
            }
        }

        return false;
    }
}
