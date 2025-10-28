using System.Collections.Immutable;
using System.Xml.Linq;
using PnpExample.Constants;
using ControlBee.Interfaces;
using ControlBee.Models;
using ControlBee.Sequences;
using ControlBee.Utils;
using ControlBeeAbstract.Exceptions;
using log4net;
using Newtonsoft.Json;
using Message = ControlBee.Models.Message;

namespace PnpExample.Actors.Syncer;

public class IdleState(SyncerActor actor) : State<SyncerActor>(actor)
{
    private static readonly ILog Logger = LogManager.GetLogger("Sequence");
    private Guid _masterModeCycleId = Guid.Empty;
    private string _masterModeType = "";

    public override void Dispose()
    {
        Actor.SetStatus("_ready", false);
    }

    public override bool ProcessMessage(Message message)
    {
        switch (message.Name)
        {
            case StateEntryMessage.MessageName:
                Actor.Send(new Message(Actor, "IdleEvent"));
                Actor.EventManager.Write(Actor.Name, "Machine stopped.", code:100);
                UpdateReady();
                Actor.ChangeIndicatorLights(SyncerActor.MachineStateEnum.Idle);
                return true;
            case TimerMessage.MessageName:
                if (Actor.GetStatus("AllInitialized") is true && Actor.StartSwitchDet.IsOn())
                    message.Sender.Send(new Message(Actor, "_start"));
                return true;
            case "_status":
                {
                    var error = message.Sender != Actor && message.DictPayload!.GetValueOrDefault("_error") is true;
                    if (error)
                        throw new SequenceError();
                    UpdateReady();
                    break;
                }
            case "_initialize":
                {
                    var initializingActors = ((IEnumerable<IActor>)message.Payload!).ToImmutableList();
                    if (initializingActors.Count == 0)
                    {
                        Logger.Info("No actor has been checked for initialization.");
                        return true;
                    }

                    Logger.Info("Initializing actors...");
                    Actor.Initializing.Show();

                    var globalInitializeSequence = new GlobalInitializeSequence(
                        Actor,
                        sequence =>
                        {
                            sequence.InitializeIfPossible(actor.Peers.Picker0);
                            sequence.InitializeIfPossible(actor.Peers.Picker1);
                            if (sequence.IsInitializingActors)
                                return;

                            sequence.InitializeIfPossible(actor.Peers.Head);
                            if (sequence.IsInitializingActors)
                                return;

                            sequence.InitializeIfPossible(actor.Peers.SourceStage0);
                            sequence.InitializeIfPossible(actor.Peers.TargetStage);

                        },
                        initializingActors
                    );
                    Actor.SetGlobalInitializeSequence(globalInitializeSequence);

                    globalInitializeSequence.Run();
                    Actor.State = new InitializingState(Actor, globalInitializeSequence);
                    return true;
                }
            case "_start":
                if (Actor.GetStatus("_ready") is not true)
                {
                    Logger.Warn("Syncer is not ready to start.");
                    return true;
                }

                var cycleMode = DictPath.Start(message.DictPayload)["_cycle"].Value is true;
                var stepMode = DictPath.Start(message.DictPayload)["_step"].Value is true;
                Actor.State = new AutoState(actor, cycleMode, stepMode);
                return true;
        }

        if (Actor.GetFunctions().Contains(message.Name))
        {
            Actor.State = new ProcessingCycleState(actor, message.Name, message.Payload);
            return true;
        }

        return false;
    }

    private void UpdateReady()
    {
        var ready = true;
        foreach (var peer in Actor.InternalPeers.ToArray())
        {
            if (Actor.GetPeerStatus(peer, "_ready") is true)
                continue;
            ready = false;
            break;
        }

        Actor.SetStatus("_ready", ready);
    }
}