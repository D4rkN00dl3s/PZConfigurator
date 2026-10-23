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
