using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlBee.Interfaces;
using ControlBee.Models;
using Message = ControlBee.Models.Message;

namespace PnpExample.ViewModels;

public partial class FrameViewModel : ObservableObject, IDisposable
{
    private readonly IActor _syncer;
    private readonly IUiActor _uiActor;

    [ObservableProperty]
    private bool _canStart;

    [ObservableProperty]
    private bool _canStop = true;

    public FrameViewModel(IActorRegistry actorRegistry)
    {
        _syncer = actorRegistry.Get("Syncer")!;
        _uiActor = (IUiActor)actorRegistry.Get("Ui")!;
        _uiActor.MessageArrived += UiActorOnMessageArrived;
    }

    public void Dispose()
    {
        _uiActor.MessageArrived -= UiActorOnMessageArrived;
    }

    private void UiActorOnMessageArrived(object? sender, Message e)
    {
        switch (e.Name)
        {
            case "_status":
                if (e.Sender == _syncer)
                {
                    CanStart = e.DictPayload!.GetValueOrDefault("_ready") is true;
                    CanStop = e.DictPayload!.GetValueOrDefault("_stopping") is not true;
                }

                break;
        }
    }

    [RelayCommand]
    private void Start()
    {
        _syncer.Send(new Message(_uiActor, "_start"));
    }

    [RelayCommand]
    private void Stop()
    {
        _syncer.Send(new Message(_uiActor, "_stop"));
    }

    [RelayCommand]
    private void ResetError()
    {
        _syncer.Send(new Message(_uiActor, "_resetError"));
    }
}
