using System.Text.RegularExpressions;
using PZConfigurator.Config;
using PZConfigurator.ViewModels;
using Xunit;

namespace PZConfigurator.Tests;

public class ParserTests
{
    static string Data(string name) =>
        Path.Combine(AppContext.BaseDirectory, "TestData", name);

    static (ConfigFile file, string original) Load(string name)
    {
        var f = name.EndsWith(".ini") ? ConfigParser.LoadIni(Data(name)) : ConfigParser.LoadLua(Data(name));
        return (f, File.ReadAllText(Data(name)));
    }

    [Theory]
    [InlineData("myserver.ini")]
    [InlineData("myserver_SandboxVars.lua")]
    public void RoundTrip_Unchanged_IsByteIdentical(string name)
    {
        var (file, _) = Load(name);
        Assert.Equal(file.OriginalText, file.Render());
    }

    [Fact]
    public void IniSecretKeys_AreMaskedInVM()
    {
        static Setting S(string key) => new()
            { Key = key, LineIndex = 0, LinePrefix = "", LineSuffix = "", RawValue = "x" };

        // VM constructed directly (no file needed: IsSecret is fixed at load time)
        static TextSettingVM VM(string key) => new(S(key), null!, () => { });

        Assert.True(VM("Password").IsSecret);
        Assert.True(VM("RCONPassword").IsSecret);
        Assert.True(VM("DiscordToken").IsSecret);
        Assert.True(VM("WebhookAddress").IsSecret);
        Assert.False(VM("PublicName").IsSecret);
        Assert.False(VM("MaxPlayers").IsSecret);
    }

    [Fact]
    public void Ini_ChangeValue_OnlyChangesThatLine()
    {
        var (file, _) = Load("myserver.ini");
        var before = file.Lines.ToArray();
        var setting = file.Settings.First(s => s.Key == "MaxPlayers");
        file.Update(setting, "10");
        file.Render();

        // untouched lines stay identical
        Assert.Equal(
            before.Where((_, i) => i != setting.LineIndex),
            file.Lines.Where((_, i) => i != setting.LineIndex));
        Assert.Equal("MaxPlayers=10", file.Lines[setting.LineIndex]);
    }

    [Fact]
    public void Lua_ChangeValue_OnlyChangesThatLine()
    {
        var (file, _) = Load("myserver_SandboxVars.lua");
        var before = file.Lines.ToArray();
        var setting = file.Groups.First(g => g.Name == "ZombieLore").Settings
            .First(s => s.Key == "Speed");
        file.Update(setting, "3");

        Assert.Equal(
            before.Where((_, i) => i != setting.LineIndex),
            file.Lines.Where((_, i) => i != setting.LineIndex));
        Assert.Contains("Speed = 3", file.Lines[setting.LineIndex]);
    }

    [Fact]
    public void Lua_EnumOptions_AreParsedFromComments()
    {
        var (file, _) = Load("myserver_SandboxVars.lua");
        var speed = file.Groups.SelectMany(g => g.Settings)
            .First(s => s.Key == "Speed");
        Assert.Equal(SettingKind.Enum, speed.Kind);
        Assert.Contains(speed.Options, o => o.Label == "Sprinters" && o.Value == 1);
        Assert.Equal("4", speed.RawValue.Trim());
    }

    [Fact]
    public void Ini_MinMax_IsParsed()
    {
        var (file, _) = Load("myserver.ini");
        var maxPlayers = file.Settings.First(s => s.Key == "MaxPlayers");
        Assert.Equal(1, maxPlayers.Min);
        Assert.Equal(254, maxPlayers.Max);
        Assert.Equal(SettingKind.Int, maxPlayers.Kind);
    }

    [Fact]
    public void Lua_Sections_BecomeGroups()
    {
        var (file, _) = Load("myserver_SandboxVars.lua");
        Assert.Contains(file.Groups, g => g.Name == "ZombieLore" && g.Settings.Any(s => s.Key == "Speed"));
        Assert.Contains(file.Groups, g => g.Name == "General");
    }

    [Fact]
    public void Save_WritesChangedValue_CreatesBackup_AndLeavesRestIntact()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pzcfg-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        foreach (var name in new[] { "myserver.ini", "myserver_SandboxVars.lua" })
            File.Copy(Data(name), Path.Combine(dir, name));

        var iniPath = Path.Combine(dir, "myserver.ini");
        var preSave = File.ReadAllText(iniPath);
        var file = ConfigParser.LoadIni(iniPath);
        var setting = file.Settings.First(s => s.Key == "MaxPlayers");
        file.Update(setting, "10");
        ConfigParser.Save(file);

        var after = File.ReadAllText(iniPath);
        Assert.Contains("MaxPlayers=10", after);
        Assert.Contains("ShowFirstAndLastName=false", after); // other lines untouched
        Assert.Equal(preSave, File.ReadAllText(iniPath + ".bak")); // backup = pre-save state

        Directory.Delete(dir, true);
    }

    [Fact]
    public void Save_Direct_ThrowsWhenFileChangedOnDisk_NoWriteNoBak()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pzcfg-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var iniPath = Path.Combine(dir, "a.ini");
        File.WriteAllText(iniPath, "PVP=false\r\n");

        var file = ConfigParser.LoadIni(iniPath);
        file.Update(file.Settings.First(s => s.Key == "PVP"), "true"); // pending change

        // someone else rewrites the file while the app holds its copy
        File.WriteAllText(iniPath, "PVP=false\r\nExtraKey=5\r\n");

        var ex = Assert.Throws<ConfigChangedOnDiskException>(() => ConfigParser.Save(file));
        Assert.Contains("a.ini", ex.Message); // message names the file
        Assert.Equal("PVP=false\r\nExtraKey=5\r\n", File.ReadAllText(iniPath)); // nothing written
        Assert.False(File.Exists(iniPath + ".bak")); // no backup either

        Directory.Delete(dir, true);
    }

    [Fact]
    public void Save_PreservesUnixFileMode()
    {
        if (OperatingSystem.IsWindows()) return; // SetUnixFileMode is a Unix-only API

        var dir = Path.Combine(Path.GetTempPath(), "pzcfg-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var iniPath = Path.Combine(dir, "a.ini");
        File.WriteAllText(iniPath, "PVP=false\r\n");
        File.SetUnixFileMode(iniPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead); // 0640

        var file = ConfigParser.LoadIni(iniPath);
        file.Update(file.Settings.First(s => s.Key == "PVP"), "true");
        ConfigParser.Save(file);

        Assert.Equal("PVP=true", File.ReadAllText(iniPath).TrimEnd()); // save actually happened
        var mode = File.GetUnixFileMode(iniPath) &
            (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead |
             UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead, mode);

        Directory.Delete(dir, true);
    }

    [Fact]
    public void Save_History_TwoSavesKeepOldestTimestampedBackup()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pzcfg-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var iniPath = Path.Combine(dir, "a.ini");
        File.WriteAllText(iniPath, "PVP=false\r\nMaxPlayers=32\r\n");
        var originalBytes = File.ReadAllBytes(iniPath);
        ConfigParser.UtcNow = () => new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        try
        {
            var file = ConfigParser.LoadIni(iniPath);
            ConfigParser.UtcNow = () => new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            file.Update(file.Settings.First(s => s.Key == "PVP"), "true");
            ConfigParser.Save(file);
            ConfigParser.UtcNow = () => new DateTime(2026, 1, 1, 12, 0, 1, DateTimeKind.Utc);
            file.Update(file.Settings.First(s => s.Key == "MaxPlayers"), "10");
            ConfigParser.Save(file);

            var stamps = Directory.GetFiles(dir, "a.ini.*.bak")
                .Select(Path.GetFileName).OrderBy(n => n).ToList();
            Assert.Equal(2, stamps.Count);
            // the OLDEST snapshot is the fixture's pre-save content, byte-identical
            Assert.Equal(originalBytes, File.ReadAllBytes(Path.Combine(dir, stamps[0])));
            // newest .bak still holds the intermediate state (invariant unchanged)
            Assert.Equal("PVP=true\r\nMaxPlayers=32\r\n", File.ReadAllText(iniPath + ".bak"));
        }
        finally
        {
            ConfigParser.UtcNow = () => DateTime.UtcNow;
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Save_History_PrunedToTen_OtherFilesUntouched()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pzcfg-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var iniPath = Path.Combine(dir, "a.ini");
        File.WriteAllText(iniPath, "PVP=false\r\n");
        File.WriteAllText(Path.Combine(dir, "a.ini.keepme.bak"), "do not touch");
        File.WriteAllText(Path.Combine(dir, "notes.txt"), "do not touch");
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        try
        {
            var file = ConfigParser.LoadIni(iniPath);
            for (var i = 1; i <= 12; i++)
            {
                ConfigParser.UtcNow = () => baseTime.AddSeconds(i);
                file.Update(file.Settings.First(s => s.Key == "PVP"), $"true{i}");
                ConfigParser.Save(file);
            }

            var stamps = Directory.GetFiles(dir, "a.ini.*.bak")
                .Select(Path.GetFileName)
                .Where(n => n != null && Regex.IsMatch(n, @"^a\.ini\.\d{8}-\d{6}-\d{3}\.bak$"))
                .OrderBy(n => n).ToList();
            Assert.Equal(ConfigParser.BackupHistoryCount, stamps.Count);
            // the two oldest snapshots were pruned; the .keepme/.txt files survive
            Assert.DoesNotContain("a.ini.20260101-000001-000.bak", stamps);
            Assert.Contains("a.ini.keepme.bak", Directory.GetFiles(dir).Select(Path.GetFileName));
            Assert.Contains("notes.txt", Directory.GetFiles(dir).Select(Path.GetFileName));
        }
        finally
        {
            ConfigParser.UtcNow = () => DateTime.UtcNow;
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Save_History_NoOpSaveCreatesNone()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pzcfg-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var iniPath = Path.Combine(dir, "a.ini");
        File.WriteAllText(iniPath, "PVP=false\r\n");

        var file = ConfigParser.LoadIni(iniPath);
        ConfigParser.Save(file); // Render == OriginalText: nothing at all happens

        Assert.DoesNotContain(Directory.GetFiles(dir),
            static f => f.EndsWith(".bak", StringComparison.Ordinal));
        Directory.Delete(dir, true);
    }

    [Fact]
    public void Save_TwiceWithoutChanges_BackupStillHoldsPreFirstSaveContent()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pzcfg-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var iniPath = Path.Combine(dir, "a.ini");
        File.WriteAllText(iniPath, "PVP=false\r\n");

        // first save changes the value, backup must keep the original
        var file = ConfigParser.LoadIni(iniPath);
        file.Update(file.Settings.First(s => s.Key == "PVP"), "true");
        ConfigParser.Save(file);
        Assert.Equal("PVP=false", File.ReadAllText(iniPath + ".bak").TrimEnd());

        // second save with NO changes must not touch anything (especially not the backup)
        ConfigParser.Save(file);
        Assert.Equal("PVP=false", File.ReadAllText(iniPath + ".bak").TrimEnd());

        Directory.Delete(dir, true);
    }

    [Fact]
    public void Save_PreservesMultipleTrailingNewlines()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pzcfg-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var iniPath = Path.Combine(dir, "a.ini");
        File.WriteAllText(iniPath, "PVP=true\r\n\r\n");

        var file = ConfigParser.LoadIni(iniPath);
        ConfigParser.Save(file);
        Assert.Equal("PVP=true\r\n\r\n", File.ReadAllText(iniPath));

        Directory.Delete(dir, true);
    }

    [Fact]
    public void LoadingOutOfRangeEnumValue_DoesNotRewriteFileOrDirty()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pzcfg-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var iniPath = Path.Combine(dir, "a.ini");
        // stored value 9 is not in the comment list 1..3 (happens with mods / older configs)
        File.WriteAllText(iniPath, "# 1 = Foo\n# 2 = Bar\n# 3 = Baz\nEnKey=9\n");
        var original = File.ReadAllText(iniPath);

        var file = ConfigParser.LoadIni(iniPath);
        var setting = file.Settings.First(s => s.Key == "EnKey");
        Assert.Equal(SettingKind.Enum, setting.Kind);

        var wasDirty = false;
        var vm = SettingVMFactory.Create(file, setting, () => wasDirty = true);
        var enumVm = Assert.IsType<EnumSettingVM>(vm);
        // must not write anything and must not select a random option
        Assert.Equal(-1, enumVm.SelectedIndex);
        Assert.False(wasDirty);
        Assert.Equal(original, file.Render());

        Directory.Delete(dir, true);
    }

    [Fact]
    public void NonDefaultSetting_IsFlagged_AndClearedWhenRevertedToDefault()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pzcfg-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var iniPath = Path.Combine(dir, "a.ini");
        File.WriteAllText(iniPath, "EnKey=2\n");
        var file = ConfigParser.LoadIni(iniPath);
        var setting = file.Settings.First(s => s.Key == "EnKey");

        var vm = Assert.IsType<NumberSettingVM>(SettingVMFactory.Create(file, setting, () => { }, defaultRaw: "2"));
        Assert.False(vm.IsNonDefault); // matches vanilla default

        vm.NumberValue = 3;
        Assert.True(vm.IsNonDefault);

        vm.NumberValue = 2;
        Assert.False(vm.IsNonDefault);

        Directory.Delete(dir, true);
    }

    [Fact]
    public void ZomboidCfgPreset_IsParsedAsTemplate()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pzcfg-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var p = Path.Combine(dir, "save1.cfg");
        File.WriteAllText(p,
            "Version=6\n" +
            "# comment\n" +
            "    -- 1 = Insane\n" +
            "Zombies=4\n" +
            "\n" +
            "ZombieVoronoiNoise=true\n" +
            "OtherLootNew=0.8\n");

        var errors = new List<string>();
        var list = TemplateLoader.LoadZomboidPresets(dir, errors);

        Assert.Empty(errors);
        var t = Assert.Single(list);
        Assert.Equal("save1", t.Name);
        Assert.Equal(3, t.Values.Count); // Version= and comment lines excluded
        Assert.Equal("4", t.Values["Zombies"]);
        Assert.Equal("true", t.Values["ZombieVoronoiNoise"]);
        Assert.Equal("0.8", t.Values["OtherLootNew"]);

        Directory.Delete(dir, true);
    }
}
