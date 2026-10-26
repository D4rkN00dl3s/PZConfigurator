namespace PZConfigurator.Config;

public enum SettingKind { Bool, Enum, Int, Float, Text }

/// <summary>ini keys whose values must never be shown in clear text on screen.</summary>
public static class SettingSecrets
{
    public static readonly HashSet<string> Keys = new(StringComparer.Ordinal)
    {
        "Password", "RCONPassword", "DiscordToken", "WebhookAddress",
    };
}

public sealed class EnumOption
{
    public required int Value { get; init; }
    public required string Label { get; init; }
}

/// <summary>One editable setting. The raw file line it lives on is reproduced as LinePrefix + RawValue + LineSuffix,
/// so untouched lines stay byte-identical.</summary>
public sealed class Setting
{
    public required string Key { get; init; }
    public required int LineIndex { get; init; }
    public required string LinePrefix { get; init; }
    public required string LineSuffix { get; init; }
    public required string RawValue { get; set; }
    public string Label { get; set; } = "";
    public string Tooltip { get; set; } = "";
    public SettingKind Kind { get; set; }
    public List<EnumOption> Options { get; set; } = [];
    public double? Min { get; set; }
    public double? Max { get; set; }
    public bool ReadOnly { get; set; }
}

public sealed class SettingGroup
{
    public required string Name { get; init; }
    public required List<Setting> Settings { get; init; }
}

public sealed class ConfigFile
{
    public required string Path { get; init; }
    public required List<string> Lines { get; init; }
    public required string NewLine { get; init; }
    /// <summary>How many newlines terminated the original file (0, 1, ...).</summary>
    public required int TrailingNewlines { get; init; }
    public string OriginalText { get; set; } = "";
    public required bool HasBom { get; init; }
    public required List<SettingGroup> Groups { get; init; }

    public IEnumerable<Setting> Settings => Groups.SelectMany(g => g.Settings);

    public void Update(Setting setting, string newValue)
    {
        setting.RawValue = newValue;
        Lines[setting.LineIndex] = setting.LinePrefix + newValue + setting.LineSuffix;
    }

    public string Render() => string.Join(NewLine, Lines) + string.Concat(Enumerable.Repeat(NewLine, TrailingNewlines));
}
