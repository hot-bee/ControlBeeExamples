using System.Windows;
using System.Windows.Controls;
using ControlBee.Interfaces;
using ControlBeeWPF.Interfaces;
using ControlBeeWPF.Services;
using ControlBeeWPF.Views;
using PnpExample.Interfaces;
using Button = System.Windows.Controls.Button;
using Message = ControlBee.Models.Message;
using UserControl = System.Windows.Controls.UserControl;
using ViewFactory = PnpExample.Services.ViewFactory;

namespace PnpExample.Views;

/// <summary>
///     Interaction logic for FunctionView.xaml
/// </summary>
public partial class ManualView : UserControl, IRefreshable
{
    private readonly IActorRegistry _actorRegistry;
    private readonly List<Button> _buttons = [];


    private readonly Dictionary<Guid, Button> _propertyReads = new();
    private readonly IUiActor _uiActor;
    private readonly IViewFactory _viewFactory;
    private readonly InspectionContainerView _inspectionView;

    public ManualView(
        IActorRegistry actorRegistry,
        IViewFactory viewFactory
    )
    {
        _actorRegistry = actorRegistry;
        _viewFactory = viewFactory;
        InitializeComponent();

        _uiActor = (IUiActor)actorRegistry.Get("Ui")!;
        _uiActor.MessageArrived += UiActor_MessageArrived;

        SetupFunctions();
        JogGroup.Content = viewFactory.Create(typeof(JogView), "Head0");

        // _inspectionView = (InspectionContainerView)viewFactory.Create(typeof(InspectionContainerView), "TcpVision");
        // InspectionGroup.Content = _inspectionView;
    }

    public void Refresh()
    {
        // _inspectionView.Refresh();
    }

    private void UiActor_MessageArrived(object? sender, Message message)
    {
        switch (message.Name)
        {
            case "_property":
            {
                if (_propertyReads.TryGetValue(message.RequestId, out var button))
                {
                    if (message.DictPayload == null)
                        break;
                    var name = message.DictPayload.GetValueOrDefault("Name") as string;
                    var desc = message.DictPayload.GetValueOrDefault("Desc") as string;
                    button.Content = name;
                    if (!string.IsNullOrEmpty(desc))
                        button.ToolTip = desc;
                }

                break;
            }
            case "_status":
                if (message.Sender.Name == "Syncer")
                {
                    var ready = message.DictPayload!.GetValueOrDefault("_ready") is true;
                    // _buttons.ForEach(x => x.IsEnabled = ready);  // TODO: temp
                }

                break;
        }
    }

    private void SetupFunctions()
    {
        foreach (var actor in _actorRegistry.GetActors())
        foreach (var functionName in actor.GetFunctions())
        {
            var button = new Button
            {
                Content = functionName
            };
            button.Click += (sender, args) => { actor.Send(new Message(_uiActor, functionName)); };
            FunctionPanel.Children.Add(button);
            var reqId = actor.Send(
                new Message(_uiActor, "_propertyRead", $"/Functions/{functionName}")
            );
            _propertyReads[reqId] = button;
            _buttons.Add(button);
        }
    }
}