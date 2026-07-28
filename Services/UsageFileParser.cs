using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WrightLogs.Models;

namespace WrightLogs.Services;

/// <summary>A Usage CSV file family (a rollover chain), ordered oldest file first.</summary>
public sealed record UsageFileFamily(string BaseName, IReadOnlyList<string> FilesOldestFirst);

public sealed record UsageParseResult(
    IReadOnlyList<UsageSample> Samples,
    IReadOnlyList<string> MetricNames,
    IReadOnlyList<string> SourceFiles);

/// <summary>
/// Parses "Usage" heartbeat CSV files and stitches a file together with its rollover
/// siblings (e.g. "X.Usage.csv" + "X.Usage.1.csv") into one chronological series.
/// Usage files are small, so unlike log files this reads everything into memory.
/// </summary>
public sealed class UsageFileParser
{
    private static readonly Regex FamilyMemberPattern =
        new(@"^(?<base>.+)\.Usage(?:\.(?<index>\d+))?\.csv$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Finds every rollover sibling of <paramref name="anyMemberPath"/>, oldest first.</summary>
    public UsageFileFamily ResolveFamily(string anyMemberPath)
    {
        var directory = Path.GetDirectoryName(anyMemberPath);
        var fileName = Path.GetFileName(anyMemberPath);
        var match = FamilyMemberPattern.Match(fileName);

        if (!match.Success || string.IsNullOrEmpty(directory))
        {
            return new UsageFileFamily(fileName, new[] { anyMemberPath });
        }

        var baseName = match.Groups["base"].Value;
        var indexed = new List<(int Index, string Path)>();
        foreach (var candidate in Directory.EnumerateFiles(directory, $"{baseName}.Usage*.csv"))
        {
            var candidateMatch = FamilyMemberPattern.Match(Path.GetFileName(candidate));
            if (!candidateMatch.Success ||
                !string.Equals(candidateMatch.Groups["base"].Value, baseName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var index = candidateMatch.Groups["index"].Success
                ? int.Parse(candidateMatch.Groups["index"].Value, CultureInfo.InvariantCulture)
                : 0;
            indexed.Add((index, candidate));
        }

        // Higher index = older rollover; index 0 (no suffix) = current file. Oldest first for a
        // chronological series.
        var orderedOldestFirst = indexed.OrderByDescending(x => x.Index).Select(x => x.Path).ToList();
        return new UsageFileFamily(baseName, orderedOldestFirst);
    }

    /// <summary>Resolves and parses a file's whole rollover family into one chronological result.</summary>
    public async Task<UsageParseResult> ParseFamilyAsync(string anyMemberPath, CancellationToken ct = default)
    {
        var family = ResolveFamily(anyMemberPath);
        var samples = new List<UsageSample>();
        var metricNames = new List<string>();
        var seenMetrics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in family.FilesOldestFirst)
        {
            var (fileSamples, fileMetrics) = await ParseFileAsync(file, ct).ConfigureAwait(false);
            samples.AddRange(fileSamples);
            foreach (var metric in fileMetrics)
            {
                if (seenMetrics.Add(metric))
                {
                    metricNames.Add(metric);
                }
            }
        }

        return new UsageParseResult(samples, metricNames, family.FilesOldestFirst);
    }

    /// <summary>Parses exactly one file, without pulling in its rollover siblings.</summary>
    public async Task<UsageParseResult> ParseSingleFileAsync(string path, CancellationToken ct = default)
    {
        var (samples, metricNames) = await ParseFileAsync(path, ct).ConfigureAwait(false);
        return new UsageParseResult(samples, metricNames, new[] { path });
    }

    private static async Task<(List<UsageSample> Samples, List<string> MetricNames)> ParseFileAsync(
        string path, CancellationToken ct)
    {
        var samples = new List<UsageSample>();
        using var reader = new StreamReader(path);

        var headerLine = await reader.ReadLineAsync(ct).ConfigureAwait(false);
        if (headerLine is null)
        {
            return (samples, new List<string>());
        }

        var headers = headerLine.Split(',').Select(h => h.Trim()).ToArray();
        var metricNames = headers.Skip(1).Where(h => h.Length > 0).ToList();

        string? line;
        while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cells = line.Split(',');
            if (cells.Length == 0 ||
                !DateTime.TryParse(cells[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var when))
            {
                continue;
            }

            var metrics = new Dictionary<string, double>();
            for (var i = 1; i < headers.Length && i < cells.Length; i++)
            {
                if (headers[i].Length == 0)
                {
                    continue;
                }

                if (double.TryParse(cells[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    metrics[headers[i]] = value;
                }
            }

            samples.Add(new UsageSample { When = when, Metrics = metrics });
        }

        return (samples, metricNames);
    }
}
