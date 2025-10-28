using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ControlBee.Interfaces;
using ControlBee.Models;
using ControlBee.Variables;
using log4net;
using Brushes = System.Windows.Media.Brushes;
using Dict = System.Collections.Generic.Dictionary<string, object?>;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;

namespace PnpExample.Views;

/// <summary>
///     Interaction logic for BoolRectView.xaml
/// </summary>
public partial class BoolRectView : UserControl
{
    private static readonly ILog Logger = LogManager.GetLogger(nameof(BoolRectView));

    private readonly IActor _actor;
    private readonly string _actorName;
    private readonly ActorItemBinder _binder;
    private readonly string _itemPath;
    private readonly object[] _subItemPath;
    private readonly IActor _uiActor;
    private object? _value;

    public BoolRectView(
        IActorRegistry actorRegistry,
        string actorName,
        string itemPath,
        object[]? subItemPath
    )
    {
        _actorName = actorName;
        _itemPath = itemPath;
        _subItemPath = subItemPath ?? [];
        InitializeComponent();
        _actor = actorRegistry.Get(actorName)!;
        _uiActor = actorRegistry.Get("Ui")!;
        _binder = new ActorItemBinder(actorRegistry, actorName, itemPath);
        _binder.MetaDataChanged += BinderOnMetaDataChanged;
        _binder.DataChanged += Binder_DataChanged;
    }

    public BoolRectView(IActorRegistry actorRegistry, string actorName, string itemPath)
        : this(actorRegistry, actorName, itemPath, null) { }

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
        var value = GetValue(location, newValue);
        if (value == null)
            return;
        _value = value;
        if (value is bool)
            BoolValueRect.Fill = _value is true ? Brushes.LawnGreen : Brushes.WhiteSmoke;
    }

    private object? GetValue(object[] location, object newValue)
    {
        var paths = _subItemPath.ToArray();
        foreach (var o in location)
            if (paths[0].Equals(o))
                paths = paths[1..];
            else
                return null;

        var curValue = newValue;
        foreach (var pathPart in paths)
            if (curValue is IIndex1D index1D)
            {
                if (pathPart is int index)
                    curValue = index1D.GetValue(index);
                else
                    return null;
            }
            else if (curValue is IIndex2D index2D)
            {
                if (pathPart is ValueTuple<int, int> index)
                    curValue = index2D.GetValue(index.Item1, index.Item2);
                else
                    return null;
            }
            else
            {
                if (pathPart is string propertyName)
                {
                    var propertyInfo = curValue?.GetType().GetProperty(propertyName);
                    if (propertyInfo == null)
                    {
                        Logger.Warn($"PropertyInfo is null. ({_actorName}, {_itemPath})");
                        curValue = null;
                        break;
                    }

                    curValue = propertyInfo.GetValue(curValue);
                }
                else
                {
                    return null;
                }
            }

        return curValue;
    }

    private void ToggleBoolValue(bool booleanValue)
    {
        if (
            MessageBox.Show(
                "Do you want to turn this on/off?",
                "Change value",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question
            ) == MessageBoxResult.Yes
        )
            _actor.Send(
                new ActorItemMessage(
                    _uiActor,
                    _itemPath,
                    "_itemDataWrite",
                    new ItemDataWriteArgs(_subItemPath, !booleanValue)
                )
            );
    }

    private void BoolValueRect_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_value is not bool boolValue)
            return;
        ToggleBoolValue(boolValue);
    }
}
