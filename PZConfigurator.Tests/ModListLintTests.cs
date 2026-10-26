using PZConfigurator.Config;
using Xunit;

namespace PZConfigurator.Tests;

public class ModListLintTests
{
    static string? Chk(string key, string value) => ModListLint.Check(key, value);

    [Fact]
    public void CleanLists_AreNull()
    {
        Assert.Null(Chk("Mods", "modA;modB;modC"));
        Assert.Null(Chk("WorkshopItems", "123;456;789"));
    }

    [Fact]
    public void DuplicateMod_IsFlagged_CaseInsensitive()
    {
        Assert.Contains("duplicate", Chk("Mods", "modA;modB;MODA"));
        Assert.Contains("duplicate", Chk("WorkshopItems", "123;456;123"));
    }

    [Fact]
    public void EmptyEntries_AreFlagged()
    {
        // leading, double and trailing semicolons
        Assert.Contains("empty entry", Chk("Mods", ";modA"));
        Assert.Contains("empty entry", Chk("Mods", "modA;;modB"));
        Assert.Contains("empty entry", Chk("WorkshopItems", "123;456;"));
    }

    [Fact]
    public void TrailingSemicolon_IsFlagged()
    {
        Assert.Contains("empty entry", Chk("Mods", "modA;modB;"));
    }

    [Fact]
    public void WhitespaceAroundEntry_IsFlagged()
    {
        Assert.Contains("whitespace", Chk("Mods", "modA; modB"));
        Assert.Contains("whitespace", Chk("WorkshopItems", "123;456 "));
    }

    [Fact]
    public void NonNumericWorkshopItem_IsFlagged()
    {
        Assert.Contains("non-numeric", Chk("WorkshopItems", "123;abc"));
    }

    [Fact]
    public void Mods_AllowsNonNumeric()
    {
        Assert.Null(Chk("Mods", "123;abc"));
    }

    [Fact]
    public void EmptyValue_IsNull()
    {
        Assert.Null(Chk("Mods", ""));
        Assert.Null(Chk("Mods", "   "));
        Assert.Null(Chk("WorkshopItems", ""));
    }

    [Fact]
    public void UnrelatedKey_IsNull()
    {
        Assert.Null(Chk("PublicName", "modA;;modB"));
        Assert.Null(Chk("Map", ""));
    }

    [Fact]
    public void FixtureLists_AreClean()
    {
        var iniPath = Path.Combine(AppContext.BaseDirectory, "TestData", "myserver.ini");
        foreach (var line in File.ReadLines(iniPath))
        {
            var i = line.IndexOf('=');
            if (i <= 0) continue;
            var key = line[..i];
            if (key is not ("Mods" or "WorkshopItems")) continue;
            Assert.Null(Chk(key, line[(i + 1)..]));
        }
    }
}
