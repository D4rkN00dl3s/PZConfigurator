using System.Collections.ObjectModel;
using PZConfigurator.Config;
using PZConfigurator.ViewModels;
using Xunit;

namespace PZConfigurator.Tests;

public class MainViewModelTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "pzcfg-vm-" + Guid.NewGuid().ToString("N"));

    // two minimal fake servers so the VM has something to select
    private static readonly string IniText = "PVP=false\r\nMaxPlayers=32\r\n";
    private static readonly string LuaText = "SandboxVars = {\r\n    VERSION = 6,\r\n}\r\n";

    private static void WriteServer(string dir, string name)
    {
        File.WriteAllText(Path.Combine(dir, name + ".ini"), IniText);
        File.WriteAllText(Path.Combine(dir, name + "_SandboxVars.lua"), LuaText);
    }

    public MainViewModelTests()
    {
        Directory.CreateDirectory(_dir);
        WriteServer(_dir, "a");
        WriteServer(_dir, "b");
    }

    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public async Task SwitchingServerWhileDirty_RevertsWhenDeclined()
    {
        var vm = new MainViewModel(() => Task.FromResult(false));
        await vm.SetFolder(_dir); // fresh folder, no edits pending
        var first = vm.SelectedServer;
        Assert.Equal("a", vm.LoadedServer);

        // dirty up the state: change a setting value through its VM
        var number = vm.IniGroups.SelectMany(g => g.Items)
            .OfType<NumberSettingVM>().First(n => n.Key == "MaxPlayers");
        number.NumberValue = 10;

        vm.SelectedServer = "b"; // decline -> revert
        await WaitUntil(() => vm.SelectedServer == first, "selection revert");

        Assert.Equal("a", vm.SelectedServer);
        Assert.Equal("a", vm.LoadedServer);
        Assert.True(vm.IsDirty); // edits survived
    }

    [Fact]
    public async Task SwitchingServerWhileDirty_LoadsWhenConfirmed()
    {
        var vm = new MainViewModel(() => Task.FromResult(true));
        await vm.SetFolder(_dir);
        Assert.Equal("a", vm.SelectedServer);

        vm.IniGroups.SelectMany(g => g.Items)
            .OfType<NumberSettingVM>().First(n => n.Key == "MaxPlayers")
            .NumberValue = 10;

        vm.SelectedServer = "b";
        await WaitUntil(() => vm.LoadedServer == "b", "confirm switch");

        Assert.Equal("b", vm.LoadedServer);
        Assert.False(vm.IsDirty);
        Assert.True(vm.SandboxGroups.Count > 0); // loaded content for b
    }

    [Fact]
    public async Task SearchAcrossCategories_FindsSettingsOutsideSelectedGroup()
    {
        var vm = new MainViewModel();
        await vm.SetFolder(_dir); // fresh folder, no edits pending
        Assert.Equal("a", vm.SelectedServer);

        vm.SearchText = "MaxPlayers"; // lives in Players, not Details (the selected group)
        Assert.True(vm.IsSearching);
        var matchedKeys = vm.IniMatches.Select(m => m.Key).ToList();
        Assert.Contains("MaxPlayers", matchedKeys);
    }

    [Fact]
    public void BuildGroups_PutsGameSettingsInAuthoredCategories()
    {
        var luaFile = ConfigParser.LoadLua(DataFile("myserver_SandboxVars.lua"));
        var target = new ObservableCollection<SettingGroupVM>();
        MainViewModel.BuildGroups(luaFile, luaFile.Groups, target, CategoryMap.SandboxOrder,
            isIni: false, markDirty: () => { });

        // ZombieLore's Speed must land in the Zombie group, not "Other"
        var zombie = target.First(g => g.Name == "Zombie");
        Assert.Contains(zombie.Items, i => i.Key == "Speed");

        // unlisted nested mod tables must be grouped as Mods, never lost
        var allKeys = target.SelectMany(g => g.Items).Count();
        Assert.Equal(luaFile.Groups.Sum(g => g.Settings.Count), allKeys); // every setting survives grouping
    }

    private static string DataFile(string name) =>
        Path.Combine(AppContext.BaseDirectory, "TestData", name);

    private static async Task WaitUntil(Func<bool> condition, string what)
    {
        for (var i = 0; i < 100 && !condition(); i++)
            await Task.Delay(25);
        if (!condition()) throw new TimeoutException("timed out waiting for: " + what);
    }

    [Fact]
    public async Task Save_DoesNotClobberFileChangedOnDisk()
    {
        var vm = new MainViewModel();
        await vm.SetFolder(_dir);
        var number = vm.IniGroups.SelectMany(g => g.Items)
            .OfType<NumberSettingVM>().First(n => n.Key == "MaxPlayers");
        number.NumberValue = 10; // dirty edit

        // the server rewrites the config while the app holds its own copy
        var iniPath = Path.Combine(_dir, "a.ini");
        File.AppendAllText(iniPath, "ExtraKey=5\r\n");
        var changedOnDisk = File.ReadAllText(iniPath);

        vm.SaveCommand.Execute(null);

        Assert.Equal(changedOnDisk, File.ReadAllText(iniPath)); // disk version untouched
        Assert.False(File.Exists(iniPath + ".bak")); // nothing written at all
        Assert.Contains("Refresh", vm.Status);
        Assert.True(vm.IsDirty); // edits still pending, nothing lost silently
    }
}
