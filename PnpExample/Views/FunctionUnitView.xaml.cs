using System.Windows;
using System.Windows.Controls;
using ControlBee.Interfaces;
using ControlBee.Models;
using PnpExample.Interfaces;
using Button = System.Windows.Controls.Button;
using Message = ControlBee.Models.Message;
using UserControl = System.Windows.Controls.UserControl;

namespace PnpExample.Views;

/// <summary>
///     Interaction logic for FunctionUnitView.xaml
/// </summary>
public partial class FunctionUnitView : UserControl
{
    private readonly Dictionary<Guid, Button> _propertyReads = new();
    private readonly List<Button> _buttons = [];

    public FunctionUnitView(IActorRegistry actorRegistry, IActor actor)
    {
        InitializeComponent();
        var uiActor = (IUiActor)actorRegistry.Get("Ui")!;
        uiActor.MessageArrived += UiActor_MessageArrived;

        FunctionPanel.Children.Clear();
        foreach (var functionName in actor.GetFunctions())
        {
            var textBlock = new TextBlock { Text = functionName, TextWrapping = TextWrapping.Wrap };
            var button = new Button
            {
                Content = textBlock,
                Width = 100,
                Height = 40,
                Margin = new Thickness(5),
            };
            button.Click += (sender, args) =>
            {
                actor.Send(new Message(uiActor, functionName));
            };
            FunctionPanel.Children.Add(button);
            var reqId = actor.Send(
                new Message(uiActor, "_propertyRead", $"/Functions/{functionName}")
            );
            _propertyReads[reqId] = button;
            _buttons.Add(button);
        }
    }

    private void UiActor_MessageArrived(object? sender, Message message)
    {
        switch (message.Name)
        {
            case "_property":
            {
                if (_propertyReads.TryGetValue(message.RequestId, out var button))
                {
                    if (message.DictPayload == null)
                        break;
                    var name = message.DictPayload.GetValueOrDefault("Name") as string;
                    var desc = message.DictPayload.GetValueOrDefault("Desc") as string;
                    var textBlock = (TextBlock)button.Content;
                    textBlock.Text = name;
                    if (!string.IsNullOrEmpty(desc))
                        button.ToolTip = desc;
                }

                break;
            }
            case "_status":
                if (message.Sender.Name == "Syncer")
                {
                    var ready = message.DictPayload!.GetValueOrDefault("_ready") is true;
                    // _buttons.ForEach(x => x.IsEnabled = ready);  // TODO: temp
                }

                break;
        }
    }

    public void SetCustomView(UserControl customView)
    {
        CustomArea.Content = customView;
    }
}
