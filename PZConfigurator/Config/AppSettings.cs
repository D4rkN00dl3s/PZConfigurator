using System.IO;
using System.Text.Json;

namespace PZConfigurator.Config;

/// <summary>Best-effort app state (window geometry, last folder/server) stored next to the user templates.</summary>
public sealed class AppSettings
{
    public string? Folder { get; init; }
    public string? Server { get; init; }
    public int? X { get; init; }
    public int? Y { get; init; }
    public double? Width { get; init; }
    public double? Height { get; init; }
    public bool Maximized { get; init; }

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "PZConfigurator", "settings.json");

    public static AppSettings? Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
        }
        catch (Exception)
        {
            return null; // corrupt/unwritable settings must never block startup
        }
    }

    public static void Save(AppSettings s)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception)
        {
            // best effort; disk errors are not worth crashing exit over
        }
    }
}
