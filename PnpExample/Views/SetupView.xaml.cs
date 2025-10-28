using System.Windows;
using PnpExample.Interfaces;
using ControlBee.Interfaces;
using ControlBeeWPF.Interfaces;
using ControlBeeWPF.Views;
using MathNet.Numerics.Distributions;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using UserControl = System.Windows.Controls.UserControl;
using Dict = System.Collections.Generic.Dictionary<string, object?>;

namespace PnpExample.Views;

/// <summary>
///     Interaction logic for FunctionView.xaml
/// </summary>
public partial class SetupView : UserControl, IViewChanged
{
    private readonly List<Button> _buttons = [];
    private readonly IViewFactory _viewFactory;
    private TeachingView? _currentTeachingView;
    private UserControl? _inspectionContainerView;

    public SetupView(
        IActorRegistry actorRegistry,
        IViewFactory viewFactory
    )
    {
        _viewFactory = viewFactory;
        InitializeComponent();
        UnitsPanel.Children.Clear();

        _inspectionContainerView = viewFactory.Create(typeof(InspectionContainerView), "TcpVision", new Dict
        {
            ["Mode"] = "VisionFrame",
            ["Channel"] = 0
        });
        foreach (var actorName in actorRegistry.GetActorNames())
        {
            if (actorName is "Ui")
                continue;
            var actor = actorRegistry.Get(actorName)!;
            var button = new Button { Content = actor.Title };
            var view = viewFactory.Create(typeof(TeachingView), actor.Name);
            var containerView = viewFactory.Create(typeof(EditableFrameView), view);
            if (view is IViewChanged viewChanged)
                viewChanged.ViewChanged += ViewChangedOnViewChanged;

            button.Click += (sender, args) =>
            {
                _currentTeachingView?.SetInspectionContainerView(null);
                ContentArea.Content = containerView;
                _currentTeachingView = (TeachingView)view;
                _currentTeachingView.SetInspectionContainerView(_inspectionContainerView);
                FocusButton(button);
            };
            _buttons.Add(button);
            UnitsPanel.Children.Add(button);
        }

        AddVisionButton();
        AddTestVisionButton();
        AddActorItemExplorerButton();

        if (UnitsPanel.Children.Count > 0)
            UnitsPanel.Children[0].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    }

    public event EventHandler<UserControl>? ViewChanged;

    public void FocusButton(Button focusedButton)
    {
        foreach (var button in _buttons)
            button.Background = Brushes.WhiteSmoke;
        focusedButton.Background = Brushes.LightSkyBlue;
    }

    private void ViewChangedOnViewChanged(object? sender, UserControl e)
    {
        OnViewChanged(e);
    }

    private void AddActorItemExplorerButton()
    {
        var view = _viewFactory.Create(typeof(ActorItemExplorerWholeView));
        AddButton("All parameters", view);
    }

    private void AddVisionButton()
    {
        var view = _viewFactory.Create(typeof(InspectionContainerView), "TcpVision", new Dict
        {
            ["Mode"] = "Setup",
        });
        AddButton("Vision", view);
    }
    private void AddTestVisionButton()
    {
        var view = _viewFactory.Create(typeof(VisionStatusView), "TcpVision");
        AddButton("Test Vision", view);
    }

    private void AddButton(string buttonContent, UserControl contentView)
    {
        var button = new Button { Content = buttonContent };
        button.Click += (sender, args) =>
        {
            ContentArea.Content = contentView;
            FocusButton(button);
        };
        _buttons.Add(button);
        UnitsPanel.Children.Add(button);
    }


    protected virtual void OnViewChanged(UserControl e)
    {
        ViewChanged?.Invoke(this, e);
    }
}