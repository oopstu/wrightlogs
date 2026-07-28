using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WrightLogs.Models;

namespace WrightLogs.Services;

/// <summary>Reads and parses individual log lines on demand from a byte range in a file.</summary>
public sealed class LogFileReaderService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public readonly record struct LineRange(int RowIndex, long Start, long End);

    public LogEntry ReadEntry(string path, LineRange range)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return ReadEntryCore(stream, range);
    }

    public List<LogEntry> ReadEntries(string path, IReadOnlyList<LineRange> ranges)
    {
        var results = new List<LogEntry>(ranges.Count);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        foreach (var range in ranges)
        {
            results.Add(ReadEntryCore(stream, range));
        }

        return results;
    }

    private static LogEntry ReadEntryCore(FileStream stream, LineRange range)
    {
        var length = (int)(range.End - range.Start + 1);
        if (length <= 0)
        {
            return LogEntry.CreateParseError(range.RowIndex, string.Empty, "empty line");
        }

        var buffer = new byte[length];
        stream.Seek(range.Start, SeekOrigin.Begin);
        var totalRead = 0;
        while (totalRead < length)
        {
            var read = stream.Read(buffer, totalRead, length - totalRead);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        var rawLine = Encoding.UTF8.GetString(buffer, 0, totalRead).TrimEnd('\r');
        return ParseLine(range.RowIndex, rawLine);
    }

    private static LogEntry ParseLine(int rowIndex, string rawLine)
    {
        if (string.IsNullOrWhiteSpace(rawLine))
        {
            return LogEntry.CreateParseError(rowIndex, rawLine, "empty line");
        }

        try
        {
            var dto = JsonSerializer.Deserialize<LogEntryDto>(rawLine, JsonOptions);
            if (dto is null)
            {
                return LogEntry.CreateParseError(rowIndex, rawLine, "line deserialized to null");
            }

            var properties = dto.Properties ?? new Dictionary<string, JsonElement>();
            var logNumber = GetInt64OrNull(properties, "LogNumber");
            var threadId = GetInt32OrNull(properties, "ThreadId");
            var instanceName = GetString(properties, "InstanceName");
            var sessionId = GetString(properties, "SessionId");
            var category = GetString(properties, "Category");

            var extra = new Dictionary<string, JsonElement>();
            foreach (var (key, value) in properties)
            {
                if (key is "LogNumber" or "ThreadId" or "InstanceName" or "SessionId" or "Category")
                {
                    continue;
                }

                extra[key] = value;
            }

            return new LogEntry
            {
                RowIndex = rowIndex,
                Timestamp = dto.Timestamp,
                Level = LogLevelParser.Parse(dto.Level),
                MessageTemplate = dto.MessageTemplate ?? string.Empty,
                Exception = dto.Exception,
                LogNumber = logNumber,
                ThreadId = threadId,
                InstanceName = instanceName,
                SessionId = sessionId,
                Category = category,
                ExtraProperties = extra,
            };
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            return LogEntry.CreateParseError(rowIndex, rawLine, ex.Message);
        }
    }

    private static long? GetInt64OrNull(IReadOnlyDictionary<string, JsonElement> properties, string key) =>
        properties.TryGetValue(key, out var element) && element.ValueKind == JsonValueKind.Number
            ? element.GetInt64()
            : null;

    private static int? GetInt32OrNull(IReadOnlyDictionary<string, JsonElement> properties, string key) =>
        properties.TryGetValue(key, out var element) && element.ValueKind == JsonValueKind.Number
            ? element.GetInt32()
            : null;

    private static string? GetString(IReadOnlyDictionary<string, JsonElement> properties, string key) =>
        properties.TryGetValue(key, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    private sealed class LogEntryDto
    {
        [JsonPropertyName("Timestamp")]
        public DateTimeOffset Timestamp { get; set; }

        [JsonPropertyName("Level")]
        public string? Level { get; set; }

        [JsonPropertyName("MessageTemplate")]
        public string? MessageTemplate { get; set; }

        [JsonPropertyName("Exception")]
        public string? Exception { get; set; }

        [JsonPropertyName("Properties")]
        public Dictionary<string, JsonElement>? Properties { get; set; }
    }
}
