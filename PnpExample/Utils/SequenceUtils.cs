using System.Diagnostics;
using ControlBee.Interfaces;
using ControlBeeAbstract.Devices;
using ControlBeeAbstract.Exceptions;

namespace PnpExample.Utils;

public class SequenceUtils
{
    public static void RepeatUntilUnchanged(ref int sqc, Action action)
    {
        while (true)
        {
            var before = sqc;
            action();
            var after = sqc;

            if (after.Equals(before))
                break;
        }
    }

    public static void InitializeByBuffer(bool skipWaitSensor, IAxis axis, IDialog limitSensorDetError)
    {
        if (skipWaitSensor) return;
        if (axis.GetDevice() is not IBufferDevice bufferDevice) return;
        if (axis.GetDevice() is not IMotionDevice motionDevice) return;

        var deviceChannel = axis.GetChannel();

        if (motionDevice.GetNegativeLimitSensor(deviceChannel) ||
            motionDevice.GetPositiveLimitSensor(deviceChannel))
        {
            limitSensorDetError.Show();
            throw new SequenceError();
        }

        bufferDevice.RunBuffer(deviceChannel, "Homing");

        var sw = new Stopwatch();
        sw.Start();
        while (bufferDevice.IsRunningBuffer(deviceChannel))
        {
            if (sw.ElapsedMilliseconds > 3 * 60 * 1000) throw new TimeoutError();
            Thread.Sleep(1);
        }

        var homeFlags = (int[])bufferDevice.ReadVariable("HomeFlag");
        if (homeFlags[deviceChannel] != 1)
        {
            throw new SequenceError();
        }

        axis.SetSpeed(axis.GetJogSpeed(ControlBee.Constants.JogSpeedLevel.Fast));
        axis.GetInitPos().MoveAndWait();
    }
}