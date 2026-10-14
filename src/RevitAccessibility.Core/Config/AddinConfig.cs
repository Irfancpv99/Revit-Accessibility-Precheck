using System.Text.Json;

namespace RevitAccessibility.Core.Config;

/// <summary>
/// Parameter names come from %APPDATA%\RevitAccessibility\config.json so the owner can change them
/// without a rebuild. If the file does not exist, it is created with the defaults below so it can be edited.
/// A malformed file is an error, never a silent fallback. A missing parameter in the model yields
/// "Cannot evaluate", never Pass.
/// </summary>
public sealed class AddinConfig
{
    // Defaults are PLACEHOLDERS: the owner must confirm the real names in Revit (CLAUDE.md open items 2 and 3).
    public const string DefaultClearWidthParameter = "Clear Width";
    public const string DefaultAccessDoorParameter = "Access door";

    public string ClearWidthParameter { get; private set; } = DefaultClearWidthParameter;
    public string AccessDoorParameter { get; private set; } = DefaultAccessDoorParameter;

    /// <summary>Where the values came from, for the log and the results window.</summary>
    public string Source { get; private set; } = "built-in defaults";

    public static string AppDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RevitAccessibility");

    public static string ConfigPath => Path.Combine(AppDataDir, "config.json");

    public static AddinConfig Load()
    {
        var path = ConfigPath;
        if (!File.Exists(path))
        {
            var created = new AddinConfig();
            var problem = TryWriteDefaults(path);
            created.Source = problem is null
                ? $"defaults (no file found; wrote a template to {path})"
                : $"defaults (no file found; {problem})";
            return created;
        }

        var cfg = Parse(File.ReadAllText(path), path);
        cfg.Source = path;
        return cfg;
    }

    /// <summary>Parses config JSON. Public so the parsing rules can be tested without Revit.</summary>
    public static AddinConfig Parse(string json, string sourceName = "config")
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{sourceName} is not valid JSON: {ex.Message}", ex);
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException($"{sourceName} must contain a JSON object.");

            var cfg = new AddinConfig();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (string.Equals(prop.Name, "ClearWidthParameter", StringComparison.OrdinalIgnoreCase))
                    cfg.ClearWidthParameter = ReadName(prop, sourceName);
                else if (string.Equals(prop.Name, "AccessDoorParameter", StringComparison.OrdinalIgnoreCase))
                    cfg.AccessDoorParameter = ReadName(prop, sourceName);
            }
            return cfg;
        }
    }

    private static string ReadName(JsonProperty prop, string sourceName)
    {
        var value = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : null;
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"{sourceName}: '{prop.Name}' must be a non-blank string.");
        return value.Trim();
    }

    private static string? TryWriteDefaults(string path)
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            File.WriteAllText(path,
                "{\n" +
                $"  \"ClearWidthParameter\": \"{DefaultClearWidthParameter}\",\n" +
                $"  \"AccessDoorParameter\": \"{DefaultAccessDoorParameter}\"\n" +
                "}\n");
        }
        catch (Exception ex)
        {
            return $"could not write a template config to {path}: {ex.Message}";
        }
        return null;
    }
}
