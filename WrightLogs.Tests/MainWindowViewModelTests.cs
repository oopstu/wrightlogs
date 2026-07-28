using WrightLogs.Models;
using WrightLogs.ViewModels;

namespace WrightLogs.Tests;

public class MainWindowViewModelTests
{
    [Fact]
    public void SetFolder_AddsAllUsageEntry_WhenUsageFamilyHasRollovers()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, "App.Usage.csv"), string.Empty);
            File.WriteAllText(Path.Combine(dir.FullName, "App.Usage.1.csv"), string.Empty);
            File.WriteAllText(Path.Combine(dir.FullName, "App.log"), string.Empty);

            var vm = new MainWindowViewModel();
            vm.SetFolder(dir.FullName);

            Assert.Contains(vm.FolderFiles, f => f.Kind == FolderFileKind.UsageAll && f.FileName == "All Usage");
            Assert.Equal(2, vm.FolderFiles.Count(f => f.Kind == FolderFileKind.Usage));
            Assert.Single(vm.FolderFiles, f => f.Kind == FolderFileKind.Log);
        }
        finally
        {
            Directory.Delete(dir.FullName, recursive: true);
        }
    }

    [Fact]
    public void SetFolder_NoAllUsageEntry_WhenUsageFamilyHasNoRollovers()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, "App.Usage.csv"), string.Empty);

            var vm = new MainWindowViewModel();
            vm.SetFolder(dir.FullName);

            Assert.DoesNotContain(vm.FolderFiles, f => f.Kind == FolderFileKind.UsageAll);
        }
        finally
        {
            Directory.Delete(dir.FullName, recursive: true);
        }
    }
}
