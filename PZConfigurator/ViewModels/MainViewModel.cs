using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZConfigurator.Config;

namespace PZConfigurator.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public const string DefaultFolderName = "Zomboid"; // under the user profile

    public ObservableCollection<string> Servers { get; } = [];
    public ObservableCollection<SettingGroupVM> IniGroups { get; } = [];
    public ObservableCollection<SettingGroupVM> SandboxGroups { get; } = [];

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
    private string _title = "PZConfigurator";

    private List<ConfigFile> _files = [];
    private bool _loading;

    public MainViewModel() => Discover();

    [RelayCommand]
    private void RefreshServerList() => Discover();

    public void SetFolder(string path)
    {
        Folder = path;
        Discover();
    }

    private void Discover()
    {
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
        Load();
    }

    partial void OnSelectedServerChanged(string? value) => Load();

    private void Load()
    {
        IniGroups.Clear();
        SandboxGroups.Clear();
        _files = [];
        IsDirty = false;
        Title = "PZConfigurator";
        if (_loading || SelectedServer == null) return;

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

            Status = $"Loaded {SelectedServer}: "
                + (ini.Settings.Count() + lua.Settings.Count()) + " settings";
            ApplyFilter();
        }
        catch (Exception ex)
        {
            Status = "Error loading: " + ex.Message;
        }
    }

    private void BuildGroups(
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
        foreach (var group in IniGroups.Concat(SandboxGroups))
            foreach (var item in group.Items)
                item.IsVisible = string.IsNullOrEmpty(q)
                    || item.Key.Contains(q, StringComparison.OrdinalIgnoreCase);
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
