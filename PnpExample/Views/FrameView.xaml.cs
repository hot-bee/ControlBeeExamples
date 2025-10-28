using System.Windows;
using PnpExample.Interfaces;
using PnpExample.Views;
using ControlBee.Interfaces;
using ControlBeeWPF.Interfaces;
using ControlBeeWPF.Views;
using FrameViewModel = PnpExample.ViewModels.FrameViewModel;
using UserControl = System.Windows.Controls.UserControl;
using Dict = System.Collections.Generic.Dictionary<string, object?>;

namespace PnpExample.Views;

/// <summary>
///     Interaction logic for FrameView.xaml
/// </summary>
public partial class FrameView : UserControl
{
    private readonly AutoOperationView _autoOperationView;
    private readonly ChangeableViewHolder _dataView;
    private readonly ChangeableViewHolder _logView;
    private readonly FunctionView _manualView;
    private readonly ChangeableViewHolder _monitorView;
    private readonly ChangeableViewHolder _setupView;
    private readonly IVariableManager _variableManager;
    private readonly FrameViewModel _viewModel;
    private readonly RecipeManagerView _recipeManagerView;
    private readonly UserControl _inspectionContainerView;

    public FrameView(
        FrameViewModel viewModel,
        IViewFactory viewFactory,
        IVariableManager variableManager
    )
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        var headerView = viewFactory.Create(typeof(HeaderView));
        var initializationView = viewFactory.Create(typeof(InitializationView));
        HeaderArea.Content = headerView;
        ContentArea.Content = initializationView;
        _autoOperationView = (AutoOperationView)viewFactory.Create(typeof(AutoOperationView));
        _inspectionContainerView = viewFactory.Create(typeof(InspectionContainerView), "TcpVision", new Dict
        {
            ["Mode"] = "VisionFrame",
            ["Channel"] = -1
        });
        _manualView = (FunctionView)viewFactory.Create(typeof(FunctionView));
        _setupView = new ChangeableViewHolder(viewFactory.Create(typeof(SetupView)));
        _dataView = new ChangeableViewHolder(viewFactory.Create(typeof(DataMainView)));
        _monitorView = new ChangeableViewHolder(viewFactory.Create(typeof(MonitorMainView)));
        _logView = new ChangeableViewHolder(viewFactory.Create(typeof(LogView)));
        _variableManager = variableManager;
        _recipeManagerView = (RecipeManagerView)viewFactory.Create(typeof(RecipeManagerView));

        _autoOperationView.ViewChanged += (sender, control) => ContentArea.Content = control;
    }

    private void Button_OnClick(object sender, RoutedEventArgs e)
    {
        if (Equals(sender, MainButton))
        {
            ContentArea.Content = _autoOperationView;
            _autoOperationView.Refresh();
        }
        else if (Equals(sender, VisionMonitorButton))
        {
            ContentArea.Content = _inspectionContainerView;
        }
        else if (Equals(sender, FunctionButton))
        {
            ContentArea.Content = _manualView;
        }
        else if (Equals(sender, SetupButton))
        {
            ContentArea.Content = _setupView;
            _setupView.ShowMainView();
        }
        else if (Equals(sender, RecipeButton))
        {
            ContentArea.Content = _recipeManagerView;
            _logView.ShowMainView();
        }
        else if (Equals(sender, LogButton))
        {
            ContentArea.Content = _logView;
            _logView.ShowMainView();
        }
        _variableManager.SaveTemporaryVariables();
        _variableManager.DiscardChanges();
    }
}