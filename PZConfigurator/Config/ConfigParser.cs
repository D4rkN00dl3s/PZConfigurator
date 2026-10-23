using System.Text;
using System.Text.RegularExpressions;

namespace PZConfigurator.Config;

public static partial class ConfigParser
{
    [GeneratedRegex(@"^\s*(-?\d+)\s*=\s*(.+?)\s*$")]
    private static partial Regex OptionRegex();

    [GeneratedRegex(@"Min:\s*(-?\d+(?:\.\d+)?)\s+Max:\s*(-?\d+(?:\.\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex MinMaxRegex();

    [GeneratedRegex(@"Default:\s*(-?\d+(?:\.\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex DefaultRegex();

    [GeneratedRegex(@"^-?\d+\.\d+$")]
    private static partial Regex FloatRegex();

    [GeneratedRegex(@"^-?\d+$")]
    private static partial Regex IntRegex();

    static readonly Dictionary<string, bool> BoolMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["true"] = true, ["false"] = false,
    };

    public static ConfigFile LoadIni(string path)
    {
        var lines = ReadLines(path, out var nl, out var bom, out var original, out var trailingNewlines);
        var settings = new List<Setting>();
        var comments = new List<string>();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith('#'))
            {
                comments.Add(StripComment(line));
                continue;
            }
            if (trimmed.Length == 0) { comments.Clear(); continue; }

            var eq = line.IndexOf('=');
            if (eq <= 0) { comments.Clear(); continue; }
            var key = line[..eq].Trim();
            var s = new Setting
            {
                Key = key,
                LineIndex = i,
                LinePrefix = line[..(eq + 1)],
                LineSuffix = "",
                RawValue = line[(eq + 1)..],
            };
            InitSetting(s, comments);
            settings.Add(s);
            comments.Clear();
        }

        return new ConfigFile
        {
            Path = path,
            Lines = lines,
            NewLine = nl,
            TrailingNewlines = trailingNewlines,
            OriginalText = original,
            HasBom = bom,
            Groups = [new SettingGroup { Name = "Server Settings", Settings = settings }],
        };
    }

    public static ConfigFile LoadLua(string path)
    {
        var lines = ReadLines(path, out var nl, out var bom, out var original, out var trailingNewlines);
        var groups = new List<SettingGroup>();
        var current = new SettingGroup { Name = "General", Settings = [] };
        groups.Add(current);
        // depth 1 = SandboxVars root; nesting beyond that opens a named section group
        var depth = 0;
        var stack = new List<SettingGroup>(); // open sections
        var comments = new List<string>();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("--"))
            {
                comments.Add(StripComment(line));
                continue;
            }
            if (trimmed.Length == 0) { comments.Clear(); continue; }

            var open = TryParseSectionOpen(trimmed);
            if (open != null)
            {
                depth++;
                if (depth > 1) // "SandboxVars = {" itself is the root, not a group
                {
                    var group = new SettingGroup { Name = open, Settings = [] };
                    groups.Add(group);
                    stack.Add(group);
                    current = group;
                }
                continue;
            }
            if (trimmed.StartsWith('}'))
            {
                depth = Math.Max(0, depth - 1);
                if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                current = stack.Count > 0 ? stack[^1] : groups[0];
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq <= 0) { comments.Clear(); continue; }
            var key = line[..eq].Trim();
            var rest = line[(eq + 1)..];
            // keep the whitespace between '=' and the value as part of the prefix so the
            // original spacing survives a rewrite
            var lead = rest.Length - rest.TrimStart().Length;
            var prefix = line[..(eq + 1)] + rest[..lead];
            var valuePart = rest[lead..].TrimEnd();
            var suffix = "";
            if (valuePart.EndsWith(','))
            {
                suffix = ",";
                valuePart = valuePart[..^1].TrimEnd();
            }
            var s = new Setting
            {
                Key = key,
                LineIndex = i,
                LinePrefix = prefix,
                LineSuffix = suffix,
                RawValue = valuePart.TrimStart(),
            };
            InitSetting(s, comments);
            current.Settings.Add(s);
            comments.Clear();
        }

        return new ConfigFile
        {
            Path = path,
            Lines = lines,
            NewLine = nl,
            TrailingNewlines = trailingNewlines,
            OriginalText = original,
            HasBom = bom,
            Groups = groups,
        };
    }

    static string? TryParseSectionOpen(string trimmed)
    {
        // "Name = {" optionally followed by "," — root line is "SandboxVars = {"
        if (!trimmed.EndsWith('{')) return null;
        var eq = trimmed.IndexOf('=');
        if (eq <= 0) return null;
        var key = trimmed[..eq].Trim();
        if (Regex.IsMatch(key, @"^\w+$")) return key;
        return null;
    }

    static void InitSetting(Setting s, List<string> comments)
    {
        if (s.Key == "VERSION") s.ReadOnly = true;

        var options = new List<EnumOption>();
        var descriptive = new List<string>();
        foreach (var comment in comments)
        {
            var m = OptionRegex().Match(comment);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var n))
                options.Add(new EnumOption { Value = n, Label = m.Groups[2].Value });
            else
                descriptive.Add(comment);
        }

        var mm = MinMaxRegex().Match(string.Join(" ", comments));
        if (mm.Success)
        {
            s.Min = double.Parse(mm.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            s.Max = double.Parse(mm.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        }
        var dm = DefaultRegex().Match(string.Join(" ", comments));
        string? defaultHint = dm.Success ? dm.Groups[1].Value : null;

        s.Options = options;
        s.Label = descriptive.FirstOrDefault(l => l.Length > 0) ?? "";
        var toolParts = descriptive.Where(l => l.Length > 0).ToList();
        if (defaultHint != null) toolParts.Add("Default: " + defaultHint);
        s.Tooltip = string.Join("\n", toolParts);

        if (BoolMap.TryGetValue(s.RawValue.Trim(), out _))
            s.Kind = SettingKind.Bool;
        else if (options.Count >= 2 && IntRegex().IsMatch(s.RawValue))
            s.Kind = SettingKind.Enum;
        else if (IntRegex().IsMatch(s.RawValue))
            s.Kind = SettingKind.Int;
        else if (FloatRegex().IsMatch(s.RawValue))
            s.Kind = SettingKind.Float;
        else
            s.Kind = SettingKind.Text;
    }

    static string StripComment(string line)
    {
        var t = line.TrimStart();
        if (t.StartsWith("--")) return t[2..].TrimStart();
        if (t.StartsWith('#')) return t.TrimStart('#').TrimStart();
        return t;
    }

    static List<string> ReadLines(string path, out string newLine, out bool bom, out string original, out int trailingNewlines)
    {
        var bytes = File.ReadAllBytes(path);
        bom = bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble());
        // GetString doesn't strip the BOM; ReadAllText did. Render/Save re-add the preamble.
        original = new UTF8Encoding(false)
            .GetString(bom ? bytes.AsSpan(Encoding.UTF8.GetPreamble().Length) : bytes);
        newLine = original.Contains("\r\n") ? "\r\n" : "\n";
        var normalized = original.Replace("\r\n", "\n");
        trailingNewlines = normalized.Length - normalized.TrimEnd('\n').Length;
        var lines = normalized.TrimEnd('\n').Split('\n').ToList();
        return lines;
    }

    public static void Save(ConfigFile file)
    {
        var text = file.Render();
        if (text == file.OriginalText) return; // unchanged: don't rewrite/roll the backup
        File.Copy(file.Path, file.Path + ".bak", overwrite: true);
        var tmp = file.Path + ".tmp";
        File.WriteAllText(tmp, text, new UTF8Encoding(file.HasBom));
        File.Move(tmp, file.Path, overwrite: true);
        // keep "Render == OriginalText" meaningful: after save the file IS the original
        file.OriginalText = text;
    }
}
