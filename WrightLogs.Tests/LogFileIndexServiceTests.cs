using WrightLogs.Services;

namespace WrightLogs.Tests;

public class LogFileIndexServiceTests
{
    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Theory]
    [InlineData("Decisions.Web.Core.log")]
    [InlineData("Decisions.Web.Core.1.log")]
    public async Task BuildAsync_IndexesEveryCompleteLine(string fixtureName)
    {
        var path = FixturePath(fixtureName);
        var expectedLineCount = (await File.ReadAllLinesAsync(path)).Length;

        var index = await new LogFileIndexService().BuildAsync(path);

        Assert.Equal(expectedLineCount, index.Count);
    }

    [Fact]
    public async Task BuildAsync_DoesNotIndexAnInProgressFinalLine()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"wrightlogs-partial-{Guid.NewGuid():N}.log");
        try
        {
            await File.WriteAllTextAsync(tempPath, "{\"a\":1}\n{\"a\":2}\n{\"a\":3, still writing this one");

            var index = await new LogFileIndexService().BuildAsync(tempPath);

            Assert.Equal(2, index.Count);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task ExtendAsync_PicksUpAppendedLines()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"wrightlogs-tail-{Guid.NewGuid():N}.log");
        try
        {
            await File.WriteAllTextAsync(tempPath, "{\"a\":1}\n{\"a\":2}\n");

            var indexService = new LogFileIndexService();
            var index = await indexService.BuildAsync(tempPath);
            Assert.Equal(2, index.Count);

            await File.AppendAllTextAsync(tempPath, "{\"a\":3}\n{\"a\":4}\n");
            var wasRebuilt = await indexService.ExtendAsync(tempPath, index);

            Assert.False(wasRebuilt);
            Assert.Equal(4, index.Count);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task ExtendAsync_RebuildsWhenFileShrinks()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"wrightlogs-rollover-{Guid.NewGuid():N}.log");
        try
        {
            await File.WriteAllTextAsync(tempPath, "{\"a\":1}\n{\"a\":2}\n{\"a\":3}\n");

            var indexService = new LogFileIndexService();
            var index = await indexService.BuildAsync(tempPath);
            Assert.Equal(3, index.Count);

            // Simulate a rollover: the file is truncated/replaced with a fresh, shorter file.
            await File.WriteAllTextAsync(tempPath, "{\"a\":1}\n");
            var wasRebuilt = await indexService.ExtendAsync(tempPath, index);

            Assert.True(wasRebuilt);
            Assert.Equal(1, index.Count);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}
