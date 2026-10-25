using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using PZConfigurator.Config;

namespace PZConfigurator.ViewModels;

public abstract partial class SettingVM : ObservableObject
{
    public string Key { get; }
    public string Label { get; }
    public string Tooltip { get; }
    protected readonly ConfigFile File;
    protected readonly Setting Setting;
    private readonly Action _markDirty;
    private string _originalRaw; // value at load/save; IsModified compares the current value against this

    protected SettingVM(Setting setting, ConfigFile file, Action markDirty)
    {
        Setting = setting;
        File = file;
        Key = setting.Key;
        Label = string.IsNullOrWhiteSpace(setting.Label) ? setting.Key : setting.Label;
        Tooltip = setting.Tooltip;
        _originalRaw = Setting.RawValue;
        _markDirty = markDirty;
    }

    [ObservableProperty]
    private bool _isModified; // changed since load/save; highlighted in the UI

    protected void Write(string newValue)
    {
        File.Update(Setting, newValue);
        // yellow only when the value actually differs from the last loaded/saved state,
        // so toggling a bool on and off doesn't look like a pending change
        IsModified = newValue.Trim() != _originalRaw.Trim();
        _markDirty();
    }

    /// <summary>baseline reset after Save: the written values are now what's on disk</summary>
    internal void ClearModified()
    {
        _originalRaw = Setting.RawValue;
        IsModified = false;
    }

    private bool _isVisible = true;
    public bool IsVisible
    {
        get => _isVisible;
        set { if (_isVisible != value) { _isVisible = value; OnPropertyChanged(); } }
    }

    [ObservableProperty]
    private string? _matchCategory; // shown in the search matches pane

    public bool HasMatchCategory => MatchCategory != null;
    partial void OnMatchCategoryChanged(string? value) => OnPropertyChanged(nameof(HasMatchCategory));
}

public partial class BoolSettingVM : SettingVM
{
    [ObservableProperty]
    private bool _isChecked;

    public BoolSettingVM(Setting s, ConfigFile file, Action markDirty)
        : base(s, file, markDirty)
    {
        _isChecked = s.RawValue.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    partial void OnIsCheckedChanged(bool value) => Write(value ? "true" : "false");
}

public partial class EnumSettingVM : SettingVM
{
    public List<string> Items { get; }
    private readonly List<EnumOption> _options;

    [ObservableProperty]
    private int _selectedIndex;

    /// <summary>raw file value that isn't in the option list; shown as a hint so it isn't silently overwritten.</summary>
    [ObservableProperty]
    private string? _outOfRangeText;

    public EnumSettingVM(Setting s, ConfigFile file, Action markDirty)
        : base(s, file, markDirty)
    {
        _options = s.Options;
        Items = _options.Select(o => o.Label).ToList();
        _selectedIndex = _options.FindIndex(o => o.Value.ToString() == s.RawValue.Trim());
        if (_selectedIndex < 0)
        {
            var raw = s.RawValue.Trim();
            OutOfRangeText = raw.Length == 0
                ? "(not set — file value not in list)"
                : $"(file has: {raw} — not in list)";
        }
    }

    partial void OnSelectedIndexChanged(int value)
    {
        if (value >= 0 && value < _options.Count)
            Write(_options[value].Value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Selects by the raw numeric value; false when the value isn't in the option list.</summary>
    public bool TrySelectValue(int value)
    {
        var idx = _options.FindIndex(o => o.Value == value);
        if (idx < 0) return false;
        SelectedIndex = idx;
        return true;
    }

    public bool HasOutOfRangeText => OutOfRangeText != null;
    partial void OnOutOfRangeTextChanged(string? value) => OnPropertyChanged(nameof(HasOutOfRangeText));
}

public partial class NumberSettingVM : SettingVM
{
    public bool IsInteger { get; }
    public string Format => IsInteger ? "0" : "0.0######";
    public double MinValue { get; }
    public double MaxValue { get; }

    [ObservableProperty]
    private double? _numberValue;

    public NumberSettingVM(Setting s, ConfigFile file, Action markDirty, bool isInteger)
        : base(s, file, markDirty)
    {
        IsInteger = isInteger;
        MinValue = s.Min ?? double.MinValue;
        MaxValue = s.Max ?? double.MaxValue;
        _numberValue = double.TryParse(s.RawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    partial void OnNumberValueChanged(double? value)
    {
        if (!value.HasValue) return;
        Write(IsInteger
            ? ((long)Math.Round(value.Value)).ToString(CultureInfo.InvariantCulture)
            : value.Value.ToString("0.0#######", CultureInfo.InvariantCulture));
    }
}

public partial class TextSettingVM : SettingVM
{
    public bool IsReadOnly { get; }

    [ObservableProperty]
    private string _text = "";

    public TextSettingVM(Setting s, ConfigFile file, Action markDirty)
        : base(s, file, markDirty)
    {
        IsReadOnly = s.ReadOnly;
        _text = s.RawValue;
    }

    partial void OnTextChanged(string value)
    {
        if (!IsReadOnly) Write(value);
    }
}

public sealed class SettingGroupVM : ObservableObject
{
    private readonly List<SettingVM> _items;
    private int _modified;

    public SettingGroupVM(string name, List<SettingVM> items)
    {
        Name = name;
        _items = items;
        // keeps the category badge ("count of unsaved changes") current
        foreach (var item in items)
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SettingVM.IsModified))
                {
                    _modified = _items.Count(i => i.IsModified);
                    OnPropertyChanged(nameof(ModifiedCount));
                    OnPropertyChanged(nameof(HasModified));
                }
            };
    }

    public string Name { get; }
    public List<SettingVM> Items => _items;
    public int ModifiedCount => _modified;
    public bool HasModified => _modified > 0;
}

public static class SettingVMFactory
{
    public static SettingVM Create(ConfigFile file, Setting s, Action markDirty)
    {
        if (s.ReadOnly)
            return new TextSettingVM(s, file, markDirty);
        return s.Kind switch
        {
        SettingKind.Bool => new BoolSettingVM(s, file, markDirty),
        SettingKind.Enum => new EnumSettingVM(s, file, markDirty),
        SettingKind.Int => new NumberSettingVM(s, file, markDirty, isInteger: true),
        SettingKind.Float => new NumberSettingVM(s, file, markDirty, isInteger: false),
        _ => new TextSettingVM(s, file, markDirty),
        };
    }
}
