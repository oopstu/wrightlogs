using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WrightLogs.Services;

/// <summary>
/// Streams a log file once (or incrementally) to record the byte offset of each
/// complete line, without parsing line contents. This is the "virtual" index that
/// backs a <see cref="VirtualizedLogCollection"/>.
/// </summary>
public sealed class LogFileIndexService
{
    private const int BufferSize = 64 * 1024;

    public async Task<LineIndex> BuildAsync(string path, CancellationToken ct = default)
    {
        var index = new LineIndex();
        await ScanAsync(path, index, ct).ConfigureAwait(false);
        return index;
    }

    /// <summary>Extends an existing index with any data appended since it was last scanned.</summary>
    /// <returns>True if the file had shrunk/rolled over and the index was rebuilt from scratch.</returns>
    public Task<bool> ExtendAsync(string path, LineIndex index, CancellationToken ct = default) =>
        ScanAsync(path, index, ct);

    private static async Task<bool> ScanAsync(string path, LineIndex index, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        var wasRebuilt = false;
        if (index.NeedsRebuild(stream.Length))
        {
            index.Reset();
            wasRebuilt = true;
        }

        var currentLineStart = index.NextLineStart;
        stream.Seek(currentLineStart, SeekOrigin.Begin);

        var buffer = new byte[BufferSize];
        var position = currentLineStart;
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, BufferSize), ct).ConfigureAwait(false)) > 0)
        {
            for (var i = 0; i < bytesRead; i++)
            {
                if (buffer[i] == (byte)'\n')
                {
                    index.AddLine(currentLineStart);
                    currentLineStart = position + i + 1;
                }
            }

            position += bytesRead;
        }

        index.NextLineStart = currentLineStart;
        index.ScannedLength = position;
        return wasRebuilt;
    }
}
