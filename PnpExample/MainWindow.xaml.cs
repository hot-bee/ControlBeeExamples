using PnpExample.Views;
using ControlBee.Interfaces;
using ControlBee.Models;

namespace PnpExample;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    private readonly DialogDisplay _dialogDisplay;
    private readonly IActorRegistry _actorRegistry;
    private readonly IDeviceMonitor _deviceMonitor;

    public MainWindow(FrameView frameView, DialogDisplay dialogDisplay, IActorRegistry actorRegistry, IDeviceMonitor deviceMonitor)
    {
        _dialogDisplay = dialogDisplay;
        _actorRegistry = actorRegistry;
        _deviceMonitor = deviceMonitor;
        InitializeComponent();
        Grid1.Children.Add(frameView);
        Closed += OnClosed;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _actorRegistry.Dispose();
        _deviceMonitor.Dispose();
    }
}
