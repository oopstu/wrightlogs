using WrightLogs.Services;

namespace WrightLogs.Tests;

public class UsageFileParserTests
{
    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public void ResolveFamily_FindsBothRolloverSiblings_OldestFirst()
    {
        var path = FixturePath("Decisions.Web.Core.Usage.csv");

        var family = new UsageFileParser().ResolveFamily(path);

        Assert.Equal(2, family.FilesOldestFirst.Count);
        Assert.EndsWith("Decisions.Web.Core.Usage.1.csv", family.FilesOldestFirst[0]);
        Assert.EndsWith("Decisions.Web.Core.Usage.csv", family.FilesOldestFirst[1]);
    }

    [Fact]
    public async Task ParseFamilyAsync_CombinesBothFiles_InChronologicalOrder()
    {
        var path = FixturePath("Decisions.Web.Core.Usage.csv");

        var result = await new UsageFileParser().ParseFamilyAsync(path);

        Assert.Equal(2, result.SourceFiles.Count);
        Assert.Equal(762, result.Samples.Count); // 754 rows from .1.csv + 8 rows from the current file

        for (var i = 1; i < result.Samples.Count; i++)
        {
            Assert.True(result.Samples[i].When >= result.Samples[i - 1].When, "samples must be in chronological order");
        }
    }

    [Fact]
    public async Task ParseFamilyAsync_OmitsBlankCells_RatherThanZeroingThem()
    {
        var path = FixturePath("Decisions.Web.Core.Usage.csv");

        var result = await new UsageFileParser().ParseFamilyAsync(path);

        // "StoredFlowCount" is blank on every example row.
        Assert.Contains("StoredFlowCount", result.MetricNames);
        Assert.DoesNotContain(result.Samples, s => s.Metrics.ContainsKey("StoredFlowCount"));

        // "QueuedThreadJobs" (trailing space in the header) always has a value.
        Assert.All(result.Samples, s => Assert.True(s.Metrics.ContainsKey("QueuedThreadJobs")));
    }
}
