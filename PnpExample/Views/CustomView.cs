using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using ControlBee.Interfaces;
using ControlBee.Models;
using Button = System.Windows.Controls.Button;
using Message = ControlBee.Models.Message;
using UserControl = System.Windows.Controls.UserControl;

namespace PnpExample.Views;

public class CustomView : UserControl, INotifyPropertyChanged
{
    private readonly IActor _syncer;
    private readonly IUiActor _uiActor;
    private bool _ready;
    private bool _step;
    protected List<Button> FunctionButtons = [];

    public CustomView(IActorRegistry actorRegistry)
    {
        _syncer = actorRegistry.Get("Syncer")!;
        _uiActor = (IUiActor)actorRegistry.Get("Ui")!;
        _uiActor.MessageArrived += UiActorOnMessageArrived;
        PropertyChanged += OnPropertyChanged;
        UpdateUi();
    }

    public bool Ready
    {
        get => _ready;
        set => SetField(ref _ready, value);
    }

    public bool Step
    {
        get => _step;
        set => SetField(ref _step, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateUi();
    }

    private void UiActorOnMessageArrived(object? sender, Message e)
    {
        switch (e.Name)
        {
            case "_status":
                if (e.Sender == _syncer)
                {
                    Step = e.DictPayload!.GetValueOrDefault("_step") is true;
                    Ready = e.DictPayload!.GetValueOrDefault("_ready") is true;
                }

                break;
        }
    }

    private void UpdateUi()
    {
        FunctionButtons.ForEach(x =>
        {
            if (!Ready && Step && "_step".Equals(x.Tag))
                x.IsEnabled = true;
            else
                x.IsEnabled = Ready;
        });
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
