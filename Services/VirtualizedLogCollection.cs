using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using WrightLogs.Models;

namespace WrightLogs.Services;

/// <summary>
/// Lazily-loading, read-only view of a log file's rows, backed by a <see cref="LineIndex"/>.
/// Implements <see cref="IList"/> (not just IEnumerable) so Avalonia's DataGrid can access
/// rows by index directly instead of materializing the whole collection.
///
/// This type only stores/exposes data; it does not compute filters itself and does not
/// touch the UI thread. Callers (typically a ViewModel) compute filter results off-thread
/// via <see cref="LogFilterService"/> and then hand the result to <see cref="SetFilteredRows"/>
/// or <see cref="AppendFilteredRows"/> on the UI thread, since those raise
/// <see cref="CollectionChanged"/>.
/// </summary>
public sealed class VirtualizedLogCollection : IList, IList<LogEntry>, INotifyCollectionChanged, INotifyPropertyChanged
{
    private const int PageSize = 200;
    private const int MaxCachedPages = 64;

    private readonly string _path;
    private readonly LogFileReaderService _reader;
    private readonly LineIndex _index;

    private readonly Dictionary<int, LogEntry[]> _pageCache = new();
    private readonly LinkedList<int> _pageLru = new();

    private List<int>? _filteredRows;

    public VirtualizedLogCollection(string path, LineIndex index, LogFileReaderService reader)
    {
        _path = path;
        _index = index;
        _reader = reader;
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public int Count => _filteredRows?.Count ?? _index.Count;

    public bool HasActiveFilter => _filteredRows is not null;

    /// <summary>Replace the active filter. Pass null to clear filtering entirely.</summary>
    public void SetFilteredRows(List<int>? filteredRows)
    {
        _filteredRows = filteredRows;
        _pageCache.Clear();
        _pageLru.Clear();
        NotifyReset();
    }

    /// <summary>Append newly-matching rows to an already-active filter (no-op if unfiltered).</summary>
    public void AppendFilteredRows(List<int> newMatchingRows)
    {
        if (_filteredRows is null || newMatchingRows.Count == 0)
        {
            return;
        }

        _filteredRows.AddRange(newMatchingRows);
        NotifyReset();
    }

    /// <summary>Call after the backing <see cref="LineIndex"/> grows and there is no active filter.</summary>
    public void NotifyUnfilteredGrowth() => NotifyReset();

    private void NotifyReset()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public LogEntry this[int displayIndex]
    {
        get
        {
            if (displayIndex < 0 || displayIndex >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(displayIndex));
            }

            var underlyingRow = _filteredRows?[displayIndex] ?? displayIndex;
            return GetByUnderlyingRow(underlyingRow);
        }
        set => throw new NotSupportedException();
    }

    private LogEntry GetByUnderlyingRow(int row)
    {
        var pageIndex = row / PageSize;
        if (!_pageCache.TryGetValue(pageIndex, out var page))
        {
            page = LoadPage(pageIndex);
            CachePage(pageIndex, page);
        }
        else
        {
            _pageLru.Remove(pageIndex);
            _pageLru.AddLast(pageIndex);
        }

        return page[row - (pageIndex * PageSize)];
    }

    private LogEntry[] LoadPage(int pageIndex)
    {
        var start = pageIndex * PageSize;
        var end = Math.Min(start + PageSize, _index.Count);
        var ranges = new List<LogFileReaderService.LineRange>(end - start);
        for (var row = start; row < end; row++)
        {
            ranges.Add(new LogFileReaderService.LineRange(row, _index.GetLineStart(row), _index.GetLineEnd(row)));
        }

        return _reader.ReadEntries(_path, ranges).ToArray();
    }

    private void CachePage(int pageIndex, LogEntry[] page)
    {
        _pageCache[pageIndex] = page;
        _pageLru.AddLast(pageIndex);

        while (_pageCache.Count > MaxCachedPages)
        {
            var oldest = _pageLru.First!.Value;
            _pageLru.RemoveFirst();
            _pageCache.Remove(oldest);
        }
    }

    // ---- IEnumerable / IList plumbing (read-only; mutation throws) ----

    public IEnumerator<LogEntry> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool IsReadOnly => true;
    public bool IsFixedSize => false;
    public bool IsSynchronized => false;
    public object SyncRoot { get; } = new();

    object? IList.this[int index]
    {
        get => this[index];
        set => throw new NotSupportedException();
    }

    public void CopyTo(LogEntry[] array, int arrayIndex)
    {
        for (var i = 0; i < Count; i++)
        {
            array[arrayIndex + i] = this[i];
        }
    }

    public void CopyTo(Array array, int index)
    {
        for (var i = 0; i < Count; i++)
        {
            array.SetValue(this[i], index + i);
        }
    }

    public bool Contains(LogEntry item) => IndexOf(item) >= 0;

    public bool Contains(object? value) => value is LogEntry entry && Contains(entry);

    public int IndexOf(LogEntry item)
    {
        for (var i = 0; i < Count; i++)
        {
            if (ReferenceEquals(this[i], item))
            {
                return i;
            }
        }

        return -1;
    }

    public int IndexOf(object? value) => value is LogEntry entry ? IndexOf(entry) : -1;

    public int Add(object? value) => throw new NotSupportedException();
    public void Add(LogEntry item) => throw new NotSupportedException();
    public void Insert(int index, object? value) => throw new NotSupportedException();
    public void Insert(int index, LogEntry item) => throw new NotSupportedException();
    public void Remove(object? value) => throw new NotSupportedException();
    public bool Remove(LogEntry item) => throw new NotSupportedException();
    public void RemoveAt(int index) => throw new NotSupportedException();
    public void Clear() => throw new NotSupportedException();
}
