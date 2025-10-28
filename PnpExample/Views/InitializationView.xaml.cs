using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ControlBee.Constants;
using ControlBeeAbstract.Exceptions;
using ControlBeeWPF.Components;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using InitializationViewModel = PnpExample.ViewModels.InitializationViewModel;
using UserControl = System.Windows.Controls.UserControl;

namespace PnpExample.Views;

/// <summary>
///     Interaction logic for InitializationView.xaml
/// </summary>
public partial class InitializationView : UserControl, IDisposable
{
    private readonly Dictionary<string, ToggleImageButton> _buttonMap = new();
    private readonly InitializationViewModel _viewModel;

    public InitializationView(InitializationViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        MakeButtons();
        UpdateButtonColor();
        _viewModel.PropertyChanged += _viewModel_PropertyChanged;
    }

    public void Dispose()
    {
        _viewModel.PropertyChanged -= _viewModel_PropertyChanged;
        foreach (var (_, button) in _buttonMap)
            button.PropertyChanged -= CheckButton_PropertyChanged;
    }

    private void MakeButtons()
    {
        foreach (var (actorName, actorTitle) in _viewModel.GetActorTitles())
        {
            var checkButton = new ToggleImageButton(
                new BitmapImage(
                    new Uri("/Images/326558_blank_check_box_icon.png", UriKind.RelativeOrAbsolute)
                ),
                new BitmapImage(
                    new Uri("/Images/326561_box_check_icon.png", UriKind.RelativeOrAbsolute)
                ),
                actorTitle
            )
            {
                Width = 150,
                Height = 60,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(10, 0, 0, 0),
                Margin = new Thickness(10),
            };
            _buttonMap[actorName] = checkButton;
            WrapPanel1.Children.Add(checkButton);
            checkButton.PropertyChanged += CheckButton_PropertyChanged;
            checkButton.IsChecked = true;
        }
    }

    private void CheckButton_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "IsChecked")
        {
            var button = (ToggleImageButton)sender!;
            var actorName = _buttonMap.FirstOrDefault(x => x.Value == button).Key;
            _viewModel.SetInitialization(actorName, button.IsChecked);
        }
    }


    private void _viewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(_viewModel.InitializationStatus):
            {
                UpdateButtonColor();
                break;
            }
        }
    }

    private void UpdateButtonColor()
    {
        foreach (var (actorName, initialized) in _viewModel.InitializationStatus)
            switch (initialized)
            {
                case InitializationStatus.Uninitialized:
                    _buttonMap[actorName].ClearValue(BackgroundProperty);
                    break;
                case InitializationStatus.Initialized:
                    _buttonMap[actorName].Background = new SolidColorBrush(
                        Colors.LightGreen
                    );
                    break;
                case InitializationStatus.Initializing:
                    _buttonMap[actorName].Background = new SolidColorBrush(Colors.Yellow);
                    break;
                case InitializationStatus.Error:
                    _buttonMap[actorName].Background = new SolidColorBrush(
                        Colors.SlateGray
                    );
                    break;
                default:
                    throw new ValueError();
            }

        if (_viewModel.InitializationAll)
            InitializeAllButton.Background = new SolidColorBrush(Colors.LightGreen);
        else
            InitializeAllButton.ClearValue(BackgroundProperty);
        return;
    }

    private void SelectAllButton_OnClick(object sender, RoutedEventArgs e)
    {
        foreach (var (_, button) in _buttonMap)
            button.IsChecked = true;
    }

    private void DeselectAllButton_OnClick(object sender, RoutedEventArgs e)
    {
        foreach (var (_, button) in _buttonMap)
            button.IsChecked = false;
    }
}
