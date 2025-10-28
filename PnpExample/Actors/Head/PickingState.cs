using PnpExample.Constants;
using PnpExample.Models;
using ControlBee.Interfaces;
using ControlBee.Models;
using ControlBeeAbstract.Exceptions;
using log4net;
using Message = ControlBee.Models.Message;
using Dict = System.Collections.Generic.Dictionary<string, object?>;

namespace PnpExample.Actors.Head;

/*
 * 1. Wait ReadyToTransfer
 * 2. Set ReadyToTransfer
 * 3. Set Transferring
 * 4. Wait Transferring
 * 5. Set Transferred
 */
public class PickingState(HeadActor actor, int stageIndex) : State<HeadActor>(actor)
{
    private static readonly ILog Logger = LogManager.GetLogger("Sequence");
    private readonly List<Guid> _pickupIds = [];
    private int _pickerDownCount;
    private List<int> _pickerIndices = [];
    private bool _stayHere;
    private bool _toEnd;
    private bool _waitReadyToTransfer;
    private bool _waitTransferring;
    private int? _transferRow;
    private int? _transferCol;
    private IActor ActiveSourceStage => Actor.SourceStages[stageIndex];
    public override void Dispose()
    {
        base.Dispose();
        using var _ = new StatusGroup(Actor);
        Actor.SetStatusByActor(ActiveSourceStage, "TransferredRows", null);
        Actor.SetStatusByActor(ActiveSourceStage, "Transferred", false);
        Actor.SetStatusByActor(ActiveSourceStage, "OutOfZone", false);
        Actor.SetStatusByActor(ActiveSourceStage, "StayHere", false);
        Actor.SetStatusByActor(ActiveSourceStage, "ReadyToTransfer", false);
        Actor.SetStatusByActor(ActiveSourceStage, "Transferring", false);
        Actor.SetStatusByActor(Actor.SourceStages[stageIndex], "CurrentPickerIndex", -1);
    }

    public override bool ProcessMessage(Message message)
    {
        switch (message.Name)
        {
            case StateEntryMessage.MessageName:
                Logger.Info("Start pickup.");
                Actor.Syncer.Send(new Message(Actor, "WorkEvent"));
                Actor.EventManager.Write(Actor.Name, "Machine worked for picking.", code: 300);
                Actor.SafetyCheck();

                _waitReadyToTransfer = true;
                break;
            case "_stop":
            case "_scan":
            case "_status":
                Scan();
                return true;
            case "PickerDown":
                _pickerDownCount++;
                if (_pickupIds.Count == _pickerDownCount)
                {
                    Actor.SetStatusByActor(ActiveSourceStage, "Transferring", true);
                    _waitTransferring = true;
                    Actor.Send(new Message(Actor, "_scan"));
                }

                break;
            case "CycleDone":
                if (_pickupIds.Contains(message.RequestId))
                {
                    _pickupIds.Remove(message.RequestId);
                    if (_pickupIds.Count == 0) _toEnd = true;
                }

                Actor.Send(new Message(Actor, "_scan"));
                break;
        }

        return false;
    }

    private void Scan()
    {
        if (_waitReadyToTransfer)
        {
            if (Actor.GetPeerStatusByActor(ActiveSourceStage, "ReadyToTransfer") is true)
            {
                _waitReadyToTransfer = false;

                _transferRow = Actor.GetPeerStatusByActor(ActiveSourceStage, "TransferRow") as int?;
                _transferCol = Actor.GetPeerStatusByActor(ActiveSourceStage, "TransferCol") as int?;

                _pickerIndices.Clear();
                var increasingDir = true;
                if (_transferCol != null)
                    if (_transferCol % 2 == 0)
                        increasingDir = false;

                var i = increasingDir ? 0 : 1;
                while (i is >= 0 and < 2)
                    try
                    {
                        if (!Actor.UsePickers.Value[i]) continue;
                        if (!Actor.Products.Value[i].Exists)
                        {
                            _pickerIndices.Add(i);
                            if (Actor.SourceStageType != StageType.Station) break;
                        }
                    }
                    finally
                    {
                        if (increasingDir) i++;
                        else i--;
                    }

                if (_pickerIndices.Count == 0)
                {
                    Actor.AllPickersHaveProductError.Show();
                    throw new SequenceError();
                }

                if (Actor.SourceStageType != StageType.Station)
                {
                    if (_pickerIndices.Count != 1) throw new FatalSequenceError();
                    var pickerIndex = _pickerIndices[0];
                    Actor.Products.Value[pickerIndex].PickupPickerIndex = pickerIndex;
                    Actor.Products.Value[pickerIndex].SourceVisionResult = Actor.GetPeerStatusByActor(
                        ActiveSourceStage, "VisionResult") as VisionResult ?? new VisionResult();

                    Actor.SetStatusByActor(Actor.SourceStages[stageIndex], "CurrentPickerIndex", pickerIndex);
                    var transferOffsetX = -Actor.VisionCenterX.Value[pickerIndex];
                    transferOffsetX += Actor.PickupOffsetX.Value[pickerIndex];
                    Logger.Info($"TransferOffsetX: {transferOffsetX}");
                    Actor.SetStatusByActor(ActiveSourceStage, "TransferOffsetX", transferOffsetX);
                }

                Actor.SetStatusByActor(ActiveSourceStage, "ReadyToTransfer", true);

                if (Actor.SourceStageType != StageType.Station
                    && (int)Actor.GetPeerStatus(ActiveSourceStage, "WorkableCellCount")! >= 2
                    && (int)Actor.GetStatus("VacantPickerCount")! >= 2
                    && Actor.GetPeerStatus(Actor.Syncer, "_auto") is true)
                {
                    Logger.Info("Have next job.");
                    _stayHere = true;
                }
                else
                {
                    Logger.Info("This is the final job.");
                }

                var pos = Actor.GetPickupPos(_pickerIndices[0], stageIndex);
                if (Actor.GetPeerStatusByActor(ActiveSourceStage, "TransferOffsetY") is double
                    transferOffsetY)
                {
                    Logger.Info($"TransferOffsetY: {transferOffsetY}");
                    pos += transferOffsetY;
                }

                var angleOffset = 0.0;
                if (Actor.SourceStageType != StageType.Station)
                {
                    if (_pickerIndices.Count != 1) throw new FatalSequenceError();
                    var pickerIndex = _pickerIndices[0];
                    Actor.SetStatusByActor(Actor.SourceStages[stageIndex], "CurrentPickerIndex", pickerIndex);
                    pos += Actor.PickupOffsetY.Value[pickerIndex];
                }
                else
                {
                    pos += Actor.PickupOffsetY.Value[0];
                }

                if (Actor.SourceStageType == StageType.Station)
                    if (Actor.GetPeerStatusByActor(ActiveSourceStage,
                            "TransferRows") is int[] transferRows)
                        _pickerIndices = transferRows.Where(row => _pickerIndices.Contains(row)).ToList();

                if (_pickerIndices.Count == 0)
                {
                    Actor.AllPickersHaveProductError.Show();
                    throw new SequenceError();
                }

                Actor.SafetyCheck();
                Actor.Y.SetSpeed(Actor.Y.GetNormalSpeed());
                Actor.Y.Move(pos, true);

                _pickupIds.Clear();
                _pickerIndices.ForEach(idx => _pickupIds.Add(Actor.Pickers[idx].Send(new Message(Actor, "Pickup",
                    new Dict
                    {
                        ["StageIndex"] = stageIndex,
                        ["AngleOffset"] = angleOffset + Actor.PickupOffsetR.Value[idx]
                    }))));
            }

            if (Actor.HasPeerFailed(ActiveSourceStage))
                throw new SequenceError();
        }

        if (_waitTransferring)
        {
            if (Actor.GetPeerStatusByActor(ActiveSourceStage, "Transferring") is true)
            {
                _waitTransferring = false;
                _pickerIndices.ForEach(idx => Actor.Pickers[idx].Send(new Message(Actor, "PickerUp")));
            }

            if (Actor.HasPeerFailed(ActiveSourceStage))
                throw new SequenceError();
        }

        if (_pickupIds.Count > 0)
            _pickerIndices.ForEach(idx =>
            {
                if (Actor.HasPeerFailed(Actor.Pickers[idx])) throw new SequenceError();
            });
        if (_toEnd)
        {
            var targetPos = Actor.Y.GetInitPos()[0];

            _toEnd = false;
            Actor.SetStatusByActor(ActiveSourceStage, "TransferredRows", _pickerIndices.ToArray());
            Actor.SetStatusByActor(ActiveSourceStage, "Transferred", true);

            var pickupPosX = Actor.GetPeerStatusByActor(ActiveSourceStage, "TransferringPosX") as double? ?? 0;
            var pickupPosY = Actor.Y.GetPosition();
            _pickerIndices.ForEach(idx =>
            {
                Actor.Products.Value[idx] = new Product
                {
                    Exists = true,
                    PickupPickerIndex = idx,
                    PickupPosX = pickupPosX,
                    PickupPosY = pickupPosY,
                    PickupRow = _transferRow ?? 0,
                    PickupCol = _transferCol ?? 0,
                    SourceVisionResult = Actor.Products.Value[idx].SourceVisionResult,
                    PickupStageIndex = stageIndex
                };

                Actor.PickupLogger?.WriteProduct(Actor.Products.Value[idx]);
            });

            if (!_stayHere)
            {
                Actor.SafetyCheck();
                Actor.Y.Move(targetPos);
            }

            if (!Actor.TryPopState())
                Actor.State = new IdleState(Actor);
            Actor.Syncer.Send(new Message(Actor, "CycleDone"));
            Logger.Info("Finished picking.");
        }
    }
}