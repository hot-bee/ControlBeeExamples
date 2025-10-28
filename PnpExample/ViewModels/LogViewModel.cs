using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlBee.Interfaces;
using log4net;

namespace PnpExample.ViewModels;

public partial class LogViewModel(IEventManager eventManager, IVariableManager variableManager) : ObservableObject
{
    public readonly IEventManager EventManager = eventManager;
    private static readonly ILog Logger = LogManager.GetLogger("LogViewModel");

    [ObservableProperty] private DataTable? _currentTable;
    [ObservableProperty] private DataView? _currentView;
    [ObservableProperty] private DateTime _startDate = DateTime.Today;
    [ObservableProperty] private DateTime _endDate = DateTime.Today;

    [RelayCommand]
    private void DateFilterTable()
    {
        LoadTable(CurrentTable?.TableName!);
        var table = CurrentTable;
        if (table is null)
            return;

        var start = StartDate.Date;
        var end = EndDate.Date.AddDays(1);

        string filterExpression =
            $"updated_at >= #{start:yyyy-MM-dd}# " +
            $"And updated_at < #{end:yyyy-MM-dd}#";

        try
        {
            var filtered = table.Select(filterExpression);
            if (filtered.Length > 0)
                CurrentView = filtered.CopyToDataTable().DefaultView;
            else
                CurrentView = null;
        }
        catch (Exception ex)
        {
            Logger.Error($"Table filtering failed." +
                         $"TableName: {CurrentTable?.TableName!}" +
                         $"Message: {ex.Message}");
        }
    }

    [RelayCommand]
    private void LoadTable(string tableName)
    {
        CurrentTable = EventManager.ReadAll(tableName);
        CurrentView = CurrentTable.DefaultView;
    }

    [RelayCommand]
    private void LoadVariableChanges()
    {
        CurrentTable = variableManager.ReadVariableChanges();
        CurrentView = CurrentTable.DefaultView;
    }
}