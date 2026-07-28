using System;

namespace WrightLogs.Models;

/// <summary>One timestamped block from a UsageDetails file (flow/rule names and their run counts).</summary>
public sealed record UsageDetailBlock(DateTime Timestamp, string Text, string SourceFile);
