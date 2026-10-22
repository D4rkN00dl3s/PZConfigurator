using System.Collections.ObjectModel;
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
                ConfigParser.Save(f);
            }
            IsDirty = false;
            Title = "PZConfigurator";
            Status = "Saved " + DateTime.Now.ToString("HH:mm:ss");
        }
        catch (Exception ex)
        {
            Status = "Error saving: " + ex.Message;
        }
    }
}
