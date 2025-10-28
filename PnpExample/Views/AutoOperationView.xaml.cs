using PnpExample.Constants;
using PnpExample.Interfaces;
using PnpExample.Models;
using PnpExample.Views;
using ControlBee.Interfaces;
using ControlBeeWPF.Interfaces;
using ControlBeeWPF.Views;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using AutoOperationViewModel = PnpExample.ViewModels.AutoOperationViewModel;
using Brushes = System.Windows.Media.Brushes;
using Dict = System.Collections.Generic.Dictionary<string, object?>;
using GroupBox = System.Windows.Controls.GroupBox;
using Message = ControlBee.Models.Message;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using UserControl = System.Windows.Controls.UserControl;

namespace PnpExample.Views;

/// <summary>
///     Interaction logic for AutoOperationView.xaml
/// </summary>
public partial class AutoOperationView : UserControl, IRefreshable
{
    private readonly IActorRegistry _actorRegistry;
    private readonly IActor _headActor;
    private readonly IActor _syncerActor;
    private readonly IActor _uiActor;
    private readonly bool _useBallFeeder;
    private readonly bool _useDualSourceStage;
    private readonly bool _useMagazineLoader;
    private readonly IViewFactory _viewFactory;
    private readonly AutoOperationViewModel _viewModel;
    private readonly InspectionContainerView _inspectionContainerView;

    public AutoOperationView(
        AutoOperationViewModel viewModel,
        IViewFactory viewFactory,
        IActorRegistry actorRegistry,
        ISystemConfigurations systemConfigurations
    )
    {
        _viewModel = viewModel;
        _viewFactory = viewFactory;
        _actorRegistry = actorRegistry;
        _inspectionContainerView = (InspectionContainerView)viewFactory.Create(typeof(InspectionContainerView), "TcpVision", new Dict
        {
            ["Mode"] = "VisionFrame",
            ["Channel"] = 0
        });
        _uiActor = actorRegistry.Get("Ui")!;
        _syncerActor = actorRegistry.Get("Syncer")!;
        _headActor = actorRegistry.Get("Head")!;
        InitializeComponent();
        DataContext = viewModel;
        SetupUi();
        viewModel.PropertyChanged += ViewModelOnPropertyChanged;
    }

    public void Refresh()
    {
    }

    public event EventHandler<UserControl>? ViewChanged;

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
    }

    private void SetupUi()
    {
        TimeInfoPanel.Children.Add(new GroupBox
        {
            Header = "Time Table",
            Content = new StackPanel
            {
                Children =
                {
                    _viewFactory.Create(typeof(VariableStatusBarView), "Syncer", "/IdleTime", (object[])["Value"]),
                    _viewFactory.Create(typeof(VariableStatusBarView), "Syncer", "/AutoTime", (object[])["Value"]),
                    _viewFactory.Create(typeof(VariableStatusBarView), "Syncer", "/WorkTime", (object[])["Value"])
                }
            }
        });

        SequencePanel.Children.Add(new GroupBox
        {
            Header = "Status",
            Content = new StackPanel
            {
                Children =
                {
                    _viewFactory.Create(typeof(VariableStatusBarView), "Syncer",
                        "/IndexTime"),
                }
            }
        });

        var rearProductView = (VariableStatusBarView)_viewFactory.Create(typeof(VariableStatusBarView), "Head0",
            "/Products",
            (object[])[1, "Exists"]);
        rearProductView.OverrideName = "RearProduct";

        var frontProductView = (VariableStatusBarView)_viewFactory.Create(typeof(VariableStatusBarView), "Head0",
            "/Products",
            (object[])[0, "Exists"]);
        frontProductView.OverrideName = "FrontProduct";

        SequencePanel.Children.Add(new GroupBox
        {
            Header = "Head",
            Content = new StackPanel
            {
                Children =
                {
                    rearProductView,
                    frontProductView
                }
            }
        });

        var targetStageTrayView = (VariableStatusBarView)
            _viewFactory.Create(typeof(VariableStatusBarView), "TargetStage", "/Tray", (object[])["Exists"]);
        targetStageTrayView.OverrideName = "TargetStageTray";

        SequencePanel.Children.Add(new GroupBox
        {
            Header = "Target Carrier",
            Content = new StackPanel
            {
                Children = {targetStageTrayView }
            }
        });

        TargetCarrierGroup.Content = new GridContainerView(_actorRegistry, "TargetStage", "/Tray", null, true)
        {
            ReverseCol = true,
            Height = 230
        };

        if (!_useBallFeeder)
        {
            var sourceStageView1 = _useDualSourceStage
                ? _viewFactory.Create(typeof(VariableStatusBarView), "SourceStage1", "/Tray",
                    (object[])["Exists"])
                : new ContentControl();
            if (sourceStageView1 is VariableStatusBarView view)
                view.OverrideName = "SourceRearTray";

            var sourceStageView0 =
                (VariableStatusBarView)_viewFactory.Create(typeof(VariableStatusBarView),
                    "SourceStage0", "/Tray", (object[])["Exists"]);
            sourceStageView0.OverrideName = "SourceFrontTray";

            SequencePanel.Children.Add(new GroupBox
            {
                Header = "Source Tray",
                Content = new StackPanel
                {
                    Children =
                    {
                        sourceStageView1, sourceStageView0,
                        _viewFactory.Create(typeof(VariableStatusBarView), "Syncer", "/ActiveSourceStageIndex")
                    }
                }
            });

            if (_useDualSourceStage)
            {
                RearSourceTrayGroup.Content = new GridContainerView(_actorRegistry, "SourceStage1", "/Tray", null, true)
                {
                    ReverseCol = true,
                    Height = 200
                };
            }
            else
                RearSourceTrayGroup.Visibility = Visibility.Collapsed;

            FrontSourceTrayGroup.Content = new GridContainerView(_actorRegistry, "SourceStage0", "/Tray", null, true)
            {
                ReverseCol = true,
                Height = 200
            };
        }
        else
        {
            MagazinePanel.Visibility = Visibility.Collapsed;
            SourceTrayPanel.Visibility = Visibility.Collapsed;
        }

        CameraArea.Content = _inspectionContainerView;
    }

    protected virtual void OnViewChanged(UserControl e)
    {
        ViewChanged?.Invoke(this, e);
    }

    private void InitializeButton_Click(object sender, RoutedEventArgs e)
    {
        var view = _viewFactory.Create(typeof(InitializationView));
        OnViewChanged(view);
    }

    private void LoadProject_OnClick(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Select a file",
            Filter = "NanoCad files (*.nnp)|*.nnp|All files (*.*)|*.*"
        };

        if (openFileDialog.ShowDialog() != true) return;
        var filePath = openFileDialog.FileName;
        _headActor.Send(new Message(_uiActor, "LoadProject", new Dict
        {
            ["Index"] = 0,
            ["ProjectFile"] = filePath
        }));
    }
}