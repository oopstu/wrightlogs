using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WrightLogs.Models;
using WrightLogs.Services;

namespace WrightLogs.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly UsageFileParser _usageParser = new();

    public LogViewerViewModel LogViewer { get; } = new();
    public UsageViewModel Usage { get; } = new();

    public ObservableCollection<FolderFileEntry> FolderFiles { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadSelectedFileCommand))]
    private FolderFileEntry? _selectedFile;

    [ObservableProperty]
    private string? _openFolderPath;
    
    [ObservableProperty]
    private string? _updateVersionUrl;
    
    [ObservableProperty]
    private string? _updateVersionText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLogViewActive))]
    private bool _isUsageViewActive;

    public bool IsLogViewActive => !IsUsageViewActive;

    public void SetFolder(string folderPath)
    {
        OpenFolderPath = folderPath;
        SelectedFile = null;
        FolderFiles.Clear();

        var classified = Directory.EnumerateFiles(folderPath)
            .Select(path => (Path: path, Kind: ClassifyFile(path)))
            .Where(x => x.Kind is not null)
            .ToList();

        var entries = classified
            .Select(x => new FolderFileEntry(x.Path, Path.GetFileName(x.Path), x.Kind!.Value))
            .ToList();

        // Multiple rollovers of the same Usage family are slow to chart aggregated together
        // (see samples/realexample), so only the virtual "All Usage" entry loads them all at
        // once; picking an individual file loads just that file.
        var usageFamilies = classified
            .Where(x => x.Kind == FolderFileKind.Usage)
            .Select(x => _usageParser.ResolveFamily(x.Path))
            .GroupBy(f => f.BaseName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Where(f => f.FilesOldestFirst.Count > 1)
            .ToList();

        var multipleFamilies = usageFamilies.Count > 1;
        foreach (var family in usageFamilies)
        {
            var anchor = family.FilesOldestFirst[^1];
            var label = multipleFamilies ? $"All Usage — {family.BaseName}" : "All Usage";
            entries.Add(new FolderFileEntry(anchor, label, FolderFileKind.UsageAll));
        }

        var ordered = entries
            .OrderBy(e => e.Kind == FolderFileKind.Log ? 0 : e.Kind == FolderFileKind.UsageAll ? 1 : 2)
            .ThenBy(e => e.FileName, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in ordered)
        {
            FolderFiles.Add(entry);
        }
    }

    private static FolderFileKind? ClassifyFile(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".log" => FolderFileKind.Log,
            ".csv" => FolderFileKind.Usage,
            _ => null,
        };

    [RelayCommand(CanExecute = nameof(CanLoadSelectedFile))]
    private async Task LoadSelectedFileAsync()
    {
        if (SelectedFile is null)
        {
            return;
        }

        switch (SelectedFile.Kind)
        {
            case FolderFileKind.Log:
                IsUsageViewActive = false;
                await LogViewer.OpenFileAsync(SelectedFile.FullPath);
                break;
            case FolderFileKind.UsageAll:
                IsUsageViewActive = true;
                await Usage.OpenFamilyAsync(SelectedFile.FullPath);
                break;
            default:
                IsUsageViewActive = true;
                await Usage.OpenSingleFileAsync(SelectedFile.FullPath);
                break;
        }
    }

    private bool CanLoadSelectedFile() => SelectedFile is not null;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsUsageViewActive)
        {
            await Usage.RefreshCommand.ExecuteAsync(null);
        }
        else
        {
            await LogViewer.RefreshCommand.ExecuteAsync(null);
        }
    }
}
