namespace RevitAccessibility.Revit;

internal static class AddinLog
{
    public static string Path_ => Path.Combine(AddinConfig.AppDataDir, "log.txt");

    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(AddinConfig.AppDataDir);
            File.AppendAllText(Path_, $"{DateTime.Now:s}  {message}{Environment.NewLine}");
        }
        catch { /* logging must never break the command */ }
    }
}
