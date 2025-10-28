using PnpExample.Constants;
using PnpExample.Models;
using PnpExample.Utils;
using ControlBee.Interfaces;
using ControlBee.Models;
using ControlBee.Variables;
using ControlBeeAbstract.Constants;
using ControlBeeAbstract.Exceptions;
using log4net;
using Message = ControlBee.Models.Message;

namespace PnpExample.Actors.Stage;

public class StageActor : Actor
{
    private static readonly ILog Logger = LogManager.GetLogger("Sequence");
    public readonly TransferDirection TransferDirection;
    public const double NearRange = 1.0;
    private readonly bool _fakeMode;

    public IVision TargetHeadVision = new VisionPlaceholder();

    public IDialog AlreadyHaveTrayError = new DialogPlaceholder();
    public IDialog LimitSensorDetError = new DialogPlaceholder();
    public IDialog NotHaveTrayError = new DialogPlaceholder();
    public IDialog TrayWorkDoneError = new DialogPlaceholder();
    public IDialog OperationNotPermittedError = new DialogPlaceholder();
    public IDialog CollisionDistanceError = new DialogPlaceholder();
    public IDialog PostInspectionNgCellExist = new DialogPlaceholder();
    public IDialog VisionProcessStopRequested = new DialogPlaceholder();
    public IDialog VisionError = new DialogPlaceholder();

    public IDigitalInput TrayClampOn = new DigitalInputPlaceholder();
    public IDigitalInput TrayDet = new DigitalInputPlaceholder();
    public IDigitalInput StageUpDet = new DigitalInputPlaceholder();
    public IDigitalInput StageDownDet = new DigitalInputPlaceholder();
    public IDigitalInput OtherStageDownDet = new DigitalInputPlaceholder();
    public IDigitalInput OtherStageUpDet = new DigitalInputPlaceholder();

    public IDigitalOutput TrayClamp = new DigitalOutputPlaceholder();
    public IDigitalOutput StageUp = new DigitalOutputPlaceholder();
    public IDigitalOutput StageDown = new DigitalOutputPlaceholder();

    public IBinaryActuator StageUpDown;

    public IAxis X;
    public IAxis Z;
    public IAxis SX;
    public IAxis OX;

    public Variable<bool> UseRowWiseDirection = new(VariableScope.Global);
    public Variable<bool> UseStableWait = new(VariableScope.Global);
    public Variable<GridContainer> Tray = new(VariableScope.Temporary);
    public Variable<Position1D> TrayPushPosX = new(VariableScope.Global);
    public Variable<Position1D> WaitPosX = new(VariableScope.Global);
    public Variable<Position1D> FirstPosX = new(VariableScope.Global);
    public Variable<Position1D> LastPosX = new(VariableScope.Global);
    public Variable<Position1D> LoadPosX = new(VariableScope.Global);
    public Variable<Position1D> LoadPosSX = new(VariableScope.Global);
    public Variable<Position1D> LoadPosZ = new(VariableScope.Global);
    public Variable<Position1D> TouchPosZ = new(VariableScope.Global);
    public Variable<Position1D> UnloadPosX = new(VariableScope.Global);
    public Variable<Position1D> UnloadPosSX = new(VariableScope.Global);
    public Variable<Position1D> UnloadPosZ = new(VariableScope.Global);
    public Variable<Position1D> CollisionFreePosOX = new(VariableScope.Global);
    public Variable<int> InspectionRetryCount = new(VariableScope.Global, 5);
    public Variable<double> SoftDownOffset = new(VariableScope.Local, -1);
    public Variable<double> FarRange = new(VariableScope.Local, 5);
    public Variable<int> TrayRowCount = new(VariableScope.Local, 10);
    public Variable<int> TrayColCount = new(VariableScope.Local, 10);
    public Variable<int> PositionIndex = new(VariableScope.Temporary, 0);

    public StageActor(ActorConfig config, StageType stageType, TransferDirection transferDirection, bool bunkBedType)
        : base(config)
    {
        StageType = stageType;
        BunkBedType = bunkBedType;
        TransferDirection = transferDirection;
        State = new InactiveState(this);
        TimerMilliseconds = 200;

        X = config.AxisFactory.Create();
        Z = config.AxisFactory.Create();
        SX = config.AxisFactory.Create();
        OX = config.AxisFactory.Create();

        PositionAxesMap.Add(LoadPosX, [X]);
        PositionAxesMap.Add(UnloadPosX, [X]);
        PositionAxesMap.Add(TrayPushPosX, [X]);
        PositionAxesMap.Add(WaitPosX, [X]);
        PositionAxesMap.Add(LoadPosZ, [Z]);
        PositionAxesMap.Add(TouchPosZ, [Z]);
        PositionAxesMap.Add(UnloadPosZ, [Z]);
        PositionAxesMap.Add(FirstPosX, [X]);
        PositionAxesMap.Add(LastPosX, [X]);
        PositionAxesMap.Add(LoadPosSX, [SX]);
        PositionAxesMap.Add(UnloadPosSX, [SX]);
        PositionAxesMap.Add(CollisionFreePosOX, [OX]);

        StageUpDown = config.BinaryActuatorFactory.Create(StageUp, StageDown, StageUpDet, StageDownDet);

        _fakeMode = config.SystemConfigurations.FakeMode;
    }

    public StageType StageType { get; }
    public bool BunkBedType { get; }

    public void SetPeers(IActor syncer, IActor head, IActor? otherStage)
    {
        Syncer = syncer;
        Head = head;
        OtherStage = otherStage;
        List<IActor?> peerList = [Syncer, Head, CameraHead, OtherStage, PrevActor, NextActor];
        InitPeers(peerList.Where(x => x != null).Distinct().ToArray()!);
    }

    public override void Init(ActorConfig config)
    {
        base.Init(config);
        Z.SetInitializeAction(() => SequenceUtils.InitializeByBuffer(SkipWaitSensor, Z, LimitSensorDetError));
        X.SetInitializeAction(() => SequenceUtils.InitializeByBuffer(SkipWaitSensor, X, LimitSensorDetError));
        SX.SetInitializeAction(() => SequenceUtils.InitializeByBuffer(SkipWaitSensor, SX, LimitSensorDetError));
    }

    protected override IState CreateErrorState(SequenceError error)
    {
        return new ErrorState<StageActor>(this, error);
    }

    public override IState CreateIdleState()
    {
        return new IdleState(this);
    }

    public override string[] GetFunctions()
    {
        return ["Clear", "StageLeft", "StageRight", "LastWorkPos", "SoftDownZ"];
    }

    public void SoftDownZ()
    {
        TryMoveDown(TouchPosZ.Value[0], Z.GetNormalSpeed());
        TryMoveDown(TouchPosZ.Value[0] + SoftDownOffset.Value, Z.GetInitSpeed());
        TryMoveDown(LoadPosZ.Value[0], Z.GetNormalSpeed());
    }

    private void TryMoveDown(double targetPosZ, SpeedProfile speed)
    {
        if (Z.GetPosition() <= targetPosZ) return;

        Z.SetSpeed(speed);
        Z.MoveAndWait(targetPosZ);
    }

    public (int count, int row, int col) GetWorkableCell()
    {
        if (!Tray.Value.Exists) return (0, -1, -1);
        var count = 0;
        var foundingValue = TransferDirection != TransferDirection.TransferIn;
        var found = false;
        var foundRow = -1;
        var foundCol = -1;
        for (var j = 0; j < Tray.Value.Size.Item2; j++)
        {
            var i = j % 2 == 0 || UseRowWiseDirection.Value ? 0 : Tray.Value.Size.Item1 - 1;
            while (0 <= i && i < Tray.Value.Size.Item1)
                try
                {
                    if (Tray.Value.CellExists[i, j] != foundingValue) continue;
                    if (!found)
                    {
                        found = true;
                        foundRow = i;
                        foundCol = j;
                    }

                    count++;
                }
                finally
                {
                    if (j % 2 == 0 || UseRowWiseDirection.Value) i++;
                    else i--;
                }
        }

        return (count, foundRow, foundCol);
    }

    public (int row, int col)? GetNotInspectedCell()
    {
        if (!Tray.Value.Exists) return null;
        var foundingValue = TransferDirection != TransferDirection.TransferIn;
        for (var j = 0; j < Tray.Value.Size.Item2; j++)
        {
            var i = j % 2 == 0 || UseRowWiseDirection.Value ? 0 : Tray.Value.Size.Item1 - 1;
            while (0 <= i && i < Tray.Value.Size.Item1)
                try
                {
                    if (Tray.Value.CellExists[i, j] == foundingValue
                        && !Tray.Value.CellVisionResult[i, j].Inspected)
                        return (i, j);
                }
                finally
                {
                    if (j % 2 == 0 || UseRowWiseDirection.Value) i++;
                    else i--;
                }
        }

        return null;
    }

    public int GetWorkableMinCol()
    {
        if (!Tray.Value.Exists) return -1;
        var foundingValue = TransferDirection != TransferDirection.TransferIn;
        var (row, col) = Tray.Value.Size;
        for (var c = 0; c < col; c++)
        for (var r = 0; r < row; r++)
            if (Tray.Value.CellExists[r, c] == foundingValue &&
                !Tray.Value.CellVisionResult[r, c].Ng)
                return c;

        return Tray.Value.Size.Item2;
    }

    public bool GetPostInspectionNgExists()
    {
        var (row, col) = Tray.Value.Size;
        for (var c = 0; c < col; c++)
        for (var r = 0; r < row; r++)
            if (Tray.Value.CellPostVisionResult[r, c].Ng)
                return true;

        return false;
    }

    public int GetNotWorkedCellCount()
    {
        if (!Tray.Value.Exists) return 0;
        var count = 0;
        var foundingValue = TransferDirection != TransferDirection.TransferIn;
        for (var j = 0; j < Tray.Value.Size.Item2; j++)
        {
            var i = j % 2 == 0 ? 0 : Tray.Value.Size.Item1 - 1;
            while (0 <= i && i < Tray.Value.Size.Item1)
                try
                {
                    if (Tray.Value.CellExists[i, j] != foundingValue) continue;

                    count++;
                }
                finally
                {
                    if (j % 2 == 0) i++;
                    else i--;
                }
        }

        return count;
    }

    public override void Start()
    {
        Tray.ValueChanged += (sender, args) => { UpdateStatus(); };
        TrayRowCount.ValueChanged += (sender, args) => { UpdateStatus(); };
        UpdateStatus();
        base.Start();
    }

    private void UpdateStatus()
    {
        SetStatus("TrayRowCount", TrayRowCount.Value);
        SetStatus("NotWorkedCellCount", GetNotWorkedCellCount());
        SetStatusByActor(Syncer, "WorkableMinCol", GetWorkableMinCol());
        SetStatus("TrayExists", Tray.Value.Exists);
        SetStatus("WorkableCellCount", GetWorkableCell().count);
    }

    public double GetColPosition(int col)
    {
        var firstPos = FirstPosX.Value[0];
        var lastPost = LastPosX.Value[0];
        var pitch = (lastPost - firstPos) / (TrayColCount.Value - 1);
        var pos = firstPos + pitch * col;
        return pos;
    }

    public void Clear()
    {
        Tray.Value = StageType switch
        {
            StageType.Carrier => new GridContainer(TrayRowCount.Value, TrayColCount.Value, false) { Exists = true },
            StageType.Tray => new GridContainer(TrayRowCount.Value, TrayColCount.Value, true) { Exists = true },
            _ => Tray.Value
        };
    }

    public void CheckSafety(int? col = null)
    {
        if (_fakeMode) return;
        if (!BunkBedType) return;

        Logger.Debug($"Stage safety check." +
                     $" stage up: {StageUpDet.IsOn()}," +
                     $" other stage up: {OtherStageUpDet.IsOn()}," +
                     $" col: {col}, ApprovalCol: {ApprovalStageUpColumn}");

        if (StageUpDet.IsOn() == OtherStageUpDet.IsOn() ||
            StageDownDet.IsOn() == OtherStageDownDet.IsOn())
        {
            if (col >= ApprovalStageUpColumn) return;
            OperationNotPermittedError.Show();
            throw new SequenceError();
        }
    }

    protected override bool ProcessMessage(Message message)
    {
        switch (message.Name)
        {
            case "MoveToHomePos":
            case "MoveToSavedPos":
            {
                CheckSafety();

                break;
            }
        }

        return base.ProcessMessage(message);
    }

    public bool IsActive => Name.Equals(GetPeerStatus(Syncer, "ActiveSourceStage"));
    public int ApprovalStageUpColumn => (int)(GetPeerStatus(Syncer, "ApprovalStageUpColumn") ?? 0);

    #region Peers

    public IActor Syncer = null!;
    public IActor PrevActor = null!;
    public IActor NextActor = null!;
    public IActor Head = null!;
    public IActor CameraHead = null!;
    public IActor OtherStage = null!;

    #endregion
}