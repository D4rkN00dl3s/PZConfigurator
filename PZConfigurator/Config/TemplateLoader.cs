using System.IO;
using System.Text.Json;
using Avalonia.Platform;

namespace PZConfigurator.Config;

/// <summary>A game-mode or user template: a set of sandbox-var values to load into the editor.</summary>
public sealed class SandboxTemplate
{
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    /// <summary>Key → value literal exactly as written in the file ("4", "true", "0.8").</summary>
    public required Dictionary<string, string> Values { get; init; }
}

public static class TemplateLoader
{
    // These JSONs are a one-time snapshot of the current game build's presets
    // (GameRoot/media/lua/shared/Sandbox/*.lua). A live extractor reading the
    // game install may be tried later — for now the values are refreshed by
    // editing/regenerating the JSONs (see AGENTS.md).
    static readonly string[] EmbeddedNames = ["Apocalypse", "Extinction", "Outbreak", "Rising", "SixMonthsLater"];

    public static string UserTemplateFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "PZConfigurator", "Templates");

    public static string ZomboidPresetFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Zomboid", "Sandbox Presets");

    /// <summary>PZ's own saved sandbox presets (Zomboid\Sandbox Presets\*.cfg). Same key/value
    /// names as the SandboxVars ("Zombies=4"), comment (#) and option-list (--) lines skipped,
    /// "Version=" is the cfg-format header, not a setting.</summary>
    public static List<SandboxTemplate> LoadZomboidPresets(string folder, List<string> errors)
    {
        var list = new List<SandboxTemplate>();
        if (!Directory.Exists(folder)) return list;
        foreach (var path in Directory.EnumerateFiles(folder, "*.cfg"))
        {
            try
            {
                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var raw in File.ReadLines(path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#' || line.StartsWith("--", StringComparison.Ordinal))
                        continue;
                    var eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    var key = line[..eq].Trim();
                    if (key.Equals("Version", StringComparison.OrdinalIgnoreCase)) continue;
                    values[key] = line[(eq + 1)..].Trim();
                }
                if (values.Count == 0) continue;
                list.Add(new SandboxTemplate
                {
                    Name = Path.GetFileNameWithoutExtension(path)!,
                    Description = $"Saved sandbox preset from Zomboid ({values.Count} values)",
                    Values = values,
                });
            }
            catch (Exception ex)
            {
                errors.Add(Path.GetFileName(path) + ": " + ex.Message);
            }
        }
        return list;
    }

    public static List<SandboxTemplate> LoadEmbedded(out List<string> errors)
    {
        var list = new List<SandboxTemplate>();
        var errs = new List<string>();
        foreach (var n in EmbeddedNames)
        {
            try
            {
                using var stream = AssetLoader.Open(new Uri($"avares://PZConfigurator/Templates/{n}.json"));
                using var doc = JsonDocument.Parse(stream);
                list.Add(Parse(doc.RootElement, n));
            }
            catch (Exception ex)
            {
                errs.Add($"{n}: {ex.Message}");
            }
        }
        errors = errs;
        return list;
    }

    /// <summary>The values a fresh server ships with (the Apocalypse preset — "Vanilla preset
    /// from the Project Zomboid game files"). Used to highlight non-default settings.
    /// Calls Parse so the "values" literalization stays in one place; embedded, so it always exists.</summary>
    public static Dictionary<string, string> VanillaDefaults()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://PZConfigurator/Templates/Apocalypse.json"));
            using var doc = JsonDocument.Parse(stream);
            return Parse(doc.RootElement, "Apocalypse").Values;
        }
        catch
        {
            return [];
        }
    }

    public static List<SandboxTemplate> LoadUserFolder(string folder, List<string> errors)
    {
        var list = new List<SandboxTemplate>();
        if (!Directory.Exists(folder)) return list;
        foreach (var path in Directory.EnumerateFiles(folder, "*.json"))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                list.Add(Parse(doc.RootElement, Path.GetFileNameWithoutExtension(path)));
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }
        return list;
    }

    static SandboxTemplate Parse(JsonElement root, string fallbackName)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root.TryGetProperty("values", out var v) && v.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in v.EnumerateObject())
            {
                var val = prop.Value.ValueKind switch
                {
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => prop.Value.ToString(),
                };
                values[prop.Name] = val ?? "";
            }
        }
        var name = root.TryGetProperty("name", out var nm) && !string.IsNullOrWhiteSpace(nm.GetString())
            ? nm.GetString()!
            : fallbackName;
        return new SandboxTemplate
        {
            Name = name,
            Description = root.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
            Values = values,
        };
    }
}
