using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZConfigurator.Config;

namespace PZConfigurator.ViewModels;

/// <summary>Severity attached to the current status message.</summary>
public enum StatusKind { Info, Warn, Error }

public partial class MainViewModel : ObservableObject
{
    public const string DefaultFolderName = "Zomboid"; // under the user profile

    public ObservableCollection<string> Servers { get; } = [];
    public ObservableCollection<SettingGroupVM> IniGroups { get; } = [];
    public ObservableCollection<SettingGroupVM> SandboxGroups { get; } = [];

    /// <summary>Built-in game modes plus user JSONs; Sandbox tab only.</summary>
    public ObservableCollection<SandboxTemplate> Templates { get; } = [];

    [ObservableProperty]
    private SandboxTemplate? _templateSelected;

    // status preview of what Apply will do, before it does it
    public bool HasTemplateSelected => TemplateSelected != null;

    partial void OnTemplateSelectedChanged(SandboxTemplate? value)
    {
        OnPropertyChanged(nameof(HasTemplateSelected));
        if (value == null) return;
        var present = SandboxGroups.SelectMany(g => g.Items).Select(i => i.Key)
            .ToHashSet(StringComparer.Ordinal);
        var found = value.Values.Keys.Count(k => present.Contains(k));
        SetStatus($"Ready to apply \"{value.Name}\""
            + (string.IsNullOrEmpty(value.Description) ? "" : $": {value.Description}")
            + $" — {found} of {value.Values.Count} settings found. Press Apply, review, then Save");
    }

    /// <summary>While searching, the right pane shows a flat list of matches from
    /// every category of the tab instead of the selected category only.</summary>
    public ObservableCollection<SettingVM> IniMatches { get; } = [];
    public ObservableCollection<SettingVM> SandboxMatches { get; } = [];

    [ObservableProperty]
    private SettingGroupVM? _iniSelected;

    [ObservableProperty]
    private SettingGroupVM? _sandboxSelected;

    [ObservableProperty]
    private string _folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DefaultFolderName, "Server");

    [ObservableProperty]
    private string? _selectedServer;

    [ObservableProperty]
    private string? _status;

    [ObservableProperty]
    private StatusKind _statusKind; // severity of the current status, set by SetStatus

    private void SetStatus(string? text, StatusKind kind = StatusKind.Info)
    {
        Status = text;
        StatusKind = text == null ? StatusKind.Info : kind;
        // derived from the kind here so severity needs no text sniffing; XAML binds unchanged
        IsStatusError = kind == StatusKind.Error && text != null;
        IsStatusWarn = kind == StatusKind.Warn && text != null;
    }

    [ObservableProperty]
    private bool _isStatusError; // status text gets red/orange class

    [ObservableProperty]
    private bool _isStatusWarn;

    [ObservableProperty]
    private string _searchText = "";

    partial void OnSearchTextChanged(string value)
    {
        HasSearchText = value.Length > 0;
        ApplyFilter();
    }

    partial void OnOnlyModifiedChanged(bool value) => ApplyFilter();

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private bool _hasSearchText; // drives the × button

    [ObservableProperty]
    private bool _onlyModified; // show only settings changed since load/last save

    [ObservableProperty]
    private bool _hasNoMatches; // the search/modified panes show a "nothing found" text

    [ObservableProperty]
    private string _noMatchesText = "";

    [ObservableProperty]
    private string _title = "PZConfigurator";

    [ObservableProperty]
    private int _selectedTabIndex; // Ctrl+Tab cycles tabs; scripts use it too

    private readonly Func<Task<bool>>? _confirmDiscard;
    private readonly string? _userTemplateFolder; // test seam; real %USERPROFILE% fallback when null
    private List<ConfigFile> _files = [];
    private string? _loadedServer;
    private string? _loadedStatus; // restored when search/filter is deactivated
    private StatusKind _loadedStatusKind; // severity travels with _loadedStatus
    private bool _loading;
    private bool _suppress;

    public string? LoadedServer => _loadedServer;

    /// <summary>false while no server config pair is loaded (empty folder, bad selection,
    /// or load error) — drives the "nothing to display" state and hides the template row.</summary>
    [ObservableProperty]
    private bool _hasConfigLoaded;

    public MainViewModel(Func<Task<bool>>? confirmDiscard = null, string? folder = null,
        string? userTemplateFolder = null)
    {
        _confirmDiscard = confirmDiscard;
        _userTemplateFolder = userTemplateFolder;
        // must precede DiscoverAsync: a test folder means the real %USERPROFILE% Zomboid
        // directory is never touched, even transiently
        if (folder != null) Folder = folder;
        _ = DiscoverAsync(); // constructor can't await; first load is fire-and-forget
        LoadTemplates();
    }

    private void LoadTemplates()
    {
        var errors = new List<string>();
        var embeddedErrors = new List<string>();
        foreach (var t in TemplateLoader.LoadEmbedded(out embeddedErrors))
            Templates.Add(t);
        errors.AddRange(embeddedErrors);
        var userFolder = _userTemplateFolder ?? TemplateLoader.UserTemplateFolder;
        foreach (var t in TemplateLoader.LoadUserFolder(userFolder, errors))
            Templates.Add(t);
        // hermetic tests pass a userTemplateFolder; the real Zomboid profile is never read there
        if (_userTemplateFolder == null)
            foreach (var t in TemplateLoader.LoadZomboidPresets(TemplateLoader.ZomboidPresetFolder, errors))
                Templates.Add(t);
        if (errors.Count > 0)
            SetStatus((Status ?? "") + " | template problems: " + string.Join("; ", errors),
                StatusKind.Warn);
    }

    [RelayCommand]
    private void ApplyTemplate()
    {
        var t = TemplateSelected;
        if (t == null)
        {
            SetStatus("Select a template first");
            return;
        }

        var items = SandboxGroups.SelectMany(g => g.Items)
            .GroupBy(i => i.Key)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var applied = 0;
        var missing = 0;
        foreach (var (key, value) in t.Values)
        {
            if (items.TryGetValue(key, out var item) && TrySet(item, value)) applied++;
            else missing++;
        }
        ApplyFilter(); // update visibility first if "modified only" is on
        // nothing actually changed if every value already matched — don't send the user hunting
        SetStatus(IsDirty
            ? $"Applied \"{t.Name}\": {applied} values set"
                + (missing > 0 ? $", {missing} not found" : "")
                + " — review, then Save"
            : $"Applied \"{t.Name}\": no changes — config already matches this template"
                + (missing > 0 ? $" ({missing} not found)" : ""));
    }

    // assigns the template value through the write-through VM; returns false when
    // the item type or value can't handle it. Dirty-marking happens inside the
    // SettingVM write path, so no-op assignments never dirty the state.
    static bool TrySet(SettingVM item, string value)
    {
        switch (item)
        {
            case BoolSettingVM b when bool.TryParse(value, out var bv):
                b.IsChecked = bv;
                return true;
            case EnumSettingVM e when int.TryParse(value, out var ev):
                return e.TrySelectValue(ev);
            case NumberSettingVM n when double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var nv):
                n.NumberValue = nv;
                return true;
            case TextSettingVM tx when !tx.IsReadOnly:
                if (tx.Text != value) tx.Text = value;
                return true;
            default:
                return false;
        }
    }

    private async Task<bool> ConfirmDiscardAsync() =>
        _confirmDiscard == null || await _confirmDiscard();

    [RelayCommand]
    private async Task RefreshServerList() => await DiscoverAsync();

    public async Task SetFolder(string path)
    {
        Folder = path;
        await DiscoverAsync();
    }

    private async Task DiscoverAsync()
    {
        if (IsDirty && !await ConfirmDiscardAsync())
        {
            SetStatus("Unsaved changes kept, list not reloaded", StatusKind.Warn);
            return;
        }

        _loading = true;
        try
        {
            Servers.Clear();
            SetStatus(null);
            if (!Directory.Exists(Folder))
            {
                SetStatus($"Folder not found: {Folder}", StatusKind.Warn);
                SelectedServer = null;
                return;
            }

            var servers = Directory.EnumerateFiles(Folder, "*.ini")
                .Where(p => File.Exists(Path.Combine(
                    Path.GetDirectoryName(p)!,
                    Path.GetFileNameWithoutExtension(p) + "_SandboxVars.lua")))
                .Select(p => Path.GetFileNameWithoutExtension(p)!)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var s in servers) Servers.Add(s);
            SelectedServer = servers.Count > 0 ? servers[0] : null;
            if (SelectedServer == null)
                SetStatus("No servers found (need name.ini + name_SandboxVars.lua) in " + Folder,
                    StatusKind.Warn);
        }
        catch (UnauthorizedAccessException)
        {
            // typical on Linux: dedicated-server home (e.g. /home/pz) is mode 700
            SelectedServer = null;
            SetStatus($"Access denied on {Folder}. The server user's home is private on Linux — run the chmod/setfacl commands from the README section 'Linux: opening another user's Zomboid folder'.",
                StatusKind.Error);
        }
        catch (Exception ex)
        {
            SelectedServer = null;
            SetStatus("Error listing folder: " + ex.Message, StatusKind.Error);
        }
        finally
        {
            _loading = false;
        }
        LoadNow();
    }

    partial void OnSelectedServerChanged(string? value) => _ = SwitchServerAsync();

    private async Task SwitchServerAsync()
    {
        try
        {
            await SwitchServerCoreAsync();
        }
        catch (Exception ex)
        {
            SetStatus("Error switching server: " + ex.Message, StatusKind.Error);
        }
    }

    private async Task SwitchServerCoreAsync()
    {
        if (_suppress || _loading) return;
        if (IsDirty && !await ConfirmDiscardAsync())
        {
            // revert the dropdown to the server actually loaded
            _suppress = true;
            SelectedServer = _loadedServer;
            _suppress = false;
            SetStatus("Unsaved changes kept, still on " + _loadedServer, StatusKind.Warn);
            return;
        }
        LoadNow();
    }

    private void LoadNow()
    {
        IniGroups.Clear();
        SandboxGroups.Clear();
        IniMatches.Clear();
        SandboxMatches.Clear();
        _files = [];
        IsDirty = false;
        Title = "PZConfigurator";
        _loadedServer = SelectedServer;
        HasConfigLoaded = false;
        if (SelectedServer == null) return;

        try
        {
            var markDirty = () =>
            {
                // dirty only when an actual save would write something (same check Save uses)
                IsDirty = _files.Any(f => f.Render() != f.OriginalText);
                Title = IsDirty ? "PZConfigurator *" : "PZConfigurator";
            };
            var iniPath = Path.Combine(Folder, SelectedServer + ".ini");
            var luaPath = Path.Combine(Folder, SelectedServer + "_SandboxVars.lua");
            var ini = ConfigParser.LoadIni(iniPath);
            var lua = ConfigParser.LoadLua(luaPath);
            _files = [ini, lua];
            var defaults = TemplateLoader.VanillaDefaults();

            BuildGroups(ini, ini.Groups, IniGroups, CategoryMap.IniOrder, isIni: true, markDirty, defaults);
            BuildGroups(lua, lua.Groups, SandboxGroups, CategoryMap.SandboxOrder, isIni: false, markDirty, defaults);
            IniSelected = IniGroups.FirstOrDefault();
            SandboxSelected = SandboxGroups.FirstOrDefault();

            var loadedMsg = $"Loaded {SelectedServer}: "
                + (ini.Settings.Count() + lua.Settings.Count()) + " settings";
            SetStatus(loadedMsg);
            _loadedStatus = loadedMsg;
            _loadedStatusKind = StatusKind.Info;
            HasConfigLoaded = true;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            SetStatus("Error loading: " + ex.Message, StatusKind.Error);
        }
    }

    internal static void BuildGroups(
        ConfigFile file, IEnumerable<SettingGroup> source,
        ObservableCollection<SettingGroupVM> target, string[] order,
        bool isIni, Action markDirty, Dictionary<string, string>? defaults = null)
    {
        var byCategory = new Dictionary<string, List<Setting>>();
        foreach (var group in source)
        {
            foreach (var s in group.Settings)
            {
                var cat = isIni
                    ? CategoryMap.ResolveIni(s.Key)
                    : CategoryMap.ResolveLua(group.Name, s.Key);
                if (!byCategory.TryGetValue(cat, out var list))
                    byCategory[cat] = list = [];
                list.Add(s);
            }
        }

        // Apocalypse.json = sandbox values only; ini keys that collide with lua keys (Map,
        // BloodSplatLifespanDays…) must never be compared against them
        var d = isIni ? null : defaults;
        foreach (var cat in order.Where(byCategory.ContainsKey))
        {
            target.Add(new SettingGroupVM(cat,
                byCategory[cat].Select(s => SettingVMFactory.Create(file, s, markDirty,
                    d?.GetValueOrDefault(s.Key))).ToList()));
        }
    }

    private void ApplyFilter()
    {
        var q = SearchText?.Trim();
        IsSearching = !string.IsNullOrEmpty(q) || OnlyModified;

        IniMatches.Clear();
        SandboxMatches.Clear();
        foreach (var group in IniGroups)
            foreach (var item in group.Items)
                CollectMatch(IniMatches, item, q, group.Name);
        foreach (var group in SandboxGroups)
            foreach (var item in group.Items)
                CollectMatch(SandboxMatches, item, q, group.Name);
        UpdateFilterStatus();
    }

    [RelayCommand]
    private void ClearSearch() => SearchText = "";

    [RelayCommand]
    private void ToggleOnlyModified() => OnlyModified = !OnlyModified;

    // status reflects the active filter mode; with no filter the "Loaded" message returns
    private void UpdateFilterStatus()
    {
        var q = SearchText?.Trim();
        var total = IniMatches.Count + SandboxMatches.Count;
        HasNoMatches = IsSearching && total == 0;
        NoMatchesText = HasNoMatches
            ? (string.IsNullOrEmpty(q)
                ? "Nothing modified yet — change a setting to see it here"
                : $"No settings match \"{q}\"")
            : "";
        if (!string.IsNullOrEmpty(q))
            SetStatus($"Searching \"{q}\": {total} match{(total == 1 ? "" : "es")}"
                + (OnlyModified ? " among modified" : ""));
        else if (OnlyModified)
            SetStatus($"Showing modified settings only: {total}");
        else if (_loadedStatus != null)
            SetStatus(_loadedStatus, _loadedStatusKind); // restore the kind with the text
    }

    private void CollectMatch(ObservableCollection<SettingVM> target, SettingVM item, string? q, string category)
    {
        // key always matches; from 3 chars on the description counts too — keeps
        // short queries like "4" from hitting every "Default: 4" hint
        var match = (string.IsNullOrEmpty(q)
                || item.Key.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (q.Length >= 3 && item.Tooltip.Contains(q, StringComparison.OrdinalIgnoreCase)))
            && (!OnlyModified || item.IsModified);
        // item.IsVisible drives the master-detail pane; flat matched lists are pre-filtered
        item.IsVisible = match;
        // the category is only relevant while the flat matches pane is up
        item.MatchCategory = match && IsSearching ? category : null;
        if (match && IsSearching) target.Add(item);
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            // one atomic SaveAll for the pair: either both files land or none
            ConfigParser.SaveAll(_files);
            IsDirty = false;
            Title = "PZConfigurator";
            foreach (var g in IniGroups.Concat(SandboxGroups))
                foreach (var i in g.Items)
                    i.ClearModified();
            ApplyFilter(); // with "modified only" on, the list empties now
            SetStatus("Saved " + DateTime.Now.ToString("HH:mm:ss"));
            _loadedStatus = null; // don't resurrect the old "Loaded" message over the Save confirmation
        }
        catch (ConfigChangedOnDiskException ex)
        {
            SetStatus(ex.Message, StatusKind.Error);
            return; // edits stay pending
        }
        catch (Exception ex)
        {
            SetStatus("Error saving: " + ex.Message, StatusKind.Error);
        }
    }
}
