using ControlBee.Constants;
using ControlBee.Models;
using ControlBee.Utils;
using ControlBeeAbstract.Devices;
using ControlBeeAbstract.Exceptions;
using log4net;
using Message = ControlBee.Models.Message;


namespace PnpExample.Actors.Picker;

public class IdleState(PickerActor actor) : State<PickerActor>(actor)
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
                break;
            case "Pickup":
                Actor.State = new ProcessingCycleState(Actor, message, "Pickup");
                return true;
            case "Place":
                Actor.State = new ProcessingCycleState(Actor, message, "Place");
                return true;
            case "Home":
            {
                var slow = DictPath.Start(message.DictPayload)["Slow"].Value is true;
                Actor.Z.Wait();
                Actor.Z.SetSpeed(slow ? Actor.Z.GetJogSpeed(JogSpeedLevel.Fast) 
                    : Actor.Z.GetNormalSpeed());
                var pos = Actor.Z.GetInitPos()[0];
                Actor.Z.Move(pos);
                Actor.Z.WaitNear(pos, Actor.NearRangeZ.Value);
                message.Sender.Send(new Message(message, Actor, "CycleDone"));
                break;
            }
            case "VacuumOn":
                Actor.VacuumBlow.OffAndWait();
                Actor.VacuumOn.OnAndWait();
                Actor.VacuumOn.OffAndWait();
                break;
            case "VacuumOff":
                Actor.VacuumOn.OffAndWait();
                Actor.VacuumBlow.OnAndWait();
                Actor.VacuumBlow.OffAndWait();
                break;

        }

        if (Actor.GetFunctions().Contains(message.Name))
        {
        }

        return false;
    }
}