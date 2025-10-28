using PnpExample.Constants;
using PnpExample.Interfaces;
using PnpExample.Services;
using ControlBee.Constants;
using ControlBee.Interfaces;
using ControlBee.Models;
using ControlBee.Sequences;
using ControlBee.Utils;
using ControlBee.Variables;
using ControlBeeAbstract.Exceptions;
using log4net;
using Microsoft.Data.Sqlite;
using Dict = System.Collections.Generic.Dictionary<string, object?>;
using Message = ControlBee.Models.Message;
using String = ControlBee.Variables.String;
using SystemConfigurations = PnpExample.Models.SystemConfigurations;

namespace PnpExample.Actors.Syncer;

public class SyncerActor : Actor
{
    private static readonly ILog Logger = LogManager.GetLogger("Sequence");
    public IDialog Initializing = new DialogPlaceholder();
    public IDialog InitializationDone = new DialogPlaceholder();
    public IPeerContainer InternalPeers = null!;
    public PeerContainer Peers => (PeerContainer)InternalPeers;

    public Variable<int> IndexTime = new(VariableScope.Temporary);
    public enum MachineStateEnum
    {
        Error,
        Idle,
        Auto,
    }

    public IDigitalInput StartSwitchDet = new DigitalInputPlaceholder();
    public IDigitalInput StopSwitchDet = new DigitalInputPlaceholder();
    public IDigitalInput ResetSwitchDet = new DigitalInputPlaceholder();

    public IDigitalOutput StartSwitch = new DigitalOutputPlaceholder();
    public IDigitalOutput StopSwitch = new DigitalOutputPlaceholder();
    public IDigitalOutput ResetSwitch = new DigitalOutputPlaceholder();
    public IDigitalOutput TowerLampRed = new DigitalOutputPlaceholder();
    public IDigitalOutput TowerLampYellow = new DigitalOutputPlaceholder();
    public IDigitalOutput TowerLampGreen = new DigitalOutputPlaceholder();
    public IDigitalOutput TowerLampBuzzer = new DigitalOutputPlaceholder();

    private readonly List<string> _pendingGrant = [];

    public MachineStatus _MachineStatus = MachineStatus.Idle;
    public enum MachineStatus
    {
        Idle,
        Auto,
        Work
    }
    private Stopwatch stopwatchForWorkTime = new();
    public Stopwatch stopwatcLastWork = new();

    private const string EventStopped = "100";
    private const string EventStarted = "200";
    private const string EventWorkedPick = "300";
    private const string EventWorkedPlace = "300";

    public Variable<String> IdleTime = new(VariableScope.Temporary);
    public Variable<String> AutoTime = new(VariableScope.Temporary);
    public Variable<String> WorkTime = new(VariableScope.Temporary);

    public SyncerActor(ActorConfig config)
        : base(config)
    {
        State = new IdleState(this);
        TimerMilliseconds = -1;
        Status["ActorInitializationStatus"] = new Dictionary<string, object>();
        Status["AllInitialized"] = false;

        TimerMilliseconds = 200;
        stopwatchForWorkTime.Restart();
    }

    public Dictionary<string, object> ActorInitializationStatus =>
        (Dictionary<string, object>)Status["ActorInitializationStatus"]!;

    public virtual void SetPeers(IPeerContainer peerContainer)
    {
        InternalPeers = peerContainer;
        InitPeers(peerContainer.ToArray());
    }

    public override void Start()
    {
        SetStatus("Grant", null);
        base.Start();
        foreach (var peer in InternalPeers.ToArray())
            ActorInitializationStatus[peer.Name] = InitializationStatus.Uninitialized.ToString();
        PublishStatus();
    }

    public void SetGlobalInitializeSequence(GlobalInitializeSequence globalInitializeSequence)
    {
        // TODO: Unsubscribe the event
        globalInitializeSequence.StateChanged += GlobalInitializeSequence_StateChanged;
    }

    private void GlobalInitializeSequence_StateChanged(
        object? sender,
        (string actorName, InitializationStatus status) e
    )
    {
        ActorInitializationStatus[e.actorName] = e.status.ToString();
        Status["AllInitialized"] = ActorInitializationStatus.All(x =>
            x.Value as string == InitializationStatus.Initialized.ToString()
        );
        PublishStatus();
    }

    protected override IState CreateErrorState(SequenceError error)
    {
        return new ErrorState(this);
    }

    public void ChangeIndicatorLights(MachineStateEnum t)
    {
        TowerLampYellow.OffAndWait();
        TowerLampRed.OffAndWait();
        TowerLampGreen.OffAndWait();
        TowerLampBuzzer.OffAndWait();

        StopSwitch.OffAndWait();
        StartSwitch.OffAndWait();
        ResetSwitch.OffAndWait();

        switch (t)
        {
            case MachineStateEnum.Error:
                TowerLampRed.OnAndWait();
                ResetSwitch.OnAndWait();
                TowerLampBuzzer.OnAndWait();
                break;
            case MachineStateEnum.Auto:
                TowerLampGreen.OnAndWait();
                StartSwitch.OnAndWait();
                break;
            case MachineStateEnum.Idle:
                TowerLampYellow.OnAndWait();
                StopSwitch.OnAndWait();
                break;
        }
    }

    protected override bool ProcessMessage(Message message)
    {

        var ret = base.ProcessMessage(message);
        switch (message.Name)
        {
            case TimerMessage.MessageName:
            {
                if (stopwatchForWorkTime.ElapsedMilliseconds > 10_000)
                {
                    UpdateWorkTime();
                    stopwatchForWorkTime.Restart();
                }

                if (_MachineStatus == MachineStatus.Work && stopwatcLastWork.ElapsedMilliseconds > 10_000)
                    _MachineStatus = MachineStatus.Auto;

                break;
            }
            case "IdleEvent":
            {
                _MachineStatus = MachineStatus.Idle;
                break;
            }
            case "AutoEvent":
            {
                _MachineStatus = MachineStatus.Auto;
                break;
            }
            case "WorkEvent":
            {
                stopwatcLastWork.Restart();

                if (_MachineStatus == MachineStatus.Auto)
                    _MachineStatus = MachineStatus.Work;

                break;
            }
            case "_status":
            {
                if (DictPath.Start(message.DictPayload)["_inactive"].Value is true)
                {
                    if (GetStatus("ActorInitializationStatus") is Dict actorInitializationStatus)
                    {
                        if ((string)actorInitializationStatus[message.Sender.Name]! !=
                            InitializationStatus.Uninitialized.ToString())
                        {
                            actorInitializationStatus = DictCopy.Copy(actorInitializationStatus);
                            actorInitializationStatus[message.Sender.Name] = InitializationStatus.Uninitialized.ToString();
                            SetStatus("ActorInitializationStatus", actorInitializationStatus);
                        }
                    }
                }
                break;
            }
            case "_stop":
            {
                foreach (var peer in InternalPeers.ToArray())
                    peer.Send(new Message(this, "_stop"));

                break;
            }
            case "AcquireGrant":
            {
                var requester = (string)message.Payload!;
                Logger.Info($"Acquiring grant. {requester}");
                if (requester.Equals(GetStatus("Grant"))) break; 
                if (_pendingGrant.Contains(requester)) break;
                _pendingGrant.Add(requester);
                ProcessGrant();
                break;
            }
            case "ReleaseGrant":
            {
                var requester = (string)message.Payload!;
                Logger.Info($"Releasing grant. {requester}");
                if (requester.Equals(GetStatus("Grant"))) SetStatus("Grant", null);
                _pendingGrant.Remove(requester);
                ProcessGrant();
                break;
            }
        }
        return ret;
    }

    private bool IsWorkEvent(string eventName) => eventName == EventWorkedPick || eventName == EventWorkedPlace;
    private string FormatTimeSpanToHms(TimeSpan timeSpan) => $"{(int)timeSpan.TotalDays}d {timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";

    private void UpdateWorkTime()
    {
        var dbConnection = (SqliteConnection)EventManager.GetDatabase().GetConnection();

        DateTime currentTime = DateTime.UtcNow.ToLocalTime();
        DateTime startOfToday = currentTime.Date;
        TimeSpan tenSeconds = TimeSpan.FromSeconds(10);

        string? lastAutoEventNameBeforeToday = null;

        using (var command = dbConnection.CreateCommand())
        {
            command.CommandText = @"
            SELECT created_at, code
            FROM events
            WHERE created_at < $since
              AND code IN ($started, $stopped)
            ORDER BY created_at DESC
            LIMIT 1;";
            command.Parameters.AddWithValue("$since", startOfToday);
            command.Parameters.AddWithValue("$started", EventStarted);
            command.Parameters.AddWithValue("$stopped", EventStopped);

            using var reader = command.ExecuteReader();
            if (reader.Read())
                lastAutoEventNameBeforeToday = reader.GetString(1);
        }

        bool isAutoModeActive = lastAutoEventNameBeforeToday == EventStarted;

        DateTime? lastWorkEventTimeBeforeToday = null;
        using (var command = dbConnection.CreateCommand())
        {
            command.CommandText = @"
            SELECT created_at
            FROM events
            WHERE created_at < $since
              AND code IN ($pick, $place)
            ORDER BY created_at DESC
            LIMIT 1;";
            command.Parameters.AddWithValue("$since", startOfToday);
            command.Parameters.AddWithValue("$pick", EventWorkedPick);
            command.Parameters.AddWithValue("$place", EventWorkedPlace);

            using var reader = command.ExecuteReader();
            if (reader.Read())
                lastWorkEventTimeBeforeToday = reader.GetDateTime(0);
        }

        bool isWorkSessionActive = lastWorkEventTimeBeforeToday.HasValue &&
                                   (startOfToday - lastWorkEventTimeBeforeToday.Value) <= tenSeconds;
        DateTime currentWorkSessionStartTime = startOfToday;

        var eventListForToday = new List<(DateTime eventTime, string eventName)>();
        using (var command = dbConnection.CreateCommand())
        {
            command.CommandText = @"
            SELECT created_at, code
            FROM events
            WHERE created_at >= $since AND created_at <= $now
              AND code IN ($started, $stopped, $pick, $place)
            ORDER BY created_at ASC;";
            command.Parameters.AddWithValue("$since", startOfToday);
            command.Parameters.AddWithValue("$now", currentTime);
            command.Parameters.AddWithValue("$started", EventStarted);
            command.Parameters.AddWithValue("$stopped", EventStopped);
            command.Parameters.AddWithValue("$pick", EventWorkedPick);
            command.Parameters.AddWithValue("$place", EventWorkedPlace);

            using var reader = command.ExecuteReader();
            while (reader.Read())
                eventListForToday.Add((reader.GetDateTime(0), reader.GetString(1)));
        }

        TimeSpan totalIdleDuration = TimeSpan.Zero;
        TimeSpan totalAutoDuration = TimeSpan.Zero;
        TimeSpan totalWorkDuration = TimeSpan.Zero;

        DateTime lastProcessedEventTime = startOfToday;
        DateTime? previousWorkEventTime = lastWorkEventTimeBeforeToday;

        foreach (var (eventTime, eventName) in eventListForToday)
        {
            if (isAutoModeActive)
                totalAutoDuration += (eventTime - lastProcessedEventTime);
            else
                totalIdleDuration += (eventTime - lastProcessedEventTime);

            lastProcessedEventTime = eventTime;

            if (eventName == EventStarted)
                isAutoModeActive = true;
            else if (eventName == EventStopped)
                isAutoModeActive = false;

            if (IsWorkEvent(eventName))
            {
                if (!isWorkSessionActive)
                {
                    isWorkSessionActive = true;
                    currentWorkSessionStartTime = eventTime;
                }
                else
                {
                    if (previousWorkEventTime.HasValue &&
                        (eventTime - previousWorkEventTime.Value) > tenSeconds)
                    {
                        totalWorkDuration += (previousWorkEventTime.Value - currentWorkSessionStartTime);
                        currentWorkSessionStartTime = eventTime;
                    }
                }
                previousWorkEventTime = eventTime;
            }
        }

        if (isAutoModeActive)
            totalAutoDuration += (currentTime - lastProcessedEventTime);
        else
            totalIdleDuration += (currentTime - lastProcessedEventTime);

        if (isWorkSessionActive && previousWorkEventTime.HasValue)
            totalWorkDuration += (previousWorkEventTime.Value - currentWorkSessionStartTime);

        if (totalIdleDuration < TimeSpan.Zero) totalIdleDuration = TimeSpan.Zero;
        if (totalAutoDuration < TimeSpan.Zero) totalAutoDuration = TimeSpan.Zero;
        if (totalWorkDuration < TimeSpan.Zero) totalWorkDuration = TimeSpan.Zero;

        IdleTime.Value = new String(FormatTimeSpanToHms(totalIdleDuration));
        AutoTime.Value = new String(FormatTimeSpanToHms(totalAutoDuration));
        WorkTime.Value = new String(FormatTimeSpanToHms(totalWorkDuration));
    }

    private void ProcessGrant()
    {
        if (GetStatus("Grant") == null && _pendingGrant.Count > 0)
        {
            var requester = _pendingGrant[0];
            _pendingGrant.RemoveAt(0);
            Logger.Info($"Granted. {requester}");
            SetStatus("Grant", requester);
        }
    }

    public StageType SourceStageType { get; }
    public StageType TargetStageType { get; }
    public bool HeadPickingPhase = true;
    public Variable<int> ActiveSourceStageIndex = new(VariableScope.Temporary);
    public Variable<Array1D<bool>> UseSourceStages = new(VariableScope.Temporary, new Array1D<bool>([true, true]));

    public IDialog UsableSourceTrayEmptyError = new DialogPlaceholder();

    public SyncerActor(ActorConfig config, StageType sourceStageType, StageType targetStageType) : this(config)
    {
        SourceStageType = sourceStageType;
        TargetStageType = targetStageType;
        State = new IdleState(this);
    }

    public override IState CreateIdleState()
    {
        return new IdleState(this);
    }

    public override string[] GetFunctions()
    {
        return ["LoadFromMagazineLoader", "LoadToTargetStage", "UnloadToOutConveyor", "LoadToSourceStage", "UnloadToLifter",
            "LoadToSourceStage0", "UnloadToLifter0",
            "LoadToSourceStage1", "UnloadToLifter1", "Pickup", "Place", "PutBack"];
    }
}
