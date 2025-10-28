using PnpExample.Utils;
using ControlBee.Interfaces;
using ControlBee.Models;
using ControlBee.Variables;
using ControlBeeAbstract.Exceptions;
using log4net;
using Message = ControlBee.Models.Message;

namespace PnpExample.Actors.Picker;

public class PickerActor : Actor
{
    private static readonly ILog Logger = LogManager.GetLogger("Sequence");

    public IAxis Z;
    public IAxis R;

    public Variable<double> NearRangeZ = new(VariableScope.Global, 10.0);
    public Variable<double> NearRangeR = new(VariableScope.Global, 10.0);
    public Variable<double> FarRangeZ = new(VariableScope.Global, 5.0);
    public Variable<Array1D<Position1D>> PickupZ = new(VariableScope.Global, new Array1D<Position1D>(2));
    public Variable<Position1D> PlaceZ = new(VariableScope.Global);

    public Variable<Array1D<double>> PickupOffsetZ = new(VariableScope.Local, new Array1D<double>(2));
    public Variable<double> PlaceOffsetZ = new(VariableScope.Local, 0.0);
    public Variable<int> PickupDelay = new(VariableScope.Local, 100);
    public Variable<int> PlaceDelay = new(VariableScope.Local, 100);

    public IDigitalOutput VacuumOn = new DigitalOutputPlaceholder();
    public IDigitalOutput VacuumBlow = new DigitalOutputPlaceholder();

    public IDialog LimitSensorDetError = new DialogPlaceholder();
    public IDialog PickerDownError = new DialogPlaceholder();

    public PickerActor(ActorConfig config)
        : base(config)
    {
        State = new InactiveState(this);

        Z = config.AxisFactory.Create();
        R = config.AxisFactory.Create();

        PositionAxesMap.Add(PickupZ, [Z]);
        PositionAxesMap.Add(PlaceZ, [Z]);
    }

    public void SetPeers(IActor syncer, IActor head)
    {
        Syncer = syncer;
        Head = head;
        InitPeers([Syncer, Head]);
    }

    public override void Init(ActorConfig config)
    {
        base.Init(config);
        Z.SetInitializeAction(() => SequenceUtils.InitializeByBuffer(SkipWaitSensor, Z, LimitSensorDetError));
        R.SetInitializeAction(() => SequenceUtils.InitializeByBuffer(SkipWaitSensor, R, LimitSensorDetError));
    }

    protected override IState CreateErrorState(SequenceError error)
    {
        return new ErrorState<PickerActor>(this, error);
    }

    public override IState CreateIdleState()
    {
        return new IdleState(this);
    }

    public override string[] GetFunctions()
    {
        return ["Home", "VacuumOn", "VacuumOff"];
    }

    protected override bool ProcessMessage(Message message)
    {
        var ret = base.ProcessMessage(message);
        switch (message.Name)
        {
            case "CheckSafetyReq":
                if (Math.Abs(Z.GetPosition() - Z.GetInitPos()[0]) > 1.0)
                {
                    PickerDownError.Show();
                    throw new SequenceError();
                }

                message.Sender.Send(new Message(message, this, "CheckSafetyRes"));
                return true;
        }

        return ret;
    }
    protected override IState CreateFatalErrorState(FatalSequenceError fatalError)
    {
        return new FatalErrorState(this);
    }

    #region Peers

    public IActor Syncer = null!;
    public IActor Head = null!;

    #endregion
}