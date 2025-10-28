using ControlBee.Interfaces;
using ControlBee.Models;
using ControlBee.Utils;
using ControlBeeAbstract.Exceptions;
using log4net;
using PnpExample.Constants;
using Dict = System.Collections.Generic.Dictionary<string, object?>;
using Message = ControlBee.Models.Message;

namespace PnpExample.Actors.Syncer;

public class AutoState(SyncerActor actor, bool cycleMode, bool stepMode) : State<SyncerActor>(actor)
{
    private static readonly ILog Logger = LogManager.GetLogger("Sequence");

    private readonly Dict _grants = [];

    public override void Dispose()
    {
        Actor.SetStatus("_auto", false);
        Actor.SetStatus("_cycle", false);
        Actor.SetStatus("_step", false);
    }

    public override bool ProcessMessage(Message message)
    {
        switch (message.Name)
        {
            case StateEntryMessage.MessageName:
            {
                Logger.Info($"Start Auto run. (_cycle: {cycleMode}), (_step: {stepMode})");
                using var statusGroup = new StatusGroup(Actor);
                Actor.Send(new Message(Actor, "AutoEvent"));
                Actor.EventManager.Write(Actor.Name, "Machine started.", code: 200);
                Actor.SetStatus("_auto", true);
                Actor.SetStatus("_cycle", cycleMode);
                Actor.SetStatus("_step", stepMode);
                Actor.ChangeIndicatorLights(SyncerActor.MachineStateEnum.Auto);
                Scan();
                return true;
            }
            case TimerMessage.MessageName:
                if (Actor.StopSwitchDet.IsOn())
                    Actor.State = new StoppingState(Actor);
                return true;
            case "_stop":
                Actor.State = new StoppingState(Actor);
                return true;
            case "_status":
                var error = message.DictPayload!.GetValueOrDefault("_error") is true;
                if (error)
                    throw new SequenceError();
                Scan();
                return true;
        }

        return false;
    }

    private readonly Stopwatch _stopwatch = new();
    private void Scan()
    {
        HeadScan();
    }
    private void HeadScan()
    {
        if (SyncUtils.SyncRequestsCheck(
                Actor,
                _grants,
                [
                    new RequestSource(actor.Peers.Head, "ReadyToPurge")
                ],
                "Purge"
            )
           )
        {
            var readyToPurgeIndex = (int)actor.GetPeerStatusByActor(actor.Peers.Head, "ReadyToPurgeIndex")!;
            actor.Peers.Head.Send(new Message(Actor, "Purge", readyToPurgeIndex));
        }

        if (SyncUtils.SyncRequestsCheck(
                Actor,
                _grants,
                [
                    new RequestSource(actor.Peers.Head, "ReadyToPutBack"),
                    new RequestSource(actor.Peers.SourceStage0, "ReadyToTransfer")
                ],
                "PutBack"
            )
           )
        {
            actor.Peers.Head.Send(new Message(Actor, "PutBack", actor.ActiveSourceStageIndex.Value));
            actor.Peers.SourceStage0.Send(new Message(Actor, "PutBack"));
        }

        var occupiedPickerCount = (int)Actor.GetPeerStatus(actor.Peers.Head, "OccupiedPickerCount")!;
        var vacantPickerCount = (int)Actor.GetPeerStatus(actor.Peers.Head, "VacantPickerCount")!;
        if (actor.HeadPickingPhase)
        {
            var notWorkedCellCountInActive =
                (int?)Actor.GetPeerStatus(actor.Peers.SourceStage0, "NotWorkedCellCount");
            var phaseSwitchCondition = vacantPickerCount == 0;
            phaseSwitchCondition |= occupiedPickerCount > 0 && notWorkedCellCountInActive == 0;
            if (phaseSwitchCondition)
            {
                Logger.Info("Switch to placing phase.");
                actor.HeadPickingPhase = !actor.HeadPickingPhase;
                Actor.Send(new Message(Actor, "_scan"));
                return;
            }

            List<RequestSource> pickupRequests =
            [
                new(actor.Peers.Head, "ReadyToPickup")
            ];
            if (actor.SourceStageType != StageType.Station)
                pickupRequests.Add(new RequestSource(actor.Peers.SourceStage0, "ReadyToTransfer"));
            if (SyncUtils.SyncRequestsCheck(
                    Actor,
                    _grants,
                    pickupRequests.ToArray(),
                    "Pickup"
                )
               )
            {
                actor.Peers.SourceStage0.Send(new Message(Actor, "Transfer"));
                actor.Peers.Head.Send(new Message(Actor, "Pickup", actor.ActiveSourceStageIndex.Value));
            }
        }
        else
        {
            if (occupiedPickerCount == 0)
            {
                Actor.IndexTime.Value = (int)_stopwatch.ElapsedMilliseconds;
                _stopwatch.Restart();

                Logger.Info("Switch to picking phase.");
                actor.HeadPickingPhase = !actor.HeadPickingPhase;
                Actor.Send(new Message(Actor, "_scan"));
                return;
            }

            if (SyncUtils.SyncRequestsCheck(
                    Actor,
                    _grants,
                    [
                        new RequestSource(actor.Peers.TargetStage, "ReadyToTransfer"),
                        new RequestSource(actor.Peers.Head, "ReadyToPlace")
                    ],
                    "Place"
                )
               )
            {
                actor.Peers.TargetStage.Send(new Message(Actor, "Transfer"));
                actor.Peers.Head.Send(new Message(Actor, "Place"));
            }
        }
    }
}