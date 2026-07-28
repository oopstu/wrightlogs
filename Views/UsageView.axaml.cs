using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using LiveChartsCore.Drawing;
using WrightLogs.ViewModels;

namespace WrightLogs.Views;

public partial class UsageView : UserControl
{
    public UsageView()
    {
        InitializeComponent();
        Chart.PointerPressed += OnChartPointerPressed;
    }

    private async void OnChartPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(Chart).Properties.IsLeftButtonPressed ||
            DataContext is not UsageViewModel { IsAwaitingPointSelection: true } vm)
        {
            return;
        }

        await vm.PointSelectedAsync(PixelToTime(e.GetPosition(Chart)));
    }

    private DateTime PixelToTime(Point position)
    {
        var data = Chart.ScalePixelsToData(new LvcPointD(position.X, position.Y), 0, 0);
        var ticks = (long)Math.Clamp(data.X, 0, DateTime.MaxValue.Ticks);
        return new DateTime(ticks);
    }

    private void OnMetricPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Control { DataContext: UsageMetricToggle toggle } && DataContext is UsageViewModel vm)
        {
            vm.HoveredMetricName = toggle.Name;
        }
    }

    private void OnMetricPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Control { DataContext: UsageMetricToggle toggle } &&
            DataContext is UsageViewModel { HoveredMetricName: var hovered } vm &&
            hovered == toggle.Name)
        {
            vm.HoveredMetricName = null;
        }
    }
}
