using ControlBee.Constants;
using ControlBee.Interfaces;
using ControlBee.Models;
using ControlBee.Sequences;
using ControlBeeAbstract.Exceptions;
using log4net;
using Message = ControlBee.Models.Message;

namespace PnpExample.Actors.Syncer;

public class InitializingState(SyncerActor actor, GlobalInitializeSequence globalInitializeSequence)
    : State<SyncerActor>(actor)
{
    private static readonly ILog Logger = LogManager.GetLogger("Sequence");

    private readonly Dictionary<IActor, bool> _initializedActors = new();

    public override bool ProcessMessage(Message message)
    {
        switch (message.Name)
        {
            case "_initialized":
            {
                globalInitializeSequence.SetInitializationState(
                    message.Sender,
                    InitializationStatus.Initialized
                );
                Logger.Info($"Finished initializing {message.Sender.Name}.");
                if (globalInitializeSequence.IsComplete)
                {
                    Logger.Info("Finished initializing all actors.");
                    Actor.Initializing.Close();
                    Actor.InitializationDone.Show();
                    Actor.State = Actor.CreateIdleState();
                    return true;
                }

                if (!globalInitializeSequence.IsInitializingActors)
                    globalInitializeSequence.Run();
                return true;
            }
            case "_status":
                foreach (var peer in actor.InternalPeers.ToArray())
                    if (Actor.HasPeerError(peer))
                        throw new SequenceError();
                return true;
        }

        return false;
    }
}