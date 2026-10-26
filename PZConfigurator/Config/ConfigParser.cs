using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PZConfigurator.Config;

/// <summary>Raised when the on-disk file no longer matches the copy loaded into the app.</summary>
public sealed class ConfigChangedOnDiskException(string path) : IOException(
    Path.GetFileName(path) + " changed on disk since loading (server restart?) — press Refresh to re-read it before saving, or your other changes would be lost");

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

    /// <summary>How many timestamped history backups are kept per file.</summary>
    public const int BackupHistoryCount = 10;

    public static void Save(ConfigFile file) => SaveAll([file]);

    /// <summary>All-or-nothing save of a config pair. Phase 1 disk-checks every changed file,
    /// phase 2 makes backups + temp writes, phase 3 renames (rolling back from .bak on
    /// failure); only when EVERY rename succeeded does OriginalText move forward.</summary>
    public static void SaveAll(IReadOnlyList<ConfigFile> files)
    {
        // phase 1: nothing is written until every changed file has passed the disk-match guard
        var changed = new List<(ConfigFile file, string text)>();
        foreach (var f in files)
        {
            var text = f.Render();
            if (text == f.OriginalText) continue;
            // the cache is from load time; the server may have rewritten the file meanwhile
            if (File.Exists(f.Path) && File.ReadAllText(f.Path) != f.OriginalText)
                throw new ConfigChangedOnDiskException(f.Path);
            changed.Add((f, text));
        }
        if (changed.Count == 0) return;
        var paths = changed.Select(c => c.file.Path).ToList();

        try
        {
            // phase 2: per-file backups and temp writes; renaming happens only afterwards
            foreach (var (f, text) in changed)
            {
                File.Copy(f.Path, f.Path + ".bak", overwrite: true);
                CreateTimestampedBackup(f.Path);
                WriteTemp(f.Path, text, f.HasBom);
            }

            // phase 3: install one by one; restore from .bak when anything fails mid-way
            for (var i = 0; i < changed.Count; i++)
            {
                var path = changed[i].file.Path;
                try
                {
                    BeforeRename?.Invoke(path);
                    File.Move(path + ".tmp", path, overwrite: true);
                }
                catch (Exception failure)
                {
                    var rollback = new List<string>();
                    for (var j = 0; j < i; j++)
                    {
                        try { File.Copy(paths[j] + ".bak", paths[j], overwrite: true); }
                        catch { rollback.Add(Path.GetFileName(paths[j])); }
                    }
                    // whatever the failure is, its message must tell the user which state survived
                    var msg = rollback.Count > 0
                        ? $"{Path.GetFileName(path)} could not be saved ({failure.Message}); the earlier saves of {string.Join(", ", rollback)} could not be rolled back and remain written"
                        : $"{Path.GetFileName(path)} could not be saved ({failure.Message}); already-saved files were restored from their .bak";
                    throw new IOException(msg, failure);
                }
            }
        }
        finally
        {
            // every failure path leaves no .tmp behind
            foreach (var p in paths)
                try { if (File.Exists(p + ".tmp")) File.Delete(p + ".tmp"); }
                catch { /* best effort */ }
        }

        // phase 4: after ALL renames succeeded, the files on disk ARE the new originals
        foreach (var (f, text) in changed)
            f.OriginalText = text;

        static void WriteTemp(string path, string text, bool bom)
        {
            File.WriteAllText(path + ".tmp", text, new UTF8Encoding(bom));
            if (OperatingSystem.IsWindows()) return;
            // rename installs a fresh inode, so the new file would get umask defaults;
            // carry the original mode over, but never fail the save over it
            try { File.SetUnixFileMode(path + ".tmp", File.GetUnixFileMode(path)); }
            catch (Exception) { /* unsupported filesystem etc. */ }
        }
    }

    /// <summary>Test seam called with the target path just before each rename; null in production.</summary>
    internal static Action<string>? BeforeRename;

    /// <summary>Test seam for the timestamp clock; production stays on the real UTC clock.</summary>
    internal static Func<DateTime> UtcNow = () => DateTime.UtcNow;

    /// <summary>Copy the pre-save content to a timestamped history backup, then prune to
    /// BackupHistoryCount. Best effort: the save must never fail because of history.</summary>
    static void CreateTimestampedBackup(string path)
    {
        try
        {
            // e.g. myserver.ini.20261026-153012-417.bak
            var stamp = UtcNow().ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            var history = path + "." + stamp + ".bak";
            // a collision means a save happened in the same millisecond — skip rather than fail
            if (!File.Exists(history))
                File.Copy(path, history);

            var dir = Path.GetDirectoryName(path)!;
            var name = Path.GetFileName(path);
            var historyRegex = new Regex(
                "^" + Regex.Escape(name) + @"\.\d{8}-\d{6}-\d{3}\.bak$");
            // file names sort chronologically, so pruning all but the highest keeps the newest
            var old = Directory.EnumerateFiles(dir)
                .Select(System.IO.Path.GetFileName)
                .Where(f => f != null && historyRegex.IsMatch(f))
                .OrderByDescending(f => f, StringComparer.Ordinal)
                .Skip(BackupHistoryCount)
                .Select(f => System.IO.Path.Combine(dir, f!))
                .ToList();
            foreach (var f in old)
                File.Delete(f);
        }
        catch (Exception) { /* history is a luxury; saving must not fail over it */ }
    }
}
