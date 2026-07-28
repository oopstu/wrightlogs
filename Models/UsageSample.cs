using System;
using System.Collections.Generic;

namespace WrightLogs.Models;

/// <summary>One 30-second heartbeat row from a Usage CSV file.</summary>
public sealed class UsageSample
{
    public required DateTime When { get; init; }

    /// <summary>Metric name -&gt; value. A metric absent from this row's CSV cell is simply not a key here.</summary>
    public IReadOnlyDictionary<string, double> Metrics { get; init; } = new Dictionary<string, double>();
}
