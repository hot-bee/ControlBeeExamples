using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using PnpExample.Interfaces;
using ControlBee.Interfaces;
using PnpExample.Services;
using ControlBeeWPF.Interfaces;
using ControlBeeWPF.Views;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using UserControl = System.Windows.Controls.UserControl;

namespace PnpExample.Views;

/// <summary>
///     Interaction logic for FunctionView.xaml
/// </summary>
public partial class FunctionView : UserControl
{
    private readonly List<Button> _buttons = [];

    public FunctionView(
        IActorRegistry actorRegistry,
        IViewFactory viewFactory,
        FunctionUnitViewFactory functionUnitViewFactory
    )
    {
        InitializeComponent();
        UnitsPanel.Children.Clear();

        foreach (var actorName in actorRegistry.GetActorNames())
        {
            if (actorName is "Ui")
                continue;
            var actor = actorRegistry.Get(actorName)!;
            var button = new Button { Content = actor.Title };
            var view = functionUnitViewFactory.Create(actor);
            button.Click += (sender, args) =>
            {
                ContentArea.Content = view;
                FocusButton(button);
            };
            _buttons.Add(button);
            UnitsPanel.Children.Add(button);
        }

        if (UnitsPanel.Children.Count > 0)
            UnitsPanel.Children[0].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    }

    public void FocusButton(Button focusedButton)
    {
        foreach (var button in _buttons)
            button.Background = Brushes.WhiteSmoke;
        focusedButton.Background = Brushes.LightSkyBlue;
    }
}
