using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlBee.Constants;
using ControlBee.Interfaces;
using ControlBee.Models;
using Message = ControlBee.Models.Message;

namespace PnpExample.ViewModels;

public partial class InitializationViewModel : ObservableObject, IDisposable
{
    private readonly IActorRegistry _actorRegistry;
    private readonly Dictionary<string, bool> _isInitializationChecked = new();
    private readonly IUiActor _ui;
    public Dictionary<string, InitializationStatus> InitializationStatus = new();

    public InitializationViewModel(IActorRegistry actorRegistry)
    {
        _actorRegistry = actorRegistry;
        _ui = (IUiActor)actorRegistry.Get("Ui")!;
        foreach (var (actorName, _) in GetActorTitles())
            SetInitialization(actorName, false);
        _ui.MessageArrived += UiOnMessageArrived;
    }

    public bool InitializationAll { get; private set; }

    public void Dispose()
    {
        _ui.MessageArrived -= UiOnMessageArrived;
    }

    public (string actorName, string actorTitle)[] GetActorTitles()
    {
        var actorNames = _actorRegistry.GetActorNames();
        var actorTitles = actorNames
            .ToList()
            .ConvertAll(actorName => (actorName, _actorRegistry.Get(actorName).Title));
        actorTitles.RemoveAll(x =>
            x.actorName
                is "Ui"
                    or "Auxiliary"
                    or "Syncer"
        ); // TODO: Any better way than this?
        return actorTitles.ToArray();
    }

    private void UiOnMessageArrived(object? sender, Message e)
    {
        if (e.Name == "_status")
        {
            if (e.ActorName != "Syncer")
                return;
            var status = e.DictPayload!;

            var actorNames = _actorRegistry.GetActorNames();
            var receivedActorInitializationStatus =
                (Dictionary<string, object>)status["ActorInitializationStatus"]!;
            foreach (var actorName in actorNames)
            {
                if (!receivedActorInitializationStatus.TryGetValue(actorName, out var statusValue))
                    continue;
                var enumValue = Enum.Parse<InitializationStatus>(statusValue.ToString()!);
                InitializationStatus[actorName] = enumValue;
            }
            InitializationAll = (bool)status["AllInitialized"]!;
            OnPropertyChanged(nameof(InitializationStatus));
        }
    }

    [RelayCommand]
    private void Initialize()
    {
        var syncer = _actorRegistry.Get("Syncer")!;
        var initializingActors = new List<IActor>();
        foreach (var (actorName, check) in _isInitializationChecked)
        {
            if (!check)
                continue;
            var initializingActor = _actorRegistry.Get(actorName)!;
            initializingActors.Add(initializingActor);
        }
        syncer.Send(new Message(_ui, "_initialize", initializingActors));
    }

    public void SetInitialization(string actorName, bool check)
    {
        _isInitializationChecked[actorName] = check;
    }
}
