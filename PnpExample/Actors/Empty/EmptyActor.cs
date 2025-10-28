using ControlBee.Interfaces;
using ControlBee.Models;
using ControlBee.Variables;
using ControlBeeAbstract.Exceptions;
using log4net;

namespace PnpExample.Actors.Empty;

public class EmptyActor : Actor
{
    private static readonly ILog Logger = LogManager.GetLogger("Sequence");
    
    public EmptyActor(ActorConfig config)
        : base(config)
    {
        State = new InactiveState(this);
    }

    #region Peers

    public IActor Syncer = null!;
    public IActor Parent = null!;

    #endregion

    #region Input
    public IDigitalInput Input1 = new DigitalInputPlaceholder();
    #endregion

    #region Output
    public IDigitalOutput Output1 = new DigitalOutputPlaceholder();
    #endregion

    #region Actuator
    public IBinaryActuator BinaryActuator1;
    #endregion

    #region Variables

    public Variable<int> Variable1 = new();
    #endregion

    #region Dialog
    public IDialog Dialog1 = new DialogPlaceholder();
    #endregion

    public void SetPeers(
        IActor syncer,
        IActor parent
    )
    {
        Syncer = syncer;
        Parent = parent;
        InitPeers([syncer, parent]);
    }

    protected override IState CreateErrorState(SequenceError error)
    {
        return new ErrorState<EmptyActor>(this, error);
    }

    public override IState CreateIdleState()
    {
        return new IdleState(this);
    }
    public override string[] GetFunctions()
    {
        return [];
    }
}
