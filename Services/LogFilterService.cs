using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WrightLogs.Models;

namespace WrightLogs.Services;

/// <summary>
/// Computes which rows match a level/search filter by scanning raw line bytes
/// sequentially (a cheap string search), without deserializing JSON.
/// </summary>
public sealed class LogFilterService
{
    public List<int> ComputeMatchingRows(
        string path,
        LineIndex index,
        IReadOnlySet<LogLevel> enabledLevels,
        string? searchText,
        LogSearchField searchField = LogSearchField.All,
        int fromRow = 0,
        int? toRowExclusive = null)
    {
        var matches = new List<int>();
        var upperBound = toRowExclusive ?? index.Count;
        if (fromRow >= upperBound)
        {
            return matches;
        }

        var hasSearch = !string.IsNullOrEmpty(searchText);
        var allLevelsEnabled = enabledLevels.Count >= Enum.GetValues<LogLevel>().Length;

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        long streamPosition = 0;

        for (var row = fromRow; row < upperBound; row++)
        {
            var start = index.GetLineStart(row);
            var end = index.GetLineEnd(row);
            var length = (int)(end - start + 1);
            if (length <= 0)
            {
                continue;
            }

            if (start != streamPosition)
            {
                stream.Seek(start, SeekOrigin.Begin);
            }

            var buffer = new byte[length];
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

            streamPosition = start + totalRead;

            var line = Encoding.UTF8.GetString(buffer, 0, totalRead);

            if (!allLevelsEnabled && !enabledLevels.Contains(ExtractLevel(line)))
            {
                continue;
            }

            if (hasSearch && !FieldMatches(line, searchField, searchText!))
            {
                continue;
            }

            matches.Add(row);
        }

        return matches;
    }

    private static bool FieldMatches(string line, LogSearchField searchField, string searchText)
    {
        if (searchField == LogSearchField.All)
        {
            return line.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        var fieldName = FieldNameFor(searchField);
        var value = fieldName is null ? null : ExtractFieldValue(line, fieldName);
        return value is not null && value.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string? FieldNameFor(LogSearchField field) => field switch
    {
        LogSearchField.Message => "MessageTemplate",
        LogSearchField.Category => "Category",
        LogSearchField.InstanceName => "InstanceName",
        LogSearchField.SessionId => "SessionId",
        LogSearchField.ThreadId => "ThreadId",
        LogSearchField.LogNumber => "LogNumber",
        _ => null,
    };

    /// <summary>
    /// Pulls a single field's raw value out of a JSON log line by locating its
    /// "Name": marker directly, without deserializing the whole line.
    /// </summary>
    private static string? ExtractFieldValue(string line, string fieldName)
    {
        var marker = $"\"{fieldName}\":";
        var idx = line.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
        {
            return null;
        }

        var pos = idx + marker.Length;
        if (pos >= line.Length)
        {
            return null;
        }

        if (line[pos] == '"')
        {
            pos++;
            var sb = new StringBuilder();
            for (; pos < line.Length; pos++)
            {
                var c = line[pos];
                if (c == '\\' && pos + 1 < line.Length)
                {
                    sb.Append(line[pos + 1]);
                    pos++;
                    continue;
                }

                if (c == '"')
                {
                    return sb.ToString();
                }

                sb.Append(c);
            }

            return null;
        }

        var numStart = pos;
        while (pos < line.Length && (char.IsDigit(line[pos]) || line[pos] == '-'))
        {
            pos++;
        }

        return pos > numStart ? line[numStart..pos] : null;
    }

    private static LogLevel ExtractLevel(string line)
    {
        const string marker = "\"Level\":\"";
        var idx = line.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
        {
            return LogLevel.Information;
        }

        var start = idx + marker.Length;
        var end = line.IndexOf('"', start);
        return end < 0 ? LogLevel.Information : LogLevelParser.Parse(line[start..end]);
    }
}
