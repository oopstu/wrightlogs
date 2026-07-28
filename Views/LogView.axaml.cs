using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using WrightLogs.Models;
using WrightLogs.Services;
using WrightLogs.ViewModels;

namespace WrightLogs.Views;

public partial class LogView : UserControl
{
    private Window? _window;

    public LogView()
    {
        InitializeComponent();
        ApplySavedColumnWidths();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _window = TopLevel.GetTopLevel(this) as Window;
        if (_window is not null)
        {
            _window.Closing += OnWindowClosing;
        }
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        SaveColumnWidths();
    }

    private void ApplySavedColumnWidths()
    {
        var widths = ColumnLayoutSettingsService.Load();
        if (widths.Count == 0)
        {
            return;
        }

        ApplyWidths(FullLogGrid, widths);
        ApplyWidths(TaggedGrid, widths);
    }

    private static void ApplyWidths(DataGrid grid, Dictionary<string, double> widths)
    {
        foreach (var column in grid.Columns)
        {
            var header = column.Header?.ToString();
            if (header is not null && widths.TryGetValue(header, out var width) && width > 0)
            {
                column.Width = new DataGridLength(width);
            }
        }
    }

    private void SaveColumnWidths()
    {
        var widths = new Dictionary<string, double>();
        foreach (var column in FullLogGrid.Columns)
        {
            var header = column.Header?.ToString();
            if (header is not null && column.ActualWidth > 0)
            {
                widths[header] = column.ActualWidth;
            }
        }

        ColumnLayoutSettingsService.Save(widths);
    }

    private void OnCellPointerPressed(object? sender, DataGridCellPointerPressedEventArgs e)
    {
        var point = e.PointerPressedEventArgs.GetCurrentPoint(e.Row);
        if (!point.Properties.IsRightButtonPressed)
        {
            return;
        }

        if (e.Row.DataContext is not LogEntry entry || DataContext is not LogViewerViewModel viewModel)
        {
            return;
        }

        var isTagged = viewModel.IsRowTagged(entry.RowIndex);
        var tagMenuItem = new MenuItem
        {
            Header = isTagged ? "Untag Line" : "Tag Line",
            Command = viewModel.ToggleTagCommand,
            CommandParameter = entry,
        };

        var viewDetailMenuItem = new MenuItem
        {
            Header = "View Detail",
            Command = viewModel.ViewDetailCommand,
            CommandParameter = entry,
        };

        var menu = new ContextMenu();
        menu.Items.Add(tagMenuItem);
        menu.Items.Add(viewDetailMenuItem);
        menu.Open(e.Row);
    }
}
