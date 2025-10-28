using PnpExample.Models;
using ControlBee.Interfaces;
using ControlBee.Models;
using ControlBee.Variables;
using ControlBeeWPF.Views;
using log4net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Dict = System.Collections.Generic.Dictionary<string, object?>;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MessageBox = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;
using UserControl = System.Windows.Controls.UserControl;

namespace PnpExample.Views;

/// <summary>
///     Interaction logic for GridContainerView.xaml
/// </summary>
public partial class GridContainerView : UserControl
{
    private static readonly ILog Logger = LogManager.GetLogger(nameof(VariableStatusBarView));

    private readonly IActor _actor;
    private readonly string _actorName;
    private readonly ActorItemBinder _binder;
    private readonly string _itemPath;
    private readonly bool _maskMode;

    private readonly Dictionary<(int row, int col), Grid> _grids = new();
    private readonly object[] _subItemPath;
    private readonly IActor _uiActor;
    private GridContainer? _value;

    public bool ReverseRow { get; set; }
    public bool ReverseCol { get; set; }

    private Point? _dragStart = null;
    private Rectangle? _selectionRect = null;

    private (int row, int col)? TranslateLocation(int row, int col)
    {
        if (_value == null) return null;
        var size = _value.Size;
        if (ReverseRow) row = size.Item1 - row - 1;
        if (ReverseCol) col = size.Item2 - col - 1;
        return (row, col);
    }

    public GridContainerView(
        IActorRegistry actorRegistry,
        string actorName,
        string itemPath,
        object[]? subItemPath,
        bool maskMode
    )
    {
        _actorName = actorName;
        _itemPath = itemPath;
        _subItemPath = subItemPath ?? [];
        _maskMode = maskMode;
        InitializeComponent();
        _actor = actorRegistry.Get(actorName)!;
        _uiActor = actorRegistry.Get("Ui")!;
        _binder = new ActorItemBinder(actorRegistry, actorName, itemPath);
        _binder.MetaDataChanged += BinderOnMetaDataChanged;
        _binder.DataChanged += Binder_DataChanged;
        SizeChanged += GridContainerView_SizeChanged;

        MainCanvas.MouseLeftButtonDown += MainCanvasOnMouseLeftButtonDown;
        MainCanvas.MouseMove += MainCanvasOnMouseMove;
        MainCanvas.MouseLeave += MainCanvasMouseLeave;
        MainCanvas.MouseLeftButtonUp += MainCanvasOnMouseLeftButtonUp;
    }

    public GridContainerView(IActorRegistry actorRegistry, string actorName, string itemPath)
        : this(actorRegistry, actorName, itemPath, null, false)
    {
    }

    private void GridContainerView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateGridLayout();
    }

    public void Dispose()
    {
        _binder.MetaDataChanged -= BinderOnMetaDataChanged;
        _binder.DataChanged -= Binder_DataChanged;
        _binder.Dispose();
    }

    private void BinderOnMetaDataChanged(object? sender, Dict e)
    {
        var name = e["Name"]?.ToString();
        var unit = e["Unit"]?.ToString();
        var desc = e["Desc"]?.ToString();
    }

    private void Binder_DataChanged(object? sender, Dict e)
    {
        var valueChangedArgs = e[nameof(ValueChangedArgs)] as ValueChangedArgs;
        var location = valueChangedArgs?.Location!;
        var newValue = valueChangedArgs?.NewValue!;
        UpdateValue(location, newValue);
    }

    private void UpdateGridLayout()
    {
        if (_value == null)
            return;
        if (ActualHeight == 0 || ActualWidth == 0)
            return;
        MainCanvas.Children.Clear();
        var size = _value.Size;
        var cellHeight = (int)(ActualHeight / size.Item1);
        var cellWidth = (int)(ActualWidth / size.Item2);
        for (var i = 0; i < size.Item1; i++)
        for (var j = 0; j < size.Item2; j++)
        {
            var displayLocation = TranslateLocation(i, j)!.Value;
            var grid = new Grid
            {
                Margin = new Thickness(cellWidth * displayLocation.col, cellHeight * displayLocation.row, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Width = cellWidth - 1,
                Height = cellHeight - 1,
                Tag = (i, j)
            };
            grid.Children.Add(new Rectangle
            {
                StrokeThickness = 1,
                Stroke = Brushes.Black
            });
            if (_maskMode)
                grid.Children.Add(new TextBlock
                {
                    Text = "-",
                    Margin = new Thickness(5, 0, 0, 0)
                });
            _grids[(i, j)] = grid;
            grid.MouseRightButtonDown += RectangleOnMouseRightButtonDown;
            MainCanvas.Children.Add(grid);
            UpdateCell(i, j);
        }
    }

    private void RectangleOnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_maskMode) return;
        if (
            MessageBox.Show(
                "Do you want to turn this on/off?",
                "Change value",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question
            ) != MessageBoxResult.Yes
        )
            return;
        var rectangle = (Grid)sender;
        var location = (ValueTuple<int, int>)rectangle.Tag;
        _actor.Send(
            new ActorItemMessage(
                _uiActor,
                _itemPath,
                "_itemDataWrite",
                new ItemDataWriteArgs([nameof(GridContainer.Consumers), location], -1)
            )
        );
    }

    private void UpdateCell(int i, int j)
    {
        if (_value == null)
            return;
        try
        {
            if (_value.WorkDone[i, j])
                (_grids[(i, j)].Children[0] as Rectangle)!.Fill = Brushes.RoyalBlue;
            else if (_value.CellVisionResult[i, j].Ng)
                (_grids[(i, j)].Children[0] as Rectangle)!.Fill = Brushes.Red;
            else if (_value.CellExists[i, j] && _value.CellPostVisionResult[i, j].Ng)
                (_grids[(i, j)].Children[0] as Rectangle)!.Fill = Brushes.Fuchsia;
            else if (_value.CellExists[i, j] && _value.CellPostVisionResult[i, j].Ok)
                (_grids[(i, j)].Children[0] as Rectangle)!.Fill = Brushes.MediumSpringGreen;
            else if (_value.CellExists[i, j])
                (_grids[(i, j)].Children[0] as Rectangle)!.Fill = _value.CellVisionResult[i, j].Inspected
                    ? Brushes.LimeGreen
                    : Brushes.LawnGreen;
            else
                (_grids[(i, j)].Children[0] as Rectangle)!.Fill = _value.CellVisionResult[i, j].Inspected
                    ? Brushes.AntiqueWhite
                    : Brushes.White;

            if (_maskMode)
            {
                if (_value.Consumers.Size.Item1 <= i || _value.Consumers.Size.Item2 <= j) return;
                var text = _value.Consumers[i, j] == -1 ? "N" : _value.Consumers[i, j] == 0 ? "F" : "R";
                (_grids[(i, j)].Children[1] as TextBlock)!.Text = text;
            }
        }
        catch (KeyNotFoundException ex)
        {
            Logger.Warn("KeyNotFoundException.", ex);
        }
    }

    private void UpdateValue(object[] location, object newValue)
    {
        if (location.Length == 0)
        {
            _value = (GridContainer)newValue;
            UpdateGridLayout();
        }
        else
        {
            if (_value == null)
                return;
            if (location[0] as string == nameof(GridContainer.CellExists))
            {
                var idx = (ValueTuple<int, int>)location[1];
                _value.CellExists[idx.Item1, idx.Item2] = (bool)newValue;
                UpdateCell(idx.Item1, idx.Item2);
            }

            if (location[0] as string == nameof(GridContainer.CellVisionResult))
            {
                var idx = (ValueTuple<int, int>)location[1];
                _value.CellVisionResult[idx.Item1, idx.Item2] =
                    (VisionResult)newValue; // TODO: Any better way than this?
                UpdateCell(idx.Item1, idx.Item2);
            }
            
            if (location[0] as string == nameof(GridContainer.CellPostVisionResult))
            {
                var idx = (ValueTuple<int, int>)location[1];
                _value.CellPostVisionResult[idx.Item1, idx.Item2] =
                    (VisionResult)newValue; // TODO: Any better way than this?
                UpdateCell(idx.Item1, idx.Item2);
            }

            if (location[0] as string == nameof(GridContainer.WorkDone))
            {
                var idx = (ValueTuple<int, int>)location[1];
                _value.WorkDone[idx.Item1, idx.Item2] = (bool)newValue;
                UpdateCell(idx.Item1, idx.Item2);
            }

            if (location[0] as string == nameof(GridContainer.Consumers))
            {
                var idx = (ValueTuple<int, int>)location[1];
                _value.Consumers[idx.Item1, idx.Item2] = (int)newValue;
                UpdateCell(idx.Item1, idx.Item2);
            }
        }
    }

    private void MainCanvasOnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Point current = e.GetPosition(MainCanvas);
        _dragStart = current;

        _selectionRect = new Rectangle
        {
            Stroke = Brushes.Blue,
            StrokeThickness = 1,
            Fill = new SolidColorBrush(Color.FromArgb(50, 0, 0, 255))
        };
        MainCanvas.Children.Add(_selectionRect);
        Canvas.SetLeft(_selectionRect, current.X);
        Canvas.SetTop(_selectionRect, current.Y);
        _selectionRect.Width = 1;
        _selectionRect.Height = 1;
    }

    private void MainCanvasMouseLeave(object sender, MouseEventArgs e)
    {
        MainCanvas.Children.Remove(_selectionRect);
    }

    private void MainCanvasOnMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart == null || _selectionRect == null || e.LeftButton != MouseButtonState.Pressed)
            return;

        Point current = e.GetPosition(MainCanvas);

        double x = Math.Min(current.X, _dragStart.Value.X);
        double y = Math.Min(current.Y, _dragStart.Value.Y);
        double w = Math.Abs(current.X - _dragStart.Value.X);
        double h = Math.Abs(current.Y - _dragStart.Value.Y);

        Canvas.SetLeft(_selectionRect, x);
        Canvas.SetTop(_selectionRect, y);
        _selectionRect.Width = w;
        _selectionRect.Height = h;
    }

    private void MainCanvasOnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart == null || _selectionRect == null)
            return;

        Rect selectedArea = new Rect(
            Canvas.GetLeft(_selectionRect),
            Canvas.GetTop(_selectionRect),
            _selectionRect.Width,
            _selectionRect.Height
        );

        var dialog = new OnOffDialogView("Do you want to turn this on/off?");

        try
        {
            bool? dialogResult = dialog.ShowDialog();

            if (dialogResult == true)
            {
                switch (dialog.Result)
                {
                    case OnOffDialogView.OnOffResult.On:
                    case OnOffDialogView.OnOffResult.Off:
                        ChangeCellStates(selectedArea, dialog.Result);
                        break;
                }
            }
        }
        finally
        {
            MainCanvas.Children.Remove(_selectionRect);
            _selectionRect = null;
            _dragStart = null;
        }
    }

    private void ChangeCellStates(Rect selectedArea, OnOffDialogView.OnOffResult dialogResult)
    {
        foreach (var grid in _grids.Values)
        {
            double cellLeft = grid.Margin.Left;
            double cellTop = grid.Margin.Top;
            double cellWidth = grid.Width;
            double cellHeight = grid.Height;

            Rect cellBounds = new Rect(cellLeft, cellTop, cellWidth, cellHeight);

            if (selectedArea.IntersectsWith(cellBounds))
            {
                var location = (ValueTuple<int, int>)grid.Tag;
                var value = _value!.CellExists[location.Item1, location.Item2];

                switch (dialogResult)
                {
                    case OnOffDialogView.OnOffResult.On:
                        if (!value)
                            SetCellStates(location, !value);
                        break;
                    case OnOffDialogView.OnOffResult.Off:
                        if (value)
                            SetCellStates(location, !value);
                        break;
                }
            }
        }
    }

    private void SetCellStates((int, int) location, bool value)
    {
        _actor.Send(
            new ActorItemMessage(
                _uiActor,
                _itemPath,
                "_itemDataWrite",
                new ItemDataWriteArgs([nameof(GridContainer.CellExists), location], value)
            )
        );
        _actor.Send(
            new ActorItemMessage(
                _uiActor,
                _itemPath,
                "_itemDataWrite",
                new ItemDataWriteArgs([nameof(GridContainer.CellVisionResult), location],
                    new VisionResult())
            )
        );
        _actor.Send(
            new ActorItemMessage(
                _uiActor,
                _itemPath,
                "_itemDataWrite",
                new ItemDataWriteArgs([nameof(GridContainer.CellPostVisionResult), location],
                    new VisionResult())
            )
        );
    }
}