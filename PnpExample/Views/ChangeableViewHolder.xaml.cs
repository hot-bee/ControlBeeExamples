using System.Windows.Controls;
using PnpExample.Interfaces;
using UserControl = System.Windows.Controls.UserControl;

namespace PnpExample.Views;

public partial class ChangeableViewHolder : UserControl, IDisposable
{
    private readonly IViewChanged _mainView;

    public ChangeableViewHolder(UserControl mainView)
    {
        InitializeComponent();
        _mainView = (IViewChanged)mainView;
        _mainView.ViewChanged += DataViewFrameViewChanged;
    }

    public void Dispose()
    {
        _mainView.ViewChanged -= DataViewFrameViewChanged;
    }

    private void DataViewFrameViewChanged(object? sender, UserControl e)
    {
        ContentArea.Content = e;
    }

    public void ShowMainView()
    {
        ContentArea.Content = _mainView;
    }
}
