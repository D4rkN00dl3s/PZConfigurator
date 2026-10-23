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

    protected SettingVM(Setting setting, ConfigFile file, Action markDirty)
    {
        Setting = setting;
        File = file;
        Key = setting.Key;
        Label = string.IsNullOrWhiteSpace(setting.Label) ? setting.Key : setting.Label;
        Tooltip = setting.Tooltip;
        _markDirty = markDirty;
    }

    [ObservableProperty]
    private bool _isModified; // changed since load/save; highlighted in the UI

    protected void Write(string newValue)
    {
        File.Update(Setting, newValue);
        IsModified = true;
        _markDirty();
    }

    private bool _isVisible = true;
    public bool IsVisible
    {
        get => _isVisible;
        set { if (_isVisible != value) { _isVisible = value; OnPropertyChanged(); } }
    }
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

    public EnumSettingVM(Setting s, ConfigFile file, Action markDirty)
        : base(s, file, markDirty)
    {
        _options = s.Options;
        Items = _options.Select(o => o.Label).ToList();
        _selectedIndex = _options.FindIndex(o => o.Value.ToString() == s.RawValue.Trim());
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

public sealed class SettingGroupVM
{
    public required string Name { get; init; }
    public required List<SettingVM> Items { get; init; }
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
