using PZConfigurator.Config;
using PZConfigurator.ViewModels;
using Xunit;

namespace PZConfigurator.Tests;

public class FilterTests : IDisposable
{
    static FilterTests() => AvaloniaTestSetup.Ensure();

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

    [Fact]
    public async Task Search_TagsMatchesWithCategoryAndClearsTags()
    {
        var vm = new MainViewModel();
        await vm.SetFolder(_dir);

        vm.SearchText = "Port";
        var tagged = vm.SandboxGroups.Concat(vm.IniGroups)
            .SelectMany(g => g.Items).Where(i => i.HasMatchCategory).ToList();
        Assert.NotEmpty(tagged);
        Assert.All(tagged, i => Assert.DoesNotContain("·", i.MatchCategory));
        Assert.Equal(vm.IniMatches.Count + vm.SandboxMatches.Count, tagged.Count);

        vm.SearchText = "";
        Assert.DoesNotContain(vm.SandboxGroups.Concat(vm.IniGroups)
            .SelectMany(g => g.Items), i => i.HasMatchCategory);
    }

    [Fact]
    public async Task Search_MatchesDescriptionFromThreeCharsOn()
    {
        var vm = new MainViewModel();
        await vm.SetFolder(_dir);

        // a word that lives in a setting description but not in any key
        var desc = vm.SandboxGroups.Concat(vm.IniGroups).SelectMany(g => g.Items)
            .FirstOrDefault(i =>
                (i.Tooltip.Contains("Urban", StringComparison.OrdinalIgnoreCase)
                    && !i.Key.Contains("Urban", StringComparison.OrdinalIgnoreCase))
                || (i.Tooltip.Contains("July", StringComparison.OrdinalIgnoreCase)
                    && !i.Key.Contains("July", StringComparison.OrdinalIgnoreCase)));
        Assert.NotNull(desc); // fixture tooltips must carry such a word

        // (a) >= 3 chars: matches via description
        vm.SearchText = desc.Tooltip.Contains("Urban", StringComparison.OrdinalIgnoreCase) ? "Urban" : "July";
        Assert.True(desc.HasMatchCategory, "tooltip=" + desc.Tooltip + " status=" + vm.Status);

        // (b) 1-2 chars: description must not fire (keeps "4" from matching every "Default: 4")
        vm.SearchText = vm.SearchText[..2];
        Assert.False(desc.HasMatchCategory);
    }

    [Fact]
    public async Task Search_NoMatches_ShowsEmptyState()
    {
        var vm = new MainViewModel();
        await vm.SetFolder(_dir);

        vm.SearchText = "ZzzNoSuchThing";
        Assert.True(vm.HasNoMatches);
        Assert.StartsWith("No settings match", vm.NoMatchesText);

        // modified-only with no changes: different wording, same empty state
        vm.SearchText = "";
        vm.OnlyModified = true;
        Assert.True(vm.HasNoMatches);
        Assert.StartsWith("Nothing modified", vm.NoMatchesText);

        vm.OnlyModified = false;
        vm.SearchText = "Port";
        Assert.False(vm.HasNoMatches);
    }

    [Fact]
    public async Task TemplatePreview_ShowsCountsOnSelection()
    {
        var vm = new MainViewModel();
        await vm.SetFolder(_dir);
        var apocalypse = vm.Templates.Single(t => t.Name == "Apocalypse");

        vm.TemplateSelected = apocalypse;

        Assert.Contains($"Ready to apply \"Apocalypse\"", vm.Status);
        Assert.Contains($"{apocalypse.Values.Count} settings found", vm.Status);
    }

    [Fact]
    public async Task TemplateApply_NoEffectiveChanges_ReportsNoChanges()
    {
        var vm = new MainViewModel();
        await vm.SetFolder(_dir);
        // template built from the CURRENT on-disk values so every assignment is a no-op
        var b = vm.SandboxGroups.SelectMany(g => g.Items).OfType<BoolSettingVM>().First(i => i.IsVisible);
        var n = vm.SandboxGroups.SelectMany(g => g.Items).OfType<NumberSettingVM>().First(i => i.IsVisible && i.IsInteger);
        var values = new Dictionary<string, string>
        {
            [b.Key] = b.IsChecked ? "true" : "false",
            [n.Key] = n.NumberValue!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        vm.TemplateSelected = new SandboxTemplate { Name = "Same", Values = values };

        vm.ApplyTemplateCommand.Execute(null);

        Assert.Contains("no changes", vm.Status);
        Assert.False(vm.IsDirty);
    }
}
