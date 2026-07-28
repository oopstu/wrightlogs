using WrightLogs.Services;

namespace WrightLogs.Tests;

public class UsageDetailFileParserTests
{
    [Fact]
    public async Task FindBlocksInRangeAsync_OnlyReturnsBlocksWithinRange()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, "App.UsageDetails.txt"), """
                06/17/2026 07:40:00

                -- Flow Starts --
                TooEarlyFlow = 1

                06/17/2026 07:50:00

                -- Flow Starts --
                InRangeFlow = 3

                -- Rule Starts --
                SomeRule = 2

                06/17/2026 08:06:00

                -- Flow Starts --
                TooLateFlow = 1

                """);

            var parser = new UsageDetailFileParser();
            var start = new DateTime(2026, 6, 17, 7, 45, 0);
            var end = new DateTime(2026, 6, 17, 8, 5, 0);

            var blocks = await parser.FindBlocksInRangeAsync(dir.FullName, "App", start, end);

            var block = Assert.Single(blocks);
            Assert.Equal(new DateTime(2026, 6, 17, 7, 50, 0), block.Timestamp);
            Assert.Contains("InRangeFlow = 3", block.Text);
            Assert.Contains("SomeRule = 2", block.Text);
        }
        finally
        {
            Directory.Delete(dir.FullName, recursive: true);
        }
    }

    [Fact]
    public async Task FindBlocksInRangeAsync_SearchesAcrossRolloverSiblings()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, "App.UsageDetails.txt"), """
                06/17/2026 09:00:00

                -- Flow Starts --
                CurrentFileFlow = 1

                """);
            File.WriteAllText(Path.Combine(dir.FullName, "App.UsageDetails.1.txt"), """
                06/17/2026 08:59:00

                -- Flow Starts --
                RolloverFileFlow = 1

                """);

            var parser = new UsageDetailFileParser();
            var start = new DateTime(2026, 6, 17, 8, 49, 30);
            var end = new DateTime(2026, 6, 17, 9, 9, 30);

            var blocks = await parser.FindBlocksInRangeAsync(dir.FullName, "App", start, end);

            Assert.Equal(2, blocks.Count);
            Assert.Contains(blocks, b => b.Text.Contains("RolloverFileFlow"));
            Assert.Contains(blocks, b => b.Text.Contains("CurrentFileFlow"));
            // chronological order
            Assert.True(blocks[0].Timestamp <= blocks[1].Timestamp);
        }
        finally
        {
            Directory.Delete(dir.FullName, recursive: true);
        }
    }

    [Fact]
    public async Task FindBlocksInRangeAsync_ParsesTwelveHourTimestampsWithAmPm()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, "App.UsageDetails.txt"), """

                7/2/2026 9:42:53 AM

                -- Flow Starts --
                AfterUnitTestFlow = 1

                """);

            var parser = new UsageDetailFileParser();
            var start = new DateTime(2026, 7, 2, 9, 35, 0);
            var end = new DateTime(2026, 7, 2, 9, 55, 0);

            var blocks = await parser.FindBlocksInRangeAsync(dir.FullName, "App", start, end);

            var block = Assert.Single(blocks);
            Assert.Equal(new DateTime(2026, 7, 2, 9, 42, 53), block.Timestamp);
        }
        finally
        {
            Directory.Delete(dir.FullName, recursive: true);
        }
    }
}
