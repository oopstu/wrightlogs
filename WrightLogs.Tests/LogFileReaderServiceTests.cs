using WrightLogs.Models;
using WrightLogs.Services;

namespace WrightLogs.Tests;

public class LogFileReaderServiceTests
{
    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public async Task ReadEntry_ParsesKnownFirstLine()
    {
        var path = FixturePath("Decisions.Web.Core.log");
        var index = await new LogFileIndexService().BuildAsync(path);
        var range = new LogFileReaderService.LineRange(0, index.GetLineStart(0), index.GetLineEnd(0));

        var entry = new LogFileReaderService().ReadEntry(path, range);

        Assert.False(entry.IsParseError);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(1, entry.LogNumber);
        Assert.Equal(1, entry.ThreadId);
        Assert.Contains("MachineName", entry.MessageTemplate);
    }

    [Fact]
    public async Task ReadEntries_PromotesPropertiesSubElements_AndLeavesMissingOnesNull()
    {
        var path = FixturePath("Decisions.Web.Core.log");
        var index = await new LogFileIndexService().BuildAsync(path);
        var reader = new LogFileReaderService();

        var ranges = Enumerable.Range(0, index.Count)
            .Select(row => new LogFileReaderService.LineRange(row, index.GetLineStart(row), index.GetLineEnd(row)))
            .ToList();
        var entries = reader.ReadEntries(path, ranges);

        // First line's Properties has no InstanceName/SessionId -> should be null, not throw/default.
        var first = entries[0];
        Assert.Null(first.InstanceName);
        Assert.Null(first.SessionId);
        Assert.Equal(1, first.LogNumber);

        // A later line does carry InstanceName/SessionId -> should be populated.
        var withInstance = entries.FirstOrDefault(e => e.InstanceName is not null);
        Assert.NotNull(withInstance);
        Assert.False(string.IsNullOrEmpty(withInstance!.SessionId));
    }

    [Fact]
    public void ReadEntry_ReturnsParseErrorForMalformedJson_WithoutThrowing()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"wrightlogs-malformed-{Guid.NewGuid():N}.log");
        try
        {
            const string rawLine = "{ this is not valid json";
            File.WriteAllText(tempPath, rawLine + "\n");

            var range = new LogFileReaderService.LineRange(0, 0, rawLine.Length - 1);
            var entry = new LogFileReaderService().ReadEntry(tempPath, range);

            Assert.True(entry.IsParseError);
            Assert.Equal(rawLine, entry.RawLine);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task ReadEntries_CapturesExceptionField_ForErrorRows()
    {
        var path = FixturePath("Decisions.Web.Core.1.log");
        var index = await new LogFileIndexService().BuildAsync(path);
        var reader = new LogFileReaderService();

        var ranges = Enumerable.Range(0, index.Count)
            .Select(row => new LogFileReaderService.LineRange(row, index.GetLineStart(row), index.GetLineEnd(row)))
            .ToList();
        var entries = reader.ReadEntries(path, ranges);

        var errorWithException = entries.FirstOrDefault(e => e.Exception is not null);
        Assert.NotNull(errorWithException);
        Assert.Equal(LogLevel.Error, errorWithException!.Level);
    }
}
