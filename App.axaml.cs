using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using WrightLogs.Services;
using WrightLogs.ViewModels;
using WrightLogs.Views;

namespace WrightLogs;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        MainWindowViewModel mwvm = new();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = mwvm
            };
        }

        base.OnFrameworkInitializationCompleted();
        
        // Now fire up the update check.
        SelfUpdater.CheckForUpdates(mwvm);
    }
}