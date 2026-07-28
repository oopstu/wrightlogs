using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WrightLogs.Models;
using WrightLogs.Services;

namespace WrightLogs.ViewModels;

public partial class LogViewerViewModel : ViewModelBase, IDisposable
{
    private static readonly TimeSpan FilterDebounceDelay = TimeSpan.FromMilliseconds(300);

    private readonly LogFileIndexService _indexService = new();
    private readonly LogFileReaderService _readerService = new();
    private readonly LogFilterService _filterService = new();

    private string? _filePath;
    private LineIndex? _index;
    private FileTailWatcherService? _tailWatcher;
    private CancellationTokenSource? _filterDebounceCts;
    private bool _isExtending;
    private readonly HashSet<int> _taggedRowIndexes = new();

    public ObservableCollection<LogLevelFilterItem> LevelFilters { get; }

    public IReadOnlyList<LogSearchField> SearchFields { get; } = Enum.GetValues<LogSearchField>();

    [ObservableProperty]
    private VirtualizedLogCollection? _entries;

    [ObservableProperty]
    private VirtualizedLogCollection? _taggedEntries;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private LogSearchField _selectedSearchField = LogSearchField.All;

    [ObservableProperty]
    private bool _isLiveTailEnabled;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "No file open";

    [ObservableProperty]
    private string? _openFilePath;

    [ObservableProperty]
    private LogEntry? _detailEntry;

    [ObservableProperty]
    private bool _isDetailPaneOpen;

    public string RandomQuote
    {
        get
        {
            return QuoteGenerator.GetQuote();
        }

        set { }
    }
    
    public LogViewerViewModel()
    {
        LevelFilters = new ObservableCollection<LogLevelFilterItem>(
            Enum.GetValues<LogLevel>().Select(level => new LogLevelFilterItem(level)));

        foreach (var item in LevelFilters)
        {
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(LogLevelFilterItem.IsChecked))
                {
                    QueueFilterRefresh();
                }
            };
        }
    }

    public async Task OpenFileAsync(string path, CancellationToken ct = default)
    {
        StopTailWatcher();

        _filePath = path;
        OpenFilePath = path;
        IsBusy = true;
        StatusText = "Indexing...";

        try
        {
            var index = await _indexService.BuildAsync(path, ct).ConfigureAwait(true);
            _index = index;
            Entries = new VirtualizedLogCollection(path, index, _readerService);

            _taggedRowIndexes.Clear();
            TaggedEntries = new VirtualizedLogCollection(path, index, _readerService);
            TaggedEntries.SetFilteredRows(new List<int>());

            if (IsLiveTailEnabled)
            {
                StartTailWatcher();
            }
        }
        finally
        {
            IsBusy = false;
        }

        UpdateStatus();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await ExtendAndNotifyAsync();
    }

    /// <summary>True if the given row (by <see cref="LogEntry.RowIndex"/>) is currently tagged.</summary>
    public bool IsRowTagged(int rowIndex) => _taggedRowIndexes.Contains(rowIndex);

    [RelayCommand]
    private void ToggleTag(LogEntry? entry)
    {
        if (entry is null || TaggedEntries is null)
        {
            return;
        }

        if (!_taggedRowIndexes.Remove(entry.RowIndex))
        {
            _taggedRowIndexes.Add(entry.RowIndex);
        }

        TaggedEntries.SetFilteredRows(_taggedRowIndexes.OrderBy(row => row).ToList());
    }

    [RelayCommand]
    private void ViewDetail(LogEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        DetailEntry = entry;
        IsDetailPaneOpen = true;
    }

    [RelayCommand]
    private void CloseDetail() => IsDetailPaneOpen = false;

    partial void OnIsLiveTailEnabledChanged(bool value)
    {
        if (value)
        {
            StartTailWatcher();
        }
        else
        {
            StopTailWatcher();
        }
    }

    partial void OnSearchTextChanged(string value) => QueueFilterRefresh();

    partial void OnSelectedSearchFieldChanged(LogSearchField value) => QueueFilterRefresh();

    private void StartTailWatcher()
    {
        if (_filePath is null)
        {
            return;
        }

        StopTailWatcher();
        _tailWatcher = new FileTailWatcherService(_filePath);
        _tailWatcher.FileGrew += OnFileGrew;
    }

    private void StopTailWatcher()
    {
        if (_tailWatcher is null)
        {
            return;
        }

        _tailWatcher.FileGrew -= OnFileGrew;
        _tailWatcher.Dispose();
        _tailWatcher = null;
    }

    private void OnFileGrew(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(async () => await ExtendAndNotifyAsync());

    private async Task ExtendAndNotifyAsync()
    {
        // Guards against overlapping calls: a file rollover can fire several FileSystemWatcher
        // events (delete/rename + create) in quick succession, each posting its own call here.
        // Since this mutates the shared LineIndex/VirtualizedLogCollection across awaits, letting
        // two calls interleave corrupts that shared state (observed as IndexOutOfRangeException
        // in VirtualizedLogCollection). A skipped trigger isn't lost data — the next tick (poll
        // timer or the next watcher event) will pick up wherever this one left off.
        if (_isExtending)
        {
            return;
        }

        if (_filePath is null || _index is null || Entries is null)
        {
            return;
        }

        _isExtending = true;
        try
        {
            var previousCount = _index.Count;
            var wasRebuilt = await _indexService.ExtendAsync(_filePath, _index).ConfigureAwait(true);

            if (wasRebuilt)
            {
                // Row indexes no longer mean the same lines after a rebuild (e.g. rollover), so
                // stale tags would silently point at the wrong rows if kept.
                _taggedRowIndexes.Clear();
                TaggedEntries?.SetFilteredRows(new List<int>());
                await ApplyFilterAsync().ConfigureAwait(true);
                return;
            }

            if (_index.Count == previousCount)
            {
                return;
            }

            if (Entries.HasActiveFilter)
            {
                var enabledLevels = GetEnabledLevels();
                var path = _filePath;
                var index = _index;
                var search = SearchText;
                var searchField = SelectedSearchField;
                var newMatches = await Task.Run(
                    () => _filterService.ComputeMatchingRows(path, index, enabledLevels, search, searchField, previousCount, index.Count))
                    .ConfigureAwait(true);
                Entries.AppendFilteredRows(newMatches);
            }
            else
            {
                Entries.NotifyUnfilteredGrowth();
            }

            UpdateStatus();
        }
        finally
        {
            _isExtending = false;
        }
    }

    private void QueueFilterRefresh()
    {
        _filterDebounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _filterDebounceCts = cts;
        _ = DebouncedApplyFilterAsync(cts.Token);
    }

    private async Task DebouncedApplyFilterAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(FilterDebounceDelay, token).ConfigureAwait(true);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested)
        {
            return;
        }

        await ApplyFilterAsync().ConfigureAwait(true);
    }

    private async Task ApplyFilterAsync()
    {
        if (_filePath is null || _index is null || Entries is null)
        {
            return;
        }

        var enabledLevels = GetEnabledLevels();
        var search = SearchText;
        var searchField = SelectedSearchField;
        var allLevels = enabledLevels.Count >= LevelFilters.Count;
        var noSearch = string.IsNullOrWhiteSpace(search);

        if (allLevels && noSearch)
        {
            Entries.SetFilteredRows(null);
            UpdateStatus();
            return;
        }

        IsBusy = true;
        try
        {
            var path = _filePath;
            var index = _index;
            var matches = await Task.Run(() => _filterService.ComputeMatchingRows(path, index, enabledLevels, search, searchField))
                .ConfigureAwait(true);
            Entries.SetFilteredRows(matches);
        }
        finally
        {
            IsBusy = false;
        }

        UpdateStatus();
    }

    private HashSet<LogLevel> GetEnabledLevels() =>
        LevelFilters.Where(l => l.IsChecked).Select(l => l.Level).ToHashSet();

    private void UpdateStatus()
    {
        if (_index is null || Entries is null)
        {
            StatusText = "No file open";
            return;
        }

        StatusText = Entries.HasActiveFilter
            ? $"{Entries.Count:N0} of {_index.Count:N0} rows    -- {RandomQuote}"
            : $"{_index.Count:N0} rows    -- {RandomQuote}";
    }

    public void Dispose() => StopTailWatcher();
}
