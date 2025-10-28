using ControlBee.Models;
using ControlBeeAbstract.Exceptions;
using log4net;
using PnpExample.Constants;
using PnpExample.Models;
using Dict = System.Collections.Generic.Dictionary<string, object?>;
using Message = ControlBee.Models.Message;

namespace PnpExample.Actors.Head;

/*
 * 1. Wait ReadyToTransfer
 * 2. Set ReadyToTransfer or AbortTransfer
 * 3. Set Transferring
 * 4. Wait Transferring
 * 5. Set Transferred
 */
public class PlacingState(HeadActor actor) : State<HeadActor>(actor)
{
    private static readonly ILog Logger = LogManager.GetLogger("Sequence");
    private readonly List<Guid> _placeIds = [];
    private double _angleOffset;
    private int _pickerDownCount;
    private List<int> _pickerIndices = [];
    private int _singlePickerIndex = -1;
    private bool _stayHere;
    private bool _toEnd;
    private bool _waitReadyToTransfer;
    private bool _waitTransferring;
    private double _yOffset;

    public override void Dispose()
    {
        base.Dispose();
        using var _ = new StatusGroup(Actor);
        Actor.SetStatusByActor(Actor.TargetStage, "TransferredRows", null);
        Actor.SetStatusByActor(Actor.TargetStage, "Transferred", false);
        Actor.SetStatusByActor(Actor.TargetStage, "OutOfZone", false);
        Actor.SetStatusByActor(Actor.TargetStage, "StayHere", false);
        Actor.SetStatusByActor(Actor.TargetStage, "TransferOffsetX", null);
        Actor.SetStatusByActor(Actor.TargetStage, "ReadyToTransfer", false);
        Actor.SetStatusByActor(Actor.TargetStage, "Transferring", false);
        Actor.SetStatusByActor(Actor.TargetStage, "AbortTransfer", false);
        Actor.SetStatusByActor(Actor.TargetStage, "CurrentPickerIndex", -1);
    }

    public override bool ProcessMessage(Message message)
    {
        switch (message.Name)
        {
            case StateEntryMessage.MessageName:
                Logger.Info("Start place.");
                Actor.Syncer.Send(new Message(Actor, "WorkEvent"));
                Actor.EventManager.Write(Actor.Name, "Machine worked for placing.", code: 300);
                Actor.SafetyCheck();

                _waitReadyToTransfer = true;
                Actor.Send(new Message(Actor, "_scan"));
                break;
            case "_stop":
            case "_scan":
            case "_status":
                Scan();
                return true;
            case "PickerDown":
                _pickerDownCount++;
                if (_placeIds.Count == _pickerDownCount)
                {
                    Actor.SetStatusByActor(Actor.TargetStage, "Transferring", true);
                    _waitTransferring = true;
                    Actor.Send(new Message(Actor, "_scan"));
                }

                break;
            case "CycleDone":
                if (_placeIds.Contains(message.RequestId))
                {
                    _placeIds.Remove(message.RequestId);
                    if (_placeIds.Count == 0) _toEnd = true;
                }

                Actor.Send(new Message(Actor, "_scan"));
                break;
            case "ClearArea":
                message.Sender.Send(new Message(message, Actor, "CycleDone"));
                break;
        }

        return false;
    }

    private void Scan()
    {
        if (_waitReadyToTransfer)
        {
            if (Actor.GetPeerStatusByActor(Actor.TargetStage, "ReadyToTransfer") is true)
            {
                _waitReadyToTransfer = false;

                _pickerIndices.Clear();
                var increasingDir = true;
                if (Actor.GetPeerStatusByActor(Actor.TargetStage, "TransferCol") is int transferCol)
                    if (transferCol % 2 == 0)
                        increasingDir = false;

                var i = increasingDir ? 0 : 1;
                while (i is >= 0 and < 2)
                    try
                    {
                        if (!Actor.UsePickers.Value[i]) continue;
                        if (Actor.Products.Value[i].Exists)
                        {
                            _pickerIndices.Add(i);
                            if (Actor.TargetStageType != StageType.Station) break;
                        }
                    }
                    finally
                    {
                        if (increasingDir) i++;
                        else i--;
                    }

                if (_pickerIndices.Count == 0)
                {
                    Actor.NoPickerHasProductError.Show();
                    throw new SequenceError();
                }

                var pos = Actor.GetPlacePos(_pickerIndices[0]);
                if (Actor.GetPeerStatusByActor(Actor.TargetStage, "TransferOffsetY") is double transferOffsetY)
                {
                    Logger.Info($"TransferOffsetY: {transferOffsetY}");
                    pos += transferOffsetY;
                }

                if (Actor.GetPeerStatusByActor(Actor.TargetStage, "TransferOffsetDeg") is double transferOffsetDeg)
                {
                    Logger.Info($"TransferOffsetDeg: {transferOffsetDeg}");
                    _angleOffset += transferOffsetDeg;
                }

                if (Actor.TargetStageType != StageType.Station)
                {
                    if (_pickerIndices.Count != 1) throw new FatalSequenceError();
                    var pickerIndex = _pickerIndices[0];
                    pos += Actor.PlaceOffsetY.Value[pickerIndex];
                }
                else
                {
                    pos += Actor.PlaceOffsetY.Value[0];
                }

                Actor.SafetyCheck();
                Actor.Y.SetSpeed(Actor.Y.GetNormalSpeed());
                Actor.Y.Move(pos, true);

                if (Actor.TargetStageType != StageType.Station)
                {
                    if (_pickerIndices.Count != 1) throw new FatalSequenceError();
                    Actor.WaitingInspectTasks();
                    _singlePickerIndex = _pickerIndices[0];
                    var transferOffsetX = 0.0;

                    Actor.Products.Value[_singlePickerIndex].PlaceCol =
                        Actor.GetPeerStatusByActor(Actor.TargetStage, "TransferCol") as int? ?? -1;
                    Actor.Products.Value[_singlePickerIndex].PlaceRow =
                        Actor.GetPeerStatusByActor(Actor.TargetStage, "TransferRow") as int? ?? -1;
                    Actor.Products.Value[_singlePickerIndex].TargetVisionResult = Actor.GetPeerStatusByActor(
                        Actor.TargetStage, "VisionResult") as VisionResult ?? new VisionResult();

                    Actor.SetStatusByActor(Actor.TargetStage, "CurrentPickerIndex", _singlePickerIndex);
                    transferOffsetX += Actor.PlaceOffsetX.Value[_singlePickerIndex];
                    Logger.Info($"TransferOffsetX: {transferOffsetX}");
                    Actor.SetStatusByActor(Actor.TargetStage, "TransferOffsetX", transferOffsetX);
                }

                Actor.SetStatusByActor(Actor.TargetStage, "ReadyToTransfer", true);

                var occupiedPickerCount = (int)Actor.GetStatus("OccupiedPickerCount")!;
                var isAuto = Actor.GetPeerStatus(Actor.Syncer, "_auto") is true;
                if (Actor.TargetStageType != StageType.Station
                    && (int)Actor.GetPeerStatus(Actor.TargetStage, "WorkableCellCount")! >= 2
                    && occupiedPickerCount >= 2
                    && isAuto)
                {
                    Logger.Info("Have next job.");
                    _stayHere = true;
                }
                else
                {
                    Logger.Info("This is the final job.");
                }

                if (Actor.TargetStageType == StageType.Station)
                    if (Actor.GetPeerStatusByActor(Actor.TargetStage, "TransferRows") is int[] transferRows)
                        _pickerIndices = transferRows.Where(row => _pickerIndices.Contains(row)).ToList();

                if (_pickerIndices.Count == 0)
                {
                    Actor.NoPickerHasProductError.Show();
                    throw new SequenceError();
                }

                Actor.SafetyCheck();
                pos += _yOffset;
                Actor.Y.SetSpeed(Actor.Y.GetNormalSpeed());
                Actor.Y.Move(pos, true);
                _pickerIndices.ForEach(idx => Actor.Pickers[idx].Send(new Message(Actor, "MoveR", "Place")));
                Actor.Y.WaitNear(pos, Actor.NearRange.Value);

                _placeIds.Clear();
                _pickerIndices.ForEach(idx =>
                {
                    var angleOffset = _angleOffset + Actor.PlaceOffsetR.Value[idx];
                    Logger.Info($"Place angle compensation: guid, {Actor.Products.Value[idx].Guid}, {angleOffset}");
                    _placeIds.Add(Actor.Pickers[idx].Send(new Message(Actor, "Place", new Dict
                    {
                        ["AngleOffset"] = angleOffset
                    })));
                });
                if (_pickerIndices.Count == 0)
                {
                    Actor.SetStatusByActor(Actor.TargetStage, "Transferring", true);
                    _waitTransferring = true;
                }
            }

            if (Actor.HasPeerFailed(Actor.TargetStage)) throw new SequenceError();
        }

        if (_waitTransferring)
        {
            if (Actor.GetPeerStatusByActor(Actor.TargetStage, "Transferring") is true)
            {
                _waitTransferring = false;

                if (_singlePickerIndex != -1)
                {
                    var placedProduct = Actor.Products.Value[_singlePickerIndex];
                    placedProduct.PlacePosX =
                        Actor.GetPeerStatusByActor(Actor.TargetStage, "TransferringPosX") as double? ?? -1;
                    placedProduct.PlacePosY = Actor.Y.GetPosition();
                    placedProduct.PlacePickerIndex = _singlePickerIndex;
                }

                _pickerIndices.ForEach(idx => Actor.Pickers[idx].Send(new Message(Actor, "PickerUp")));
                if (_pickerIndices.Count == 0) _toEnd = true;
            }

            if (Actor.HasPeerFailed(Actor.TargetStage)) throw new SequenceError();
        }

        if (_placeIds.Count > 0)
            _pickerIndices.ForEach(idx =>
            {
                if (Actor.HasPeerFailed(Actor.Pickers[idx])) throw new SequenceError();
            });

        if (_toEnd)
        {
            _toEnd = false;

            Actor.SetStatusByActor(Actor.TargetStage, "TransferredRows", _pickerIndices.ToArray());
            Actor.SetStatusByActor(Actor.TargetStage, "Transferred", true);

            Actor.PlaceLogger?.WriteProduct(Actor.Products.Value[_singlePickerIndex]);

            _pickerIndices.ForEach(idx => { Actor.Products.Value[idx] = new Product { Exists = false }; });

            if (!_stayHere) Actor.Y.GetInitPos().Move();

            if (!Actor.TryPopState())
                Actor.State = new IdleState(Actor);

            Actor.Syncer.Send(new Message(Actor, "CycleDone"));

            Logger.Info("Finished placing.");
        }
    }
}