using PnpExample.Constants;
using PnpExample.Models;
using PnpExample.Services;
using PnpExample.Utils;
using ControlBee.Constants;
using ControlBee.Interfaces;
using ControlBee.Models;
using ControlBee.Variables;
using ControlBeeAbstract.Exceptions;
using log4net;
using Message = ControlBee.Models.Message;
using SystemConfigurations = PnpExample.Models.SystemConfigurations;

namespace PnpExample.Actors.Head;

public class HeadActor : Actor
{
    private static readonly ILog Logger = LogManager.GetLogger("Sequence");
    public StageType SourceStageType { get; }
    public StageType TargetStageType { get; }
    public List<Task> InspectTasks = [];

    private int PickerCount
    {
        get
        {
            var count = 0;
            if (UsePickers.Value[0]) count++;
            if (UsePickers.Value[1]) count++;
            return count;
        }
    }

    #region Peers

    public IActor Syncer = null!;
    public IActor[] Pickers = [];
    public IActor[] SourceStages = [];
    public IActor TargetStage = null!;

    #endregion

    public IAxis Y;
    public IAxis StageX0;    // Only for safety check. Never control directly.
    public IAxis PickerZ0;  // Only for safety check. Never control directly.
    public IAxis PickerZ1;  // Only for safety check. Never control directly.

    public Variable<Array1D<bool>> UsePickers = new(VariableScope.Global, new Array1D<bool>([true, true]));
    public Variable<double> NearRange = new(VariableScope.Global, 10.0);
    public Variable<double> SafetyPickerZLimit = new(VariableScope.Global, 18.0);
    public Variable<Array1D<Product>> Products = new(VariableScope.Temporary, new Array1D<Product>(2));
    public Variable<Position1D> PickupPos = new(VariableScope.Global);
    public Variable<Position1D> PlacePos = new(VariableScope.Global);
    public Variable<Array1D<double>> VisionCenterX = new(VariableScope.Global, new Array1D<double>(2));
    public Variable<Array1D<double>> PickupOffsetX = new(VariableScope.Global, new Array1D<double>(2));
    public Variable<Array1D<double>> PickupOffsetY = new(VariableScope.Global, new Array1D<double>(2));
    public Variable<Array1D<double>> PickupOffsetR = new(VariableScope.Global, new Array1D<double>(2));
    public Variable<Array1D<double>> PlaceOffsetX = new(VariableScope.Global, new Array1D<double>(2));
    public Variable<Array1D<double>> PlaceOffsetY = new(VariableScope.Global, new Array1D<double>(2));
    public Variable<Array1D<double>> PlaceOffsetR = new(VariableScope.Global, new Array1D<double>(2));

    public IDialog AllPickersHaveProductError = new DialogPlaceholder();
    public IDialog NoPickerHasProductError = new DialogPlaceholder();
    public IDialog LimitSensorDetError = new DialogPlaceholder();
    public IDialog PickerZDownError = new DialogPlaceholder();
    private readonly bool _fakeMode;

    public ProductionLogger? PickupLogger;
    public ProductionLogger? PlaceLogger;

    public HeadActor(ActorConfig config, StageType sourceStageType, StageType targetStageType)
        : base(config)
    {
        SourceStageType = sourceStageType;
        TargetStageType = targetStageType;
        State = new InactiveState(this);

        Y = config.AxisFactory.Create();
        StageX0 = config.AxisFactory.Create();
        PickerZ0 = config.AxisFactory.Create();
        PickerZ1 = config.AxisFactory.Create();

        PositionAxesMap.Add(PickupPos, [Y]);
        PositionAxesMap.Add(PlacePos, [Y]);

        _fakeMode = config.SystemConfigurations.FakeMode;

        var systemPropertiesDataSource = config.SystemPropertiesDataSource;

        if (systemPropertiesDataSource.GetValue(Name, "PickupLogHeaders") is List<object> pickupLogHeaders)
            PickupLogger = new ProductionLogger(pickupLogHeaders.Cast<string>().ToList(), "pickup_logs");

        if (systemPropertiesDataSource.GetValue(Name, "PlaceLogHeaders") is List<object> placeLogHeaders)
            PlaceLogger = new ProductionLogger(placeLogHeaders.Cast<string>().ToList(), "place_logs");
    }

    public void SetPeers(IActor syncer, IActor sourceStageA, IActor targetStage, IActor pickerA, IActor pickerB)
    {
        Syncer = syncer;
        SourceStages = (new List<IActor?> { sourceStageA }).Where(x => x != null).ToArray()!;
        TargetStage = targetStage;
        Pickers = [pickerA, pickerB];
        List<IActor?> peerList = [Syncer, sourceStageA, TargetStage, pickerA, pickerB];
        InitPeers(peerList.Where(x => x != null).ToArray()!);
    }

    protected override IState CreateErrorState(SequenceError error)
    {
        return new ErrorState<HeadActor>(this, error);
    }

    public override IState CreateIdleState()
    {
        return new IdleState(this);
    }

    protected override string[] GetRegisteredFunctions()
    {
        return [];
    }

    protected override bool IsFunctionAvailable(string functionName)
    {
        return functionName switch
        {
            _ => true
        };
    }

    public override void Init(ActorConfig config)
    {
        base.Init(config);
        Y.SetInitializeAction(() => SequenceUtils.InitializeByBuffer(SkipWaitSensor, Y, LimitSensorDetError));
    }

    public override void Start()
    { 
        ((IActorItemModifier)StageX0).Visible = false;
        ((IActorItemModifier)PickerZ0).Visible = false;
        ((IActorItemModifier)PickerZ1).Visible = false;

        Products.ValueChanged += (sender, args) => { UpdateStatus(); };
        UpdateStatus();
        base.Start();
    }

    public int[] GetOccupiedPickers()
    {
        var list = new List<int>();
        for (var i = 0; i < 2; i++)
        {
            if (UsePickers.Value[i] && Products.Value[i].Exists) list.Add(i);
        }
        return list.ToArray();
    }

    public int[] GetVacantPickers()
    {
        var list = new List<int>();
        for (var i = 0; i < 2; i++)
        {
            if (UsePickers.Value[i] && !Products.Value[i].Exists) list.Add(i);
        }
        return list.ToArray();
    }

    public int GetNgCount()
    {
        var count = 0;
        for (var i = 0; i < 2; i++)
        {
            if (UsePickers.Value[i] && Products.Value[i].Exists
                                    && Products.Value[i].VisionNg) count++;
        }
        return count;
    }

    public void UpdateStatus()
    {
        using var _ = new StatusGroup(this);
        var vacantPickers = GetVacantPickers();
        SetStatus("VacantPickerCount", vacantPickers.Length);
        SetStatus("OccupiedPickerCount", PickerCount - vacantPickers.Length);
    }

    public void SafetyCheck()
    {
        if (_fakeMode) return;
        // TODO: Don't refer the axis of child directly.
        if ((Math.Abs(PickerZ0.GetPosition()) > SafetyPickerZLimit.Value && !PickerZ0.IsMoving())
            || (Math.Abs(PickerZ1.GetPosition()) > SafetyPickerZLimit.Value && !PickerZ1.IsMoving())
            || !PickerZ0.IsEnabled() || PickerZ0.IsAlarmed()
            || !PickerZ1.IsEnabled() || PickerZ1.IsAlarmed()
            || PickerZ0.GetDevice() is null
            || PickerZ1.GetDevice() is null)
        {
            Pickers[0].Send(new Message(this, "Home"));
            Pickers[1].Send(new Message(this, "Home"));
            PickerZDownError.Show();
            throw new SequenceError();
        }
    }
    public double GetPickupPos(int pickerIndex, int sourceStageIndex)
    {
        return PickupPos.Value[0];
    }
    public double GetPlacePos(int pickerIndex)
    {
        return PlacePos.Value[0];
    }
    public void WaitingInspectTasks()
    {
        InspectTasks.ForEach(task =>
        {
            try
            {
                task.Wait();
            }
            catch (Exception ex)
            {
                throw new SequenceError(ex.Message);
            }
        });
        InspectTasks.Clear();
    }

    public void MoveHome()
    {
        Y.Wait();
        SafetyCheck();
        Y.SetSpeed(Y.GetJogSpeed(JogSpeedLevel.Fast));
        Y.GetInitPos().MoveAndWait();
    }
}