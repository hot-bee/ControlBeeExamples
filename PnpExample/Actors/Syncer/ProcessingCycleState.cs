using ControlBee.Interfaces;
using ControlBee.Models;
using ControlBeeAbstract.Exceptions;
using Message = ControlBee.Models.Message;

namespace PnpExample.Actors.Syncer;

public class ProcessingCycleState(SyncerActor actor, string cycleName, object? payload) : State<SyncerActor>(actor)
{
    private int _cycleDoneCount;
    private IActor[] _workingActors = [];

    public override bool ProcessMessage(Message message)
    {
        switch (message.Name)
        {
            case StateEntryMessage.MessageName:
                switch (cycleName)
                {
                    case "Pickup":
                        Actor.Peers.Head.Send(new Message(Actor, "Pickup", Actor.ActiveSourceStageIndex.Value));
                        Actor.Peers.SourceStage0.Send(new Message(Actor, "Transfer"));
                        _workingActors = [Actor.Peers.Head, Actor.Peers.SourceStage0];
                        return true;
                    case "Place":
                        Actor.Peers.Head.Send(new Message(Actor, "Place"));
                        Actor.Peers.TargetStage.Send(new Message(Actor, "Transfer"));
                        _workingActors = [Actor.Peers.Head, Actor.Peers.TargetStage];
                        return true;
                    default:
                        throw new ValueError();
                }
            case "CycleDone":
                if (_workingActors.Contains(message.Sender))
                {
                    _cycleDoneCount++;
                    if (_cycleDoneCount == _workingActors.Length)
                        Actor.State = new IdleState(Actor);
                    return true;
                }

                break;
            case "_status":
                if (_workingActors.Any(workingActor => Actor.HasPeerFailed(workingActor)))
                    throw new SequenceError();
                break;
        }

        return false;
    }
}