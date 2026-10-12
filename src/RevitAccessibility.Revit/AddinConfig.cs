using System.Text.Json;

namespace RevitAccessibility.Revit;

/// <summary>
/// Parameter names come from %APPDATA%\RevitAccessibility\config.json so the owner can change them
/// without a rebuild. A missing file yields the defaults below; a missing parameter in the model yields
/// "Cannot evaluate", never Pass.
/// </summary>
public sealed class AddinConfig
{
    // Defaults are PLACEHOLDERS: the owner must confirm the real names in Revit (CLAUDE.md open items 2 and 3).
    public string ClearWidthParameter { get; set; } = "Clear Width";
    public string AccessDoorParameter { get; set; } = "Access door";

    public static string AppDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RevitAccessibility");

    public static AddinConfig Load()
    {
        var path = Path.Combine(AppDataDir, "config.json");
        if (!File.Exists(path)) return new AddinConfig();
        var cfg = JsonSerializer.Deserialize<AddinConfig>(File.ReadAllText(path)) ?? new AddinConfig();
        if (string.IsNullOrWhiteSpace(cfg.ClearWidthParameter) || string.IsNullOrWhiteSpace(cfg.AccessDoorParameter))
            throw new InvalidDataException($"{path}: parameter names must not be blank.");
        return cfg;
    }
}
