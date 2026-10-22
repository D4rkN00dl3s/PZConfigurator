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
    public ObservableCollection<SettingGroupVM> Groups { get; } = [];

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
        Groups.Clear();
        _files = [];
        IsDirty = false;
        Title = "PZConfigurator";
        if (_loading || SelectedServer == null) return;

        try
        {
            var markDirty = () => { if (!IsDirty) { IsDirty = true; Title = "PZConfigurator *"; } };
            var iniPath = Path.Combine(Folder, SelectedServer + ".ini");
            var luaPath = Path.Combine(Folder, SelectedServer + "_SandboxVars.lua");
            var files = new[] { ConfigParser.LoadIni(iniPath), ConfigParser.LoadLua(luaPath) };

            foreach (var f in files)
            {
                _files.Add(f);
                foreach (var g in f.Groups)
                {
                    Groups.Add(new SettingGroupVM
                    {
                        Name = g.Name,
                        Items = g.Settings.Select(s => SettingVMFactory.Create(f, s, markDirty)).ToList(),
                    });
                }
            }
            Status = $"Loaded {SelectedServer}: "
                + files.Sum(f => f.Settings.Count()) + " settings";
            ApplyFilter();
        }
        catch (Exception ex)
        {
            Status = "Error loading: " + ex.Message;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var q = SearchText?.Trim();
        foreach (var group in Groups)
            foreach (var item in group.Items)
                item.IsVisible = string.IsNullOrEmpty(q)
                    || item.Label.Contains(q, StringComparison.OrdinalIgnoreCase)
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
