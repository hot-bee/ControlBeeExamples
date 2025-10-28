using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ControlBee.Interfaces;
using ControlBeeWPF.Interfaces;
using ControlBeeWPF.ViewModels;
using ControlBeeWPF.Views;
using Brushes = System.Windows.Media.Brushes;
using UserControl = System.Windows.Controls.UserControl;

namespace PnpExample.Views;

/// <summary>
///     Interaction logic for HeaderView.xaml
/// </summary>
public partial class HeaderView : UserControl
{
    private readonly DispatcherTimer _dispatcherTimer;
    private readonly IViewFactory _viewFactory;
    private readonly VisionStatusViewModel _visionStatusViewModel;
    private readonly RecipeManagerViewModel _recipeManagerViewModel;

    public HeaderView(
        IViewFactory viewFactory,
        IDeviceManager deviceManager,
        ISystemConfigurations systemConfigurations,
        ActorMonitorViewModel actorMonitorViewModel,
        RecipeManagerViewModel recipeManagerViewModel
    )
    {
        _viewFactory = viewFactory;
        _recipeManagerViewModel = recipeManagerViewModel;
        InitializeComponent();

        MachineStatus.Content = actorMonitorViewModel.ActorStatus.GetValueOrDefault("Syncer");
        actorMonitorViewModel.PropertyChanged += (sender, args) =>
        {
            MachineStatus.Content = actorMonitorViewModel.ActorStatus.GetValueOrDefault("Syncer");
        };

        _visionStatusViewModel = new VisionStatusViewModel("TcpVision", deviceManager);
        _visionStatusViewModel.PropertyChanged += (sender, args) => UpdateUi();
        UpdateUi();

        _dispatcherTimer = new DispatcherTimer();
        _dispatcherTimer.Tick += DispatcherTimerOnTick;
        _dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
        _dispatcherTimer.Start();
        UpdateDateTime();

        VersionLabel.Content = systemConfigurations.Version;

        UpdateRecipeLabel();
        recipeManagerViewModel.PropertyChanged += (sender, args) => UpdateRecipeLabel();
    }

    private void UpdateRecipeLabel()
    {
        var recipeName = _recipeManagerViewModel.LocalName;
        RecipeNameLabel.Content = recipeName;
    }

    public void UpdateUi()
    {
        VisionStatusLabel.Content = _visionStatusViewModel.IsConnected
            ? "Connected"
            : "Disconnected";
        VisionStatusBorder.Background = _visionStatusViewModel.IsConnected
            ? Brushes.LightGreen
            : Brushes.PaleVioletRed;
        VisionStatusLabel.Foreground = _visionStatusViewModel.IsConnected
            ? Brushes.Black
            : Brushes.White;
    }

    private void DispatcherTimerOnTick(object? sender, EventArgs e)
    {
        UpdateDateTime();
    }

    private void UpdateDateTime()
    {
        DateLabel.Content = DateTime.Now.ToString("yyyy-MM-dd");
        TimeLabel.Content = DateTime.Now.ToString("HH:mm:ss");
    }

    private void OpenActorMonitor()
    {
        var view = _viewFactory.Create(typeof(ActorMonitorView));
        var actorMonitorView = new Window
        {
            Title = "Actor Monitor",
            Content = view,
            SizeToContent = SizeToContent.WidthAndHeight,
            Left = 100,
            Top = 100,
        };
        actorMonitorView.Show();
    }

    private void VisionStatusBorder_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_visionStatusViewModel.IsConnected)
            _visionStatusViewModel.ConnectCommand.Execute(null);
    }

    private void ClockCell_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        OpenActorMonitor();
    }
}
