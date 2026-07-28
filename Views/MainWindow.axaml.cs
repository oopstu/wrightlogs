using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using WrightLogs.Services;
using WrightLogs.ViewModels;

namespace WrightLogs.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnOpenFolderClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null || DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open Log Folder",
            AllowMultiple = false,
        });

        var path = folders.Count == 0 ? null : folders[0].TryGetLocalPath();
        if (path is null)
        {
            return;
        }

        vm.SetFolder(path);
    }

    private void UpdateButtonClickEvent(object? sender, RoutedEventArgs e)
    {
        // Do upgrade. 
        if (DataContext is MainWindowViewModel vm)
        {
            SelfUpdater.ExecuteUpdateToVersion(vm);
        }
    }
}
