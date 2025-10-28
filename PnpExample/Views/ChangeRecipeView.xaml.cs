using System.Windows.Controls;
using PnpExample.Interfaces;
using UserControl = System.Windows.Controls.UserControl;

namespace PnpExample.Views;

/// <summary>
///     Interaction logic for LogView.xaml
/// </summary>
public partial class ChangeRecipeView : UserControl, IViewChanged
{
    public ChangeRecipeView()
    {
        InitializeComponent();
    }

    public event EventHandler<UserControl>? ViewChanged;
}
