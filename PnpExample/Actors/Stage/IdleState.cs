using ControlBee.Models;
using log4net;
using Message = ControlBee.Models.Message;

namespace PnpExample.Actors.Stage;

public class IdleState(StageActor actor) : State<StageActor>(actor)
{
    private static readonly ILog Logger = LogManager.GetLogger("Sequence");

    public override void Dispose()
    {
        Actor.SetStatus("_ready", false);
    }

    public override bool ProcessMessage(Message message)
    {
        switch (message.Name)
        {
            case StateEntryMessage.MessageName:
                Actor.SetStatus("_ready", true);
                return true;
            case "_inactivate":
                Actor.State = new InactiveState(Actor);
                return true;
            case "_status":
                if (Actor.GetPeerStatus(actor.Syncer, "_auto") is true)
                {
                    Actor.State = new AutoState(Actor);
                    return true;
                }

                break;
            case "Transfer":
                Actor.State = new TransferringDieState(Actor);
                return true;
            case "Clear":
                Actor.Clear();
                return true;
            case "StageLeft":
                Actor.CheckSafety();
                Actor.PositionIndex.Value++;
                if (Actor.PositionIndex.Value == Actor.TrayColCount.Value)
                    Actor.PositionIndex.Value = Actor.TrayColCount.Value - 1;
                MoveStage();
                break;
            case "StageRight":
                Actor.CheckSafety();
                Actor.PositionIndex.Value--;
                if (Actor.PositionIndex.Value < 0) Actor.PositionIndex.Value = 0;
                MoveStage();
                break;
            case "LastWorkPos":
                Actor.CheckSafety();
                MoveStage();
                break;
            case "SoftDownZ":
                Actor.SoftDownZ();
                break;
        }

        if (Actor.GetFunctions().Contains(message.Name))
        {
        }

        return false;
    }

    private void MoveStage()
    {
        var pos = Actor.GetColPosition(Actor.PositionIndex.Value);
        Actor.X.SetSpeed(Actor.X.GetNormalSpeed());
        Actor.X.MoveAndWait(pos);
    }
}