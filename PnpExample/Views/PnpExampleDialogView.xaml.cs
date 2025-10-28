using System.Windows;
using PnpExample.Models;
using ControlBee.Constants;
using ControlBee.Interfaces;
using ControlBeeAbstract.Exceptions;
using ControlBeeWPF.Utils;
using log4net;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Message = ControlBee.Models.Message;

namespace PnpExample.Views;

public partial class PnpExampleDialogView : Window, IDialogView
{
    private static readonly ILog Logger = LogManager.GetLogger("Sequence");
    private readonly IActor _syncerActor;

    private readonly IActor _uiActor;

    private PnpExampleDialogContext? _context;

    private Message? _requestMessage;

    public PnpExampleDialogView(IActorRegistry actorRegistry)
    {
        InitializeComponent();
        Owner = Application.Current.MainWindow;
        _uiActor = actorRegistry.Get("Ui")!;
        _syncerActor = actorRegistry.Get("Syncer")!;
        Loaded += (sender, args) => { WindowUtils.DisableCloseButton(this); };
    }

    public void Show(IDialogContext context, Message message)
    {
        DataContext = context;
        context.CloseRequested += (sender, args) => { Close(); };
        _context = (PnpExampleDialogContext)context;
        _requestMessage = message;
        switch (context.Severity)
        {
            case DialogSeverity.Info:
                HeadGrid.Background = Brushes.LightBlue;
                NameLabel.Foreground = Brushes.Black;
                break;
            case DialogSeverity.Warn:
                HeadGrid.Background = Brushes.Yellow;
                NameLabel.Foreground = Brushes.Black;
                break;
            case DialogSeverity.Error:
                HeadGrid.Background = Brushes.DarkRed;
                NameLabel.Foreground = Brushes.White;
                break;
            case DialogSeverity.Fatal:
                HeadGrid.Background = Brushes.MediumPurple;
                NameLabel.Foreground = Brushes.White;
                break;
            default:
                throw new ValueError();
        }

        if (context.ActionButtons.Length > 0)
        {
            CloseButton.IsEnabled = false;
            ActionButtonPanel.Visibility = Visibility.Visible;
            foreach (var actionButton in context.ActionButtons)
            {
                var button = new Button
                {
                    Content = actionButton
                };
                button.Click += (sender, args) =>
                {
                    _requestMessage?.Sender.Send(new Message(_requestMessage, _uiActor, "_dialogResult", actionButton));
                    Close();
                };
                ActionButtonPanel.Children.Add(button);
            }
        }

        Show();
    }

    public event EventHandler? DialogClosed;

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        _requestMessage?.Sender.Send(new Message(_requestMessage, _uiActor, "_dialogResult", "Ok"));
        _syncerActor.Send(new Message(_uiActor, "_resetError"));
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        OnDialogClosed();
    }

    private void ImageButton_OnClick(object sender, RoutedEventArgs e)
    {
        Logger.Info($"Image: {_context?.Image}");
    }

    protected virtual void OnDialogClosed()
    {
        DialogClosed?.Invoke(this, EventArgs.Empty);
    }
}