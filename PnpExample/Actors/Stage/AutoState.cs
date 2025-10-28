using PnpExample.Constants;
using ControlBee.Models;
using ControlBeeAbstract.Exceptions;
using Message = ControlBee.Models.Message;

namespace PnpExample.Actors.Stage;

public class AutoState(StageActor actor) : State<StageActor>(actor)
{
    private Guid _readyToLoadId;
    private Guid _readyToRetreat;
    private Guid _readyToSwapId;
    private Guid _readyToTransferId;
    private Guid _readyToUnloadId;

    public override void Dispose()
    {
        Actor.TimerMilliseconds = -1;
        Actor.SetStatus("_auto", false);
        ClearRequests();
    }

    public override bool ProcessMessage(Message message)
    {
        switch (message.Name)
        {
            case StateEntryMessage.MessageName:
                Actor.SetStatus("_auto", true);
                return true;
            case "_status":
            case TimerMessage.MessageName:
                return Scan();
            case "Transfer":
                Actor.PushState(new TransferringDieState(Actor));
                ClearRequests();
                return true;
        }

        return false;
    }

    private bool Scan()
    {
        if (Actor.GetPeerStatus(Actor.Syncer, "_auto") is not true)
        {
            Actor.State = new IdleState(Actor);
            return true;
        }

        if (Actor.Tray.Value.Exists)
        {
            if (Actor.GetNotWorkedCellCount() == 0)
            {
                Actor.Clear();
                return true;
            }
            if (_readyToTransferId == Guid.Empty)
            {
                _readyToTransferId = Guid.NewGuid();
                Actor.SetStatusByActor(Actor.Syncer, "ReadyToTransfer", _readyToTransferId);
                return true;
            }
        }
        else
        {
            Actor.Clear();
            return true;
        }

        return false;
    }

    private void ClearRequests()
    {
        using var _ = new StatusGroup(Actor);
        _readyToTransferId = Guid.Empty;
        _readyToUnloadId = Guid.Empty;
        _readyToLoadId = Guid.Empty;
        _readyToSwapId = Guid.Empty;
        _readyToRetreat = Guid.Empty;
        Actor.SetStatusByActor(Actor.Syncer, "ReadyToSwap", Guid.Empty);
        Actor.SetStatusByActor(Actor.Syncer, "ReadyToTransfer", Guid.Empty);
        Actor.SetStatusByActor(Actor.Syncer, "ReadyToUnload", Guid.Empty);
        Actor.SetStatusByActor(Actor.Syncer, "ReadyToLoad", Guid.Empty);
        Actor.SetStatusByActor(Actor.Syncer, "ReadyToRetreat", Guid.Empty);
    }
}