using System;
using System.IO;
using System.Threading;

namespace WrightLogs.Services;

/// <summary>
/// Detects growth of a log file so it can be tailed live. Uses a <see cref="FileSystemWatcher"/>
/// with a polling fallback, since watchers are unreliable on some volumes (network shares, etc.).
/// Raises <see cref="FileGrew"/> from a background thread pool thread; subscribers must marshal
/// back to the UI thread themselves.
/// </summary>
public sealed class FileTailWatcherService : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private readonly string _path;
    private readonly FileSystemWatcher? _watcher;
    private readonly Timer _pollTimer;
    private long _lastKnownLength;
    private bool _disposed;

    public event EventHandler? FileGrew;

    public FileTailWatcherService(string path)
    {
        _path = path;
        _lastKnownLength = File.Exists(path) ? new FileInfo(path).Length : 0;

        var directory = Path.GetDirectoryName(path);
        var fileName = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
        {
            _watcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.Size | NotifyFilters.LastWrite,
            };
            _watcher.Changed += OnWatcherEvent;
            _watcher.Created += OnWatcherEvent;
            _watcher.Renamed += OnWatcherEvent;
            _watcher.EnableRaisingEvents = true;
        }

        _pollTimer = new Timer(_ => CheckForChanges(), null, PollInterval, PollInterval);
    }

    private void OnWatcherEvent(object sender, FileSystemEventArgs e) => CheckForChanges();

    private void CheckForChanges()
    {
        if (_disposed || !File.Exists(_path))
        {
            return;
        }

        var length = new FileInfo(_path).Length;
        if (length == _lastKnownLength)
        {
            return;
        }

        _lastKnownLength = length;
        FileGrew?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _disposed = true;
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
        }

        _pollTimer.Dispose();
    }
}
