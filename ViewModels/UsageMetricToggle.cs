using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WrightLogs.ViewModels;

public partial class UsageMetricToggle : ObservableObject
{
    public UsageMetricToggle(string name, IBrush lineBrush)
    {
        Name = name;
        LineBrush = lineBrush;
    }

    public string Name { get; }

    public IBrush LineBrush { get; }

    [ObservableProperty]
    private bool _isChecked = true;
}
