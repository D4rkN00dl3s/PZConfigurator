using PZConfigurator.ViewModels;
using Xunit;

namespace PZConfigurator.Tests;

public class FilterTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "pzcfg-flt-" + Guid.NewGuid().ToString("N"));

    public FilterTests()
    {
        Directory.CreateDirectory(_dir);
        foreach (var name in new[] { "myserver.ini", "myserver_SandboxVars.lua" })
            File.Copy(
                Path.Combine(AppContext.BaseDirectory, "TestData", name),
                Path.Combine(_dir, name));
    }

    public void Dispose() => Directory.Delete(_dir, true);

    private static int Visible(MainViewModel vm) =>
        vm.IniGroups.Concat(vm.SandboxGroups).Sum(g => g.Items.Count(i => i.IsVisible));

    [Fact]
    public async Task OnlyModified_FilterShowsChangedRows_Only()
    {
        var vm = new MainViewModel();
        await vm.SetFolder(_dir);
        var allVisible = Visible(vm);
        Assert.True(allVisible > 0);

        // change exactly two settings
        var modified = vm.SandboxGroups.SelectMany(g => g.Items)
            .OfType<BoolSettingVM>().Where(b => b.IsVisible).Take(2).ToList();
        foreach (var m in modified)
            m.IsChecked = !m.IsChecked;

        vm.OnlyModified = true;
        Assert.Equal(2, Visible(vm));
        Assert.True(vm.IsSearching); // detail pane switches to matches view
        Assert.Contains("modified settings only", vm.Status);

        // search narrows to the modified set; clearing the search keeps the filter on
        vm.SearchText = modified[0].Key[..6];
        Assert.True(Visible(vm) >= 1);
        Assert.Contains("Searching", vm.Status);
        vm.SearchText = "";
        Assert.Equal(2, Visible(vm));
        Assert.Contains("modified settings only", vm.Status);

        // filter off -> everything visible again
        vm.OnlyModified = false;
        Assert.Equal(allVisible, Visible(vm));
        Assert.Contains("Loaded myserver", vm.Status);
    }

    [Fact]
    public async Task Save_ClearsModifiedFilterTargets()
    {
        var vm = new MainViewModel();
        await vm.SetFolder(_dir);
        var m = vm.SandboxGroups.SelectMany(g => g.Items)
            .OfType<BoolSettingVM>().First(b => b.IsVisible);
        m.IsChecked = !m.IsChecked;

        vm.OnlyModified = true;
        Assert.Equal(1, Visible(vm));

        vm.SaveCommand.Execute(null);

        Assert.Equal(0, vm.SandboxGroups.Sum(g => g.ModifiedCount)
            + vm.IniGroups.Sum(g => g.ModifiedCount)); // badges reset
    }

    [Fact]
    public void Status_SeveritySniffs()
    {
        var vm = new MainViewModel();
        vm.Status = "Loaded myserver: 580 settings";
        Assert.False(vm.IsStatusError);
        vm.Status = "Error saving: oops";
        Assert.True(vm.IsStatusError);
        vm.Status = "x changed on disk since loading (server restart?)";
        Assert.True(vm.IsStatusError);
        vm.Status = "Applied \"Apocalypse\": 265 values set, 6 not found — review, then Save";
        Assert.False(vm.IsStatusError);
        Assert.True(vm.IsStatusWarn);
    }
}
