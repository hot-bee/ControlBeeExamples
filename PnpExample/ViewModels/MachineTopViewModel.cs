using PnpExample.Constants;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlBee.Interfaces;
using ControlBee.Models;
using ControlBee.Services;
using System.Reflection;
using System.Reflection.Metadata;
using YamlDotNet.Core;
using YamlDotNet.Core.Tokens;
using Message = ControlBee.Models.Message;

namespace PnpExample.ViewModels;

public partial class MachineTopViewModel : ObservableObject, IDisposable
{
    private readonly IActor _syncer;
    private readonly IActor _inConveyor;
    private readonly IActor _outConveyor;
    private readonly IUiActor _uiActor;

    private readonly ActorItemBinder _machineReadyInBinder;
    private readonly ActorItemBinder _machineReadyOutBinder;
    private readonly ActorItemBinder _boardAvailableReadyInBinder;
    private readonly ActorItemBinder _boardAvailableReadyOutBinder;

    [ObservableProperty] private double _machineReadyInRectOpacity = 0.3;
    [ObservableProperty] private double _machineReadyOutRectOpacity = 0.3;
    [ObservableProperty] private double _boardAvailableInRectOpacity = 0.3;
    [ObservableProperty] private double _boardAvailableOutRectOpacity = 0.3;

    public MachineTopViewModel(IActorRegistry actorRegistry, ISystemConfigurations systemConfigurations)
    {
        _syncer = actorRegistry.Get("Syncer")!;
        _inConveyor = actorRegistry.Get("InConveyor")!;
        _outConveyor = actorRegistry.Get("OutConveyor")!;
        _uiActor = (IUiActor)actorRegistry.Get("Ui")!;

        _machineReadyOutBinder = new ActorItemBinder(actorRegistry, "InConveyor", "/MachineReadyOut");
        _boardAvailableReadyInBinder = new ActorItemBinder(actorRegistry, "InConveyor", "/BoardAvailableIn");
        _boardAvailableReadyOutBinder = new ActorItemBinder(actorRegistry, "OutConveyor", "/BoardAvailableOut");
        _machineReadyInBinder = new ActorItemBinder(actorRegistry, "OutConveyor", "/MachineReadyIn");

        _machineReadyInBinder.DataChanged += MachineReadyInBinder_DataChanged;
        _machineReadyOutBinder.DataChanged += MachineReadyOutBinder_DataChanged;
        _boardAvailableReadyInBinder.DataChanged += BoardAvailableReadyInBinder_DataChanged;
        _boardAvailableReadyOutBinder.DataChanged += BoardAvailableReadyOutBinder_DataChanged;
    }

    public void Dispose()
    {
        _machineReadyInBinder.DataChanged -= MachineReadyInBinder_DataChanged;
        _machineReadyOutBinder.DataChanged -= MachineReadyOutBinder_DataChanged;
        _boardAvailableReadyInBinder.DataChanged -= BoardAvailableReadyInBinder_DataChanged;
        _boardAvailableReadyOutBinder.DataChanged -= BoardAvailableReadyOutBinder_DataChanged;
        _machineReadyInBinder.Dispose();
        _machineReadyOutBinder.Dispose();
        _boardAvailableReadyInBinder.Dispose();
        _boardAvailableReadyOutBinder.Dispose();
    }

    private void MachineReadyInBinder_DataChanged(object? sender, Dictionary<string, object?> e)
    {
        MachineReadyInRectOpacity = e["IsOn"] is true ? 1 : 0.3;
    }

    private void MachineReadyOutBinder_DataChanged(object? sender, Dictionary<string, object?> e)
    {
        MachineReadyOutRectOpacity = e["IsOn"] is true ? 1 : 0.3;
    }

    private void BoardAvailableReadyInBinder_DataChanged(object? sender, Dictionary<string, object?> e)
    {
        BoardAvailableInRectOpacity = e["IsOn"] is true ? 1 : 0.3;
    }

    private void BoardAvailableReadyOutBinder_DataChanged(object? sender, Dictionary<string, object?> e)
    {
        BoardAvailableOutRectOpacity = e["IsOn"] is true ? 1 : 0.3;
    }

    [RelayCommand]
    private void LoadOutside()
    {
        _inConveyor.Send(new Message(_uiActor, "Load"));
    }

    [RelayCommand]
    private void UnloadOutside()
    {
        _outConveyor.Send(new Message(_uiActor, "Unload"));
    }

    [RelayCommand]
    private void LoadInside()
    {
        _syncer.Send(new Message(_uiActor, "LoadToTargetStage"));
    }

    [RelayCommand]
    private void UnloadInside()
    {
        _syncer.Send(new Message(_uiActor, "UnloadToOutConveyor"));
    }
}