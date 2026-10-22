using PZConfigurator.Config;
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
}
