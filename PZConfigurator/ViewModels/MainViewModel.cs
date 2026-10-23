using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZConfigurator.Config;

namespace PZConfigurator.ViewModels;

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
    private string _searchText = "";

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string _title = "PZConfigurator";

    private readonly Func<Task<bool>>? _confirmDiscard;
    private List<ConfigFile> _files = [];
    private string? _loadedServer;
    private bool _loading;
    private bool _suppress;

    public string? LoadedServer => _loadedServer;

    public MainViewModel(Func<Task<bool>>? confirmDiscard = null)
    {
        _confirmDiscard = confirmDiscard;
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
        foreach (var t in TemplateLoader.LoadUserFolder(TemplateLoader.UserTemplateFolder, errors))
            Templates.Add(t);
        if (errors.Count > 0)
            Status = (Status ?? "") + " | template problems: " + string.Join("; ", errors);
    }

    [RelayCommand]
    private void ApplyTemplate()
    {
        var t = TemplateSelected;
        if (t == null)
        {
            Status = "Select a template first";
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
        Status = $"Applied \"{t.Name}\": {applied} values set"
            + (missing > 0 ? $", {missing} not found" : "")
            + " — review, then Save";
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
            Status = "Unsaved changes kept, list not reloaded";
            return;
        }

        _loading = true;
        try
        {
            Servers.Clear();
            Status = null;
            if (!Directory.Exists(Folder))
            {
                Status = $"Folder not found: {Folder}";
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
                Status = "No servers found (need name.ini + name_SandboxVars.lua) in " + Folder;
        }
        catch (UnauthorizedAccessException)
        {
            // typical on Linux: dedicated-server home (e.g. /home/pz) is mode 700
            SelectedServer = null;
            Status = $"Access denied on {Folder}. The server user's home is private on Linux — run the chmod/setfacl commands from the README section 'Linux: opening another user's Zomboid folder'.";
        }
        catch (Exception ex)
        {
            SelectedServer = null;
            Status = "Error listing folder: " + ex.Message;
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
            Status = "Error switching server: " + ex.Message;
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
            Status = "Unsaved changes kept, still on " + _loadedServer;
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
        if (SelectedServer == null) return;

        try
        {
            var markDirty = () => { if (!IsDirty) { IsDirty = true; Title = "PZConfigurator *"; } };
            var iniPath = Path.Combine(Folder, SelectedServer + ".ini");
            var luaPath = Path.Combine(Folder, SelectedServer + "_SandboxVars.lua");
            var ini = ConfigParser.LoadIni(iniPath);
            var lua = ConfigParser.LoadLua(luaPath);
            _files = [ini, lua];

            BuildGroups(ini, ini.Groups, IniGroups, CategoryMap.IniOrder, isIni: true, markDirty);
            BuildGroups(lua, lua.Groups, SandboxGroups, CategoryMap.SandboxOrder, isIni: false, markDirty);
            IniSelected = IniGroups.FirstOrDefault();
            SandboxSelected = SandboxGroups.FirstOrDefault();

            Status = $"Loaded {SelectedServer}: "
                + (ini.Settings.Count() + lua.Settings.Count()) + " settings";
            ApplyFilter();
        }
        catch (Exception ex)
        {
            Status = "Error loading: " + ex.Message;
        }
    }

    internal static void BuildGroups(
        ConfigFile file, IEnumerable<SettingGroup> source,
        ObservableCollection<SettingGroupVM> target, string[] order,
        bool isIni, Action markDirty)
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

        foreach (var cat in order.Where(byCategory.ContainsKey))
        {
            target.Add(new SettingGroupVM
            {
                Name = cat,
                Items = byCategory[cat].Select(s => SettingVMFactory.Create(file, s, markDirty)).ToList(),
            });
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var q = SearchText?.Trim();
        IsSearching = !string.IsNullOrEmpty(q);

        IniMatches.Clear();
        SandboxMatches.Clear();
        foreach (var group in IniGroups)
            foreach (var item in group.Items)
                CollectMatch(IniMatches, item, q);
        foreach (var group in SandboxGroups)
            foreach (var item in group.Items)
                CollectMatch(SandboxMatches, item, q);
    }

    private static void CollectMatch(ObservableCollection<SettingVM> target, SettingVM item, string? q)
    {
        var match = string.IsNullOrEmpty(q)
            || item.Key.Contains(q, StringComparison.OrdinalIgnoreCase);
        // item.IsVisible drives the master-detail pane; flat matched lists are pre-filtered
        item.IsVisible = match;
        if (match) target.Add(item);
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            foreach (var f in _files)
            {
                if (f.Render() == f.OriginalText) continue;
                // the cache is from load time; the server may have rewritten the file meanwhile
                if (File.Exists(f.Path) && File.ReadAllText(f.Path) != f.OriginalText)
                {
                    Status = Path.GetFileName(f.Path) + " changed on disk since loading (server restart?) — press Refresh to re-read it before saving, or your other changes would be lost";
                    return;
                }
                ConfigParser.Save(f);
            }
            IsDirty = false;
            Title = "PZConfigurator";
            foreach (var g in IniGroups.Concat(SandboxGroups))
                foreach (var i in g.Items)
                    i.IsModified = false;
            Status = "Saved " + DateTime.Now.ToString("HH:mm:ss");
        }
        catch (Exception ex)
        {
            Status = "Error saving: " + ex.Message;
        }
    }
}
