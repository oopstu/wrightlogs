using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WrightLogs.Models;

namespace WrightLogs.Services;

/// <summary>
/// Parses "UsageDetails" files: periodic timestamped blocks listing flow/rule names and how many
/// times each ran. Matches both the ".txt" extension Decisions actually writes and a ".log"
/// variant, current file plus numbered rollover siblings (e.g. "X.UsageDetails.3.txt").
/// </summary>
public sealed class UsageDetailFileParser
{
    private static readonly Regex FamilyMemberPattern = new(
        @"^(?<base>.+)\.UsageDetails(?:\.(?<index>\d+))?\.(?:txt|log)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Finds every UsageDetails file (current + rollovers) for the given base name.</summary>
    public IReadOnlyList<string> FindFamilyFiles(string directory, string baseName)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return Array.Empty<string>();
        }

        var files = new List<string>();
        foreach (var candidate in Directory.EnumerateFiles(directory, $"{baseName}.UsageDetails*"))
        {
            var match = FamilyMemberPattern.Match(Path.GetFileName(candidate));
            if (match.Success && string.Equals(match.Groups["base"].Value, baseName, StringComparison.OrdinalIgnoreCase))
            {
                files.Add(candidate);
            }
        }

        return files;
    }

    /// <summary>Finds every timestamped block within [<paramref name="start"/>, <paramref name="end"/>], across the whole rollover family, chronological order.</summary>
    public async Task<IReadOnlyList<UsageDetailBlock>> FindBlocksInRangeAsync(
        string directory, string baseName, DateTime start, DateTime end, CancellationToken ct = default)
    {
        var blocks = new List<UsageDetailBlock>();

        foreach (var file in FindFamilyFiles(directory, baseName))
        {
            blocks.AddRange(await ParseFileAsync(file, start, end, ct).ConfigureAwait(false));
        }

        return blocks.OrderBy(b => b.Timestamp).ToList();
    }

    private static async Task<List<UsageDetailBlock>> ParseFileAsync(
        string path, DateTime start, DateTime end, CancellationToken ct)
    {
        var blocks = new List<UsageDetailBlock>();
        var lines = await File.ReadAllLinesAsync(path, ct).ConfigureAwait(false);

        DateTime? blockTimestamp = null;
        var blockText = new StringBuilder();

        void Flush()
        {
            if (blockTimestamp is { } timestamp && timestamp >= start && timestamp <= end)
            {
                blocks.Add(new UsageDetailBlock(timestamp, blockText.ToString().TrimEnd(), path));
            }

            blockText.Clear();
        }

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0 &&
                DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
            {
                Flush();
                blockTimestamp = timestamp;
                blockText.AppendLine(trimmed);
                continue;
            }

            if (blockTimestamp is not null)
            {
                blockText.AppendLine(line);
            }
        }

        Flush();
        return blocks;
    }
}
