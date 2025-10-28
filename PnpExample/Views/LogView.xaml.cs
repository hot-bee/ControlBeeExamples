using PnpExample.Interfaces;
using PnpExample.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace PnpExample.Views;

/// <summary>
///     Interaction logic for LogView.xaml
/// </summary>
public partial class LogView : IViewChanged
{
    public LogView(LogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public event EventHandler<UserControl>? ViewChanged;
}
