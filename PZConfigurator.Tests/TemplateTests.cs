using System.Text.RegularExpressions;
using PZConfigurator.Config;
using PZConfigurator.ViewModels;
using Xunit;

namespace PZConfigurator.Tests;

public class TemplateTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "pzcfg-tpl-" + Guid.NewGuid().ToString("N"));
    private readonly string _userTemplates = Path.Combine(
        Path.GetTempPath(), "pzcfg-tpl-tpl-" + Guid.NewGuid().ToString("N"));

    static TemplateTests() => AvaloniaTestSetup.Ensure();

    public TemplateTests()
    {
        Directory.CreateDirectory(_dir);
        Directory.CreateDirectory(_userTemplates);
        foreach (var name in new[] { "myserver.ini", "myserver_SandboxVars.lua" })
            File.Copy(
                Path.Combine(AppContext.BaseDirectory, "TestData", name),
                Path.Combine(_dir, name));
    }

    public void Dispose()
    {
        Directory.Delete(_dir, true);
        Directory.Delete(_userTemplates, true);
    }

    // hermetic VM factory: never touches the real %USERPROFILE% Zomboid/template folders
    private MainViewModel NewVm() => new(null, _dir, _userTemplates);

    [Fact]
    public void EmbeddedTemplates_AllParse()
    {
        var templates = TemplateLoader.LoadEmbedded(out var errors);

        Assert.Empty(errors); // shipped resources are part of the product, must never fail
        Assert.Equal(5, templates.Count);
        foreach (var t in templates)
        {
            Assert.NotEmpty(t.Values);
            Assert.False(t.Values.ContainsKey("VERSION"), "VERSION is read-only and must not be templated");
            Assert.True(!t.Values.Values.Contains("{"),
                $"{t.Name}: section headers ({{) are extractor garbage, not setting values");
        }
        Assert.True(templates.Single(t => t.Name == "Apocalypse").Values.ContainsKey("Zombies"));
    }

    [Fact]
    public async Task IniSettings_NeverCompareAgainstSandboxDefaults()
    {
        var vm = NewVm();
        await vm.SetFolder(_dir);
        Assert.Equal("myserver", vm.LoadedServer); // sanity: VM loaded both config files

        var map = vm.IniGroups.SelectMany(g => g.Items).First(i => i.Key == "Map");
        Assert.False(map.IsNonDefault,
            "Map=Muldraugh, KY must not be painted blue because of a colliding Sandbox key");
    }

    [Fact]
    public async Task ApplyTemplate_SetsValues_MarksDirty_WritesOnlyOnSave()
    {
        var vm = NewVm();
        var apocalypse = vm.Templates.Single(t => t.Name == "Apocalypse");
        var luaPath = Path.Combine(_dir, "myserver_SandboxVars.lua");
        // force a real change: put the opposite of the template value on disk BEFORE
        // the VM loads, so the in-editor value differs from the template after load
        var templateWants = bool.Parse(apocalypse.Values["ZombieVoronoiNoise"]);
        var luaText = File.ReadAllText(luaPath);
        File.WriteAllText(luaPath, Regex.Replace(
            luaText,
            @"(ZombieVoronoiNoise\s*=\s*)\w+",
            $"$1{(templateWants ? "false" : "true")}"));
        var beforeDisk = File.ReadAllText(luaPath);

        await vm.SetFolder(_dir);
        Assert.Equal("myserver", vm.LoadedServer);

        vm.TemplateSelected = apocalypse;
        vm.ApplyTemplateCommand.Execute(null);

        var allKeys = vm.SandboxGroups.SelectMany(g => g.Items).Select(i => i.Key).ToList();
        Assert.True(allKeys.Contains("ZombieVoronoiNoise"),
            "keys=" + allKeys.Count + " status=" + vm.Status + " loaded=" + vm.LoadedServer);
        var noiseVM = vm.SandboxGroups.SelectMany(g => g.Items).First(i => i.Key == "ZombieVoronoiNoise");
        var noise = vm.SandboxGroups.SelectMany(g => g.Items)
            .OfType<BoolSettingVM>().First(b => b.Key == "ZombieVoronoiNoise");

        Assert.StartsWith("Applied", vm.Status);
        Assert.True(vm.IsDirty,
            "type=" + noiseVM.GetType().Name
            + " isChecked=" + (noiseVM is BoolSettingVM bb ? bb.IsChecked.ToString() : "-")
            + " wants=" + templateWants
            + " status=" + vm.Status);
        Assert.Equal(templateWants, noise.IsChecked);

        // nothing written to disk before Save
        Assert.Equal(beforeDisk, File.ReadAllText(luaPath));
        Assert.False(File.Exists(luaPath + ".bak"));

        vm.SaveCommand.Execute(null);

        var check = ConfigParser.LoadLua(luaPath);
        Assert.Equal(apocalypse.Values["ZombieVoronoiNoise"],
            check.Settings.First(s => s.Key == "ZombieVoronoiNoise").RawValue);
        Assert.Equal(apocalypse.Values["Zombies"],
            check.Settings.First(s => s.Key == "Zombies").RawValue); // int applied too

        // untouched lines survive untouched (VERSION line is exact)
        Assert.Contains("    VERSION = 6,", File.ReadAllText(luaPath));
    }

    [Fact]
    public async Task ApplyTemplate_ReportsUnknownKeysExactly()
    {
        var vm = NewVm();
        await vm.SetFolder(_dir);

        var tiny = new SandboxTemplate
        {
            Name = "Tiny",
            Values = new Dictionary<string, string> { ["Zombies"] = "4", ["NoSuchKey"] = "1" },
        };
        vm.TemplateSelected = tiny;
        vm.ApplyTemplateCommand.Execute(null);

        Assert.Contains("Applied \"Tiny\": ", vm.Status);
        Assert.Contains("1 not found", vm.Status);
    }

    [Fact]
    public void UserFolderTemplates_LoadAndBadJsonSkipsWithoutThrow()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pzcfg-utpl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "hardmode.json"),
            """{ "name": "HardMode", "description": "test", "values": { "Zombies": "3", "DayLength": true } }""");
        File.WriteAllText(Path.Combine(dir, "broken.json"), "{ not json");

        var errors = new List<string>();
        var templates = TemplateLoader.LoadUserFolder(dir, errors).ToList();

        var hard = templates.Single(t => t.Name == "HardMode");
        Assert.Equal("3", hard.Values["Zombies"]);      // string literal preserved
        Assert.Equal("true", hard.Values["DayLength"]); // JSON bool → file literal "true"
        Assert.Single(errors);                          // bad file reported, others still load
        Assert.Contains("broken.json", errors[0]);
        Directory.Delete(dir, true);
    }

    [Fact]
    public void UserFolderTemplates_MissingFolder_IsEmpty()
    {
        var errors = new List<string>();
        var templates = TemplateLoader.LoadUserFolder(
            Path.Combine(Path.GetTempPath(), "pzcfg-nonexistent-" + Guid.NewGuid().ToString("N")),
            errors).ToList();
        Assert.Empty(templates);
        Assert.Empty(errors);
    }
}
