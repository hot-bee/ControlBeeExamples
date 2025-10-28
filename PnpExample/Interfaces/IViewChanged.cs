using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;

namespace PnpExample.Interfaces;

public interface IViewChanged
{
    event EventHandler<UserControl>? ViewChanged;
}
