namespace PZConfigurator.Config;

/// <summary>Lint for the semicolon-separated Mods / WorkshopItems list values.
/// Only reports always-wrong problems; never warns on count mismatch (one Workshop
/// item can carry several mod IDs, local mods have no Workshop ID).</summary>
public static class ModListLint
{
    public static string? Check(string key, string value)
    {
        switch (key)
        {
            case "Mods":
                return CheckList(value, digitsOnly: false);
            case "WorkshopItems":
                return CheckList(value, digitsOnly: true);
            default:
                return null;
        }
    }

    static string? CheckList(string value, bool digitsOnly)
    {
        // empty lists and the absence of the key are both fine
        if (string.IsNullOrWhiteSpace(value)) return null;

        var entries = value.Split(';');
        var problems = 0;
        string? first = null;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries)
        {
            if (e.Length == 0)
            {
                problems++; first ??= "empty entry";
                continue;
            }
            if (e.Trim().Length != e.Length)
            {
                problems++; first ??= $"whitespace around \"{e.Trim()}\"";
                continue;
            }
            if (!seen.Add(e.Trim()))
            {
                problems++; first ??= $"duplicate \"{e.Trim()}\"";
                continue;
            }
            if (digitsOnly && !e.All(char.IsDigit))
            {
                problems++; first ??= $"non-numeric \"{e.Trim()}\"";
            }
        }

        return problems == 0
            ? null
            : $"{first}" + (problems > 1 ? $" (+{problems - 1} more)" : "");
    }
}
