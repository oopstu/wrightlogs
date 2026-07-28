using WrightLogs.ViewModels;

namespace WrightLogs.Tests;

public class LogViewerViewModelTaggingTests
{
    private static string WriteTempLog(params string[] messages)
    {
        var path = Path.Combine(Path.GetTempPath(), $"wrightlogs-tagging-{Guid.NewGuid():N}.log");
        var lines = messages.Select((m, i) =>
            $"{{\"Timestamp\":\"2026-07-02T09:00:0{i}.0000000-04:00\",\"Level\":\"Information\",\"MessageTemplate\":\"{m}\",\"Properties\":{{\"LogNumber\":{i + 1}}}}}");
        File.WriteAllText(path, string.Join("\n", lines) + "\n");
        return path;
    }

    [Fact]
    public async Task ToggleTag_AddsAndRemovesRow_FromTaggedEntries()
    {
        var path = WriteTempLog("first", "second", "third");
        try
        {
            var vm = new LogViewerViewModel();
            await vm.OpenFileAsync(path);

            var second = vm.Entries![1];
            Assert.False(vm.IsRowTagged(second.RowIndex));

            vm.ToggleTagCommand.Execute(second);
            Assert.True(vm.IsRowTagged(second.RowIndex));
            Assert.Single(vm.TaggedEntries!);
            Assert.Equal("second", vm.TaggedEntries![0].MessageTemplate);

            vm.ToggleTagCommand.Execute(second);
            Assert.False(vm.IsRowTagged(second.RowIndex));
            Assert.Empty(vm.TaggedEntries!);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task TaggedEntries_AreOrderedByRow_RegardlessOfTagOrder()
    {
        var path = WriteTempLog("first", "second", "third");
        try
        {
            var vm = new LogViewerViewModel();
            await vm.OpenFileAsync(path);

            // Tag out of order: third, then first.
            vm.ToggleTagCommand.Execute(vm.Entries![2]);
            vm.ToggleTagCommand.Execute(vm.Entries![0]);

            Assert.Equal(2, vm.TaggedEntries!.Count);
            Assert.Equal("first", vm.TaggedEntries![0].MessageTemplate);
            Assert.Equal("third", vm.TaggedEntries![1].MessageTemplate);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task OpeningANewFile_ClearsPreviousTags()
    {
        var pathA = WriteTempLog("a1", "a2");
        var pathB = WriteTempLog("b1", "b2");
        try
        {
            var vm = new LogViewerViewModel();
            await vm.OpenFileAsync(pathA);
            vm.ToggleTagCommand.Execute(vm.Entries![0]);
            Assert.Single(vm.TaggedEntries!);

            await vm.OpenFileAsync(pathB);

            Assert.Empty(vm.TaggedEntries!);
            Assert.False(vm.IsRowTagged(0));
        }
        finally
        {
            File.Delete(pathA);
            File.Delete(pathB);
        }
    }
}
