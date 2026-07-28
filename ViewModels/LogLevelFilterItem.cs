using CommunityToolkit.Mvvm.ComponentModel;
using WrightLogs.Models;

namespace WrightLogs.ViewModels;

public partial class LogLevelFilterItem : ObservableObject
{
    public LogLevelFilterItem(LogLevel level)
    {
        Level = level;
    }

    public LogLevel Level { get; }

    public string DisplayName => Level.ToString();

    [ObservableProperty]
    private bool _isChecked = true;
}
