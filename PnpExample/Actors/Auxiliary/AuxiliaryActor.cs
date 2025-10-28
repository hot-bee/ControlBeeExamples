using ControlBee.Interfaces;
using ControlBee.Models;
using log4net;

namespace PnpExample.Actors.Auxiliary;

public class AuxiliaryActor : Actor
{
    private static readonly ILog Logger = LogManager.GetLogger("Sequence");
    private readonly Dictionary<string, string> _ioNames = [];

    public AuxiliaryActor(ActorConfig config)
        : base(config)
    {
        _ioDeviceName = config.SystemPropertiesDataSource.GetValue(Name, "IoDeviceName") as string ?? "";
        var inputCount = int.Parse(config.SystemPropertiesDataSource.GetValue(Name, "InputCount") as string ?? "0");
        var outputCount = int.Parse(config.SystemPropertiesDataSource.GetValue(Name, "OutputCount") as string ?? "0");

        for (var i = 0; i < inputCount; i++)
        {
            var key = $"Inputs/{i}";
            var name = config.SystemPropertiesDataSource.GetValue(Name, key) as string;
            if (!string.IsNullOrEmpty(name))
                _ioNames[key] = name;
        }
        for (var i = 0; i < outputCount; i++)
        {
            var key = $"Outputs/{i}";
            var name = config.SystemPropertiesDataSource.GetValue(Name, key) as string;
            if (!string.IsNullOrEmpty(name))
                _ioNames[key] = name;
        }

        var inputList = new List<IDigitalInput>();
        for (var i = 0; i < inputCount; i++)
        {
            inputList.Add(new DigitalInputPlaceholder());
        }
        Inputs = inputList.ToArray();

        var outputList = new List<IDigitalOutput>();
        for (var i = 0; i < outputCount; i++)
        {
            outputList.Add(new DigitalOutputPlaceholder());
        }
        Outputs = outputList.ToArray();

    }

    private readonly string _ioDeviceName;

    #region Input
    public IDigitalInput[] Inputs;
    #endregion

    #region Output

    public IDigitalOutput[] Outputs;
    #endregion

    public void SetPeers()
    {
        InitPeers([]);
    }

    private void UpdateItems()
    {
        for (var i = 0; i < Inputs.Length; i++)
        {
            var nameKey = $"Inputs/{i}";
            var name = _ioNames.GetValueOrDefault(nameKey);
            if (!string.IsNullOrEmpty(name)) ((IActorItemModifier)Inputs[i]).Name = name;
            (Inputs[i] as IDeviceChannelModifier)?.SetChannel(i);
            (Inputs[i] as IDeviceChannelModifier)?.SetDevice(_ioDeviceName);
        }
        for (var i = 0; i < Outputs.Length; i++)
        {
            var nameKey = $"Outputs/{i}";
            var name = _ioNames.GetValueOrDefault(nameKey);
            if (!string.IsNullOrEmpty(name)) ((IActorItemModifier)Outputs[i]).Name = name;
            (Outputs[i] as IDeviceChannelModifier)?.SetChannel(i);
            (Outputs[i] as IDeviceChannelModifier)?.SetDevice(_ioDeviceName);
            (Outputs[i] as IDeviceChannelModifier)?.Sync();
        }
    }

    public override void Start()
    {
        UpdateItems();
        base.Start();
    }
}
