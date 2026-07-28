using System.Collections.Generic;

namespace WrightLogs.Services;

/// <summary>
/// Byte-offset index of complete (newline-terminated) lines in a file.
/// A line is only added once its trailing newline has been observed, so an
/// in-progress final line (still being written) never shows up as a row.
/// </summary>
public sealed class LineIndex
{
    private readonly List<long> _lineStarts = new();

    /// <summary>Offset where the next (possibly still-incomplete) line begins.</summary>
    public long NextLineStart { get; internal set; }

    /// <summary>How far into the file scanning has progressed.</summary>
    public long ScannedLength { get; internal set; }

    public int Count => _lineStarts.Count;

    public long GetLineStart(int rowIndex) => _lineStarts[rowIndex];

    public long GetLineEnd(int rowIndex) =>
        rowIndex + 1 < _lineStarts.Count ? _lineStarts[rowIndex + 1] - 1 : ScannedLength - 1;

    /// <summary>True if the underlying file has shrunk/rolled and a full rebuild is required.</summary>
    public bool NeedsRebuild(long currentFileLength) => currentFileLength < ScannedLength;

    internal void AddLine(long start) => _lineStarts.Add(start);

    internal void Reset()
    {
        _lineStarts.Clear();
        NextLineStart = 0;
        ScannedLength = 0;
    }
}
