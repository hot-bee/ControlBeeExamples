using ControlBee.Models;
using ControlBee.Utils;
using ControlBeeAbstract.Constants;
using ControlBeeAbstract.Exceptions;
using log4net;
using Message = ControlBee.Models.Message;

namespace PnpExample.Actors.Picker;

public class ProcessingCycleState(PickerActor actor, Message requestMessage, string cycleName)
    : State<PickerActor>(actor)
{
    private static readonly ILog Logger = LogManager.GetLogger("Sequence");

    public override bool ProcessMessage(Message message)
    {
        switch (message.Name)
        {
            case StateEntryMessage.MessageName:
                switch (cycleName)
                {
                    case "Pickup":
                    {
                        Logger.Info("Start pickup cycle.");
                        Actor.R.WaitNear(Actor.R.GetInitPos()[0], Actor.NearRangeR.Value);
                        Actor.Z.Wait();
                        var stageIndex = (int)DictPath.Start(requestMessage.DictPayload)["StageIndex"].Value!;
                        var angleOffset = (double)DictPath.Start(requestMessage.DictPayload)["AngleOffset"].Value!;
                        Logger.Info($"Place angle offset: {angleOffset}");
                        Logger.Info("Start motions.");

                        Actor.VacuumOn.OffAndWait();
                        Actor.VacuumBlow.OffAndWait();

                        Actor.Z.SetSpeed(Actor.Z.GetNormalSpeed());
                        Actor.R.SetSpeed(Actor.R.GetNormalSpeed());
                        var rPos = Actor.R.GetInitPos()[0] + angleOffset;
                        Actor.R.Move(rPos, true);

                        var pos = Actor.PickupZ.Value[stageIndex][0] + Actor.PickupOffsetZ.Value[stageIndex];
                        Actor.Z.SetSpeed(Actor.Z.GetNormalSpeed());
                        Actor.Z.MoveAndWait(pos);
                        Actor.VacuumOn.OnAndWait();

                        Logger.Info("Start Pickup Delay.");
                        Thread.Sleep(Actor.PickupDelay.Value);
                        Logger.Info("Finish Pickup Delay.");
                        requestMessage.Sender.Send(new Message(requestMessage, Actor, "PickerDown"));
                        break;
                    }
                    case "Place":
                    {
                        Logger.Info("Start place cycle.");
                        Actor.Z.Wait();
                        Logger.Info("Start motions.");
                        var angleOffset = (double)DictPath.Start(requestMessage.DictPayload)["AngleOffset"].Value!;
                        var posR = Actor.R.GetInitPos()[0] + angleOffset;
                        Actor.Z.SetSpeed(Actor.Z.GetNormalSpeed());
                        Actor.R.SetSpeed(Actor.R.GetNormalSpeed());
                        Actor.R.Move(posR, true);

                        var pos = Actor.PlaceZ.Value[0] + Actor.PlaceOffsetZ.Value;
                        Actor.Z.SetSpeed(Actor.Z.GetNormalSpeed());
                        Actor.Z.MoveAndWait(pos);

                        Actor.VacuumOn.OffAndWait();
                        Actor.VacuumBlow.OnAndWait();
                        Logger.Info("Start Place Delay.");
                        Thread.Sleep(Actor.PlaceDelay.Value);
                        Logger.Info("Finish Place Delay.");
                        requestMessage.Sender.Send(new Message(requestMessage, Actor, "PickerDown"));
                        break;
                    }
                }

                return true;
            case "_status":
                if (Actor.HasPeerFailed(requestMessage.Sender))
                    throw new SequenceError();
                return true;
            case "PickerUp":
            {
                switch (cycleName)
                {
                    case "Pickup":
                    {
                        PickerUp();
                        var pos = Actor.Z.GetInitPos()[0];
                        Actor.Z.WaitNear(pos, Actor.NearRangeZ.Value);
                        requestMessage.Sender.Send(new Message(requestMessage, Actor, "CycleDone"));
                        Actor.State = new IdleState(Actor);
                        break;
                    }
                    case "Place":
                    {
                        PickerUp();
                        var pos = Actor.Z.GetInitPos()[0];
                        Actor.Z.WaitNear(pos, Actor.NearRangeZ.Value);
                        Actor.VacuumBlow.OffAndWait();
                        requestMessage.Sender.Send(new Message(requestMessage, Actor, "CycleDone"));
                        Actor.State = new IdleState(Actor);
                        break;
                    }
                }

                return true;
            }
        }

        return false;
    }

    private void PickerUp()
    {
        Logger.Info("Start picker up.");
        Actor.R.Wait(PositionType.Command);

        var zPos = Actor.Z.GetPosition();
        Actor.Z.SetSpeed(Actor.Z.GetNormalSpeed());
        Actor.Z.GetInitPos().MoveAndWait();

        Actor.Z.WaitFar(zPos, Actor.FarRangeZ.Value);
        Actor.R.SetSpeed(Actor.R.GetNormalSpeed());
        Actor.R.GetInitPos().Move();

        Logger.Info("Finish picker up. (Still z moving)");
    }
}