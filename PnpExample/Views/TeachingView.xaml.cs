using ControlBee.Interfaces;
using ControlBeeWPF.Services;
using ControlBeeWPF.ViewModels;
using ControlBeeWPF.Views;
using log4net;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using PnpExample.Interfaces;
using ControlBeeWPF.Interfaces;
using Button = System.Windows.Controls.Button;
using Message = ControlBee.Models.Message;
using UserControl = System.Windows.Controls.UserControl;

namespace PnpExample.Views;

/// <summary>
///     Interaction logic for TeachingView.xaml
/// </summary>
public partial class TeachingView : UserControl, IDisposable
{
    private static readonly ILog Logger = LogManager.GetLogger(
        MethodBase.GetCurrentMethod()!.DeclaringType!
    );

    private readonly IActor _actor;

    private readonly string _actorName;
    private readonly IActorRegistry _actorRegistry;
    private readonly IViewFactory _viewFactory;

    private readonly Dictionary<(string itemPath, object[] location), TeachingDataView> _dataViews = [];
    private readonly List<Button> _functionButtons = [];
    private readonly Dictionary<Guid, Button> _propertyReads = new();
    private readonly TeachingViewFactory _teachingViewFactory;
    private readonly IUiActor _uiActor;
    private readonly TeachingViewModel _viewModel;

    public TeachingView(
        string actorName,
        TeachingViewModel viewModel,
        TeachingViewFactory teachingViewFactory,
        IActorRegistry actorRegistry,
        IViewFactory viewFactory
    )
    {
        _actorName = actorName;
        _actorRegistry = actorRegistry;
        _viewFactory = viewFactory;
        _actor = actorRegistry.Get(actorName)!;
        _uiActor = (IUiActor)actorRegistry.Get("Ui")!;
        _teachingViewFactory = teachingViewFactory;
        InitializeComponent();
        DataContext = viewModel;
        _viewModel = viewModel;
        _viewModel.Loaded += ViewModelOnLoaded;
        _uiActor = (IUiActor)actorRegistry.Get("Ui")!;
        _uiActor.MessageArrived += UiActor_MessageArrived;

        SetupUi();
    }

    public void Dispose()
    {
        _viewModel.Loaded -= ViewModelOnLoaded;
        foreach (var view in _dataViews.Values)
            view.Dispose();
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
                    var textBlock = (TextBlock)button.Content;
                    textBlock.Text = name;
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

    private void SetupUi()
    {
        AxisStatusContent.Content = _viewFactory.Create(typeof(TeachingAxisStatusView), _actorName);

        foreach (var (itemPath, type) in _actor.GetItems())
            if (type.IsAssignableTo(typeof(IDigitalInput)))
                InputsPanel.Children.Add(
                    _viewFactory.Create(typeof(DigitalInputStatusBarView), _actorName, itemPath)
                );
            else if (type.IsAssignableTo(typeof(IDigitalOutput)))
                OutputsPanel.Children.Add(
                    _viewFactory.Create(typeof(DigitalOutputStatusBarView), _actorName, itemPath)
                );
            else if (type.IsAssignableTo(typeof(IBinaryActuator)))
                BinaryActuatorsPanel.Children.Add(
                    new DoubleActingActuatorStatusBarView(_actorRegistry, _actorName, itemPath)
                );

        foreach (var functionName in _actor.GetFunctions())
        {
            var textBlock = new TextBlock { Text = functionName, TextWrapping = TextWrapping.Wrap };
            var button = new Button
            {
                Content = textBlock,
                Width = 100,
                Height = 40,
                Margin = new Thickness(5)
            };
            button.Click += (sender, args) => { _actor.Send(new Message(_uiActor, functionName)); };
            FunctionPanel.Children.Add(button);
            var reqId = _actor.Send(
                new Message(_uiActor, "_propertyRead", $"/Functions/{functionName}")
            );
            _propertyReads[reqId] = button;
            _functionButtons.Add(button);
        }
    }


    private void ViewModelOnLoaded(object? sender, EventArgs e)
    {
        foreach (var (itemPath, location) in _viewModel.PositionItemPaths)
        {
            var name = _viewModel.ItemNames[itemPath];
            if (location.Length > 0) name = $"{name}({location[0]})";
            PositionItemList.Items.Add(name);
        }

        if (PositionItemList.Items.Count > 0)
            PositionItemList.SelectedIndex = 0;
    }

    private void PositionItemList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            var itemPath = _viewModel.PositionItemPaths[PositionItemList.SelectedIndex].itemPath;
            var location = _viewModel.PositionItemPaths[PositionItemList.SelectedIndex].location;
            var key = (itemPath, location);
            if (!_dataViews.TryGetValue(key, out var view))
            {
                view = _teachingViewFactory.CreateData(_actorName, itemPath, location);
                _dataViews[key] = view;
            }

            DataContent.Content = view;
            JogContent.Content = _viewFactory.Create(typeof(TeachingJogView), _actorName, itemPath, location); // TODO: memory leak
        }
        catch (ArgumentOutOfRangeException exception)
        {
            Logger.Error("Selection index is out of range.", exception);
        }
    }

    public void SetInspectionContainerView(UserControl? child)
    {
        CameraGroup.Content = child;
    }
}