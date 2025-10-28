using PnpExample.Constants;
using PnpExample.Models;
using ControlBee.Models;
using ControlBee.Utils;
using ControlBeeAbstract.Exceptions;
using log4net;
using Message = ControlBee.Models.Message;


namespace PnpExample.Actors.Stage;

/*
 * 1. Set ReadyToTransfer
 * 2. Wait ReadyToTransfer or AbortTransfer
 * 3. Wait Transferring (Don't care)
 * 4. Set Transferring
 * 5. Wait Transferred

 */
public class TransferringDieState(StageActor actor) : State<StageActor>(actor)
{
    private static readonly ILog Logger = LogManager.GetLogger("Sequence");

    private bool _toReadyToTransfer;
    private int _transferCol;
    private int _transferRow;
    private bool _waitReadyToTransfer;
    private bool _waitTransferred;
    private double _workPosX;

    public override void Dispose()
    {
        base.Dispose();
        Actor.SetStatusByActor(Actor.Head, "TransferOffsetDeg", null);
        Actor.SetStatusByActor(Actor.Head, "TransferOffsetY", null);
        Actor.SetStatusByActor(Actor.Head, "TransferRow", null);
        Actor.SetStatusByActor(Actor.Head, "TransferCol", null);
        Actor.SetStatusByActor(Actor.Head, "VisionResult", null);
        Actor.SetStatusByActor(Actor.Head, "ReadyToTransfer", false);
        Actor.SetStatusByActor(Actor.Head, "Transferring", false);
        Actor.SetStatusByActor(Actor.Head, "TransferringPosX", null);
    }

    public override bool ProcessMessage(Message message)
    {
        switch (message.Name)
        {
            case StateEntryMessage.MessageName:
                Logger.Info("Start transfer die.");
                if (!Actor.Tray.Value.Exists)
                {
                    Actor.NotHaveTrayError.Show();
                    throw new SequenceError();
                }

                Actor.SoftDownZ();
                _toReadyToTransfer = true;
                break;
            case "_scan":
            case "_status":
                Scan();
                return true;
            case "CycleDone":
                Actor.Send(new Message(Actor, "_scan"));
                break;
        }

        return false;
    }

    private void Scan()
    {
        if (_toReadyToTransfer)
        {
            _toReadyToTransfer = false;

            var (count, row, col) = Actor.GetWorkableCell();
            if (count == 0)
            {
                Actor.TrayWorkDoneError.Show();
                throw new SequenceError();
            }

            Actor.CheckSafety(col);

            _transferRow = row;
            _transferCol = col;
            Actor.PositionIndex.Value = _transferCol;

            Actor.X.SetSpeed(Actor.X.GetNormalSpeed());
            var x = Actor.GetColPosition(_transferCol);
            _workPosX = x;
            Actor.X.Move(_workPosX, true);
            if (Math.Abs(_workPosX - Actor.X.GetPosition()) > Actor.FarRange.Value)
                Actor.X.WaitNear(_workPosX, StageActor.NearRange);

            using var _ = new StatusGroup(Actor);
            Actor.SetStatusByActor(Actor.Head, "TransferRow", _transferRow);
            Actor.SetStatusByActor(Actor.Head, "TransferCol", _transferCol);
            Actor.SetStatusByActor(Actor.Head, "ReadyToTransfer", true);

            _waitReadyToTransfer = true;
        }

        if (_waitReadyToTransfer)
        {
            if (Actor.GetPeerStatusByActor(Actor.Head, "AbortTransfer") is true)
            {
                Logger.Info("Aborting transfer by head request.");
                Actor.X.Wait();
                if (!Actor.TryPopState())
                    Actor.State = new IdleState(Actor);
                Actor.Syncer.Send(new Message(Actor, "CycleDone"));
                return;
            }

            if (Actor.GetPeerStatusByActor(Actor.Head, "ReadyToTransfer") is true)
            {
                _waitReadyToTransfer = false;
                if (Actor.GetPeerStatusByActor(Actor.Head, "TransferOffsetX") is double transferOffsetX)
                {
                    Logger.Info($"TransferOffsetX: {transferOffsetX}");
                    _workPosX += -transferOffsetX;
                    Actor.X.Move(_workPosX, true);
                }

                if (Actor.UseStableWait.Value)
                    Actor.X.Wait();

                using var _ = new StatusGroup(Actor);
                Actor.SetStatusByActor(Actor.Head, "TransferringPosX", _workPosX);
                Actor.SetStatusByActor(Actor.Head, "Transferring", true); // Don't care
                _waitTransferred = true;
            }

            if (Actor.HasPeerFailed(Actor.Head)) throw new SequenceError();
        }

        if (_waitTransferred)
        {
            if (Actor.GetPeerStatusByActor(Actor.Head, "Transferred") is true)
            {
                _waitTransferred = false;
                var newValue = Actor.TransferDirection == TransferDirection.TransferIn;
                Actor.Tray.Value.CellExists[_transferRow, _transferCol] = newValue;
                var pickerIndex = (int)Actor.GetPeerStatusByActor(Actor.Head, "CurrentPickerIndex")!;

                Actor.Tray.Value.Consumers[_transferRow, _transferCol] = pickerIndex;
                if (!Actor.TryPopState())
                    Actor.State = new IdleState(Actor);
                Actor.Syncer.Send(new Message(Actor, "CycleDone"));
            }

            if (Actor.HasPeerFailed(Actor.Head)) throw new SequenceError();
        }
    }
}