using System;
using System.Collections.Generic;
using System.Text.Json;

namespace WrightLogs.Models;

public sealed class LogEntry
{
    public required int RowIndex { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public LogLevel Level { get; init; }
    public string MessageTemplate { get; init; } = string.Empty;
    public string? Exception { get; init; }

    // Well-known "Properties" sub-elements, promoted to first-class columns.
    // Nullable/blank when the source line lacks "Properties" or the specific element.
    public long? LogNumber { get; init; }
    public int? ThreadId { get; init; }
    public string? InstanceName { get; init; }
    public string? SessionId { get; init; }
    public string? Category { get; init; }

    public IReadOnlyDictionary<string, JsonElement> ExtraProperties { get; init; }
        = new Dictionary<string, JsonElement>();

    public bool IsParseError { get; init; }
    public string? RawLine { get; init; }

    public static LogEntry CreateParseError(int rowIndex, string rawLine, string error) => new()
    {
        RowIndex = rowIndex,
        Timestamp = DateTimeOffset.MinValue,
        Level = LogLevel.Error,
        MessageTemplate = $"[Failed to parse log line: {error}]",
        RawLine = rawLine,
        IsParseError = true,
    };
}
