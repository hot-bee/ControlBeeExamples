using ControlBee.Models;
using Message = ControlBee.Models.Message;

namespace PnpExample.Actors.Head;

public class AutoState(HeadActor actor) : State<HeadActor>(actor)
{
    private Guid _readyToPickupId;
    private Guid _readyToPlaceId;

    public override void Dispose()
    {
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
            case "Pickup":
                Actor.PushState(new PickingState(Actor, (int)message.Payload!));
                ClearRequests();
                return true;
            case "Place":
                Actor.PushState(new PlacingState(Actor));
                ClearRequests();
                return true;
        }

        return false;
    }

    private bool Scan()
    {
        if (Actor.GetPeerStatus(Actor.Syncer, "_auto") is not true)
        {
            Actor.MoveHome();
            Actor.State = new IdleState(Actor);
            return true;
        }

        if (Actor.Products.Value[0].Exists || Actor.Products.Value[1].Exists)
            if (_readyToPlaceId == Guid.Empty)
            {
                using var _ = new StatusGroup(Actor);
                Actor.SetStatusByActor(Actor.Syncer, "OccupiedPickers", Actor.GetOccupiedPickers());
                _readyToPlaceId = Guid.NewGuid();
                Actor.SetStatusByActor(Actor.Syncer, "ReadyToPlace", _readyToPlaceId);
                return true;
            }

        if (!Actor.Products.Value[0].Exists || !Actor.Products.Value[1].Exists)
            if (_readyToPickupId == Guid.Empty)
            {
                using var _ = new StatusGroup(Actor);
                Actor.SetStatusByActor(Actor.Syncer, "VacantPickers", Actor.GetVacantPickers());
                _readyToPickupId = Guid.NewGuid();
                Actor.SetStatusByActor(Actor.Syncer, "ReadyToPickup", _readyToPickupId);
                return true;
            }

        return false;
    }

    private void ClearRequests()
    {
        using var _ = new StatusGroup(Actor);
        _readyToPlaceId = Guid.Empty;
        _readyToPickupId = Guid.Empty;
        Actor.SetStatusByActor(Actor.Syncer, "VacantPickers", null);
        Actor.SetStatusByActor(Actor.Syncer, "OccupiedPickers", null);
        Actor.SetStatusByActor(Actor.Syncer, "ReadyToPlace", Guid.Empty);
        Actor.SetStatusByActor(Actor.Syncer, "ReadyToPickup", Guid.Empty);
    }
}