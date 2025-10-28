using System.Windows;
using System.Windows.Controls;
using ControlBeeWPF.Interfaces;
using ControlBeeWPF.Views;
using PnpExample.Interfaces;
using PnpExample.Services;
using Button = System.Windows.Controls.Button;
using UserControl = System.Windows.Controls.UserControl;

namespace PnpExample.Views;

/// <summary>
///     Interaction logic for AutoOperationView.xaml
/// </summary>
public partial class DataMainView : UserControl, IViewChanged
{
    private readonly IViewFactory _viewFactory;

    public DataMainView(IViewFactory viewFactory)
    {
        _viewFactory = viewFactory;
        InitializeComponent();
    }

    public event EventHandler<UserControl>? ViewChanged;

    private void Btn_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;
        if (button == ActorItemExplorer)
        {
            var view = _viewFactory.Create(typeof(ActorItemExplorerWholeView));
            OnViewChanged(view);
        }
        else if (button == TensionControlSettingsBtn) { }
    }

    protected virtual void OnViewChanged(UserControl e)
    {
        ViewChanged?.Invoke(this, e);
    }
}
