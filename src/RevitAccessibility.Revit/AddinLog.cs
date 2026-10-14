using RevitAccessibility.Core.Config;

namespace RevitAccessibility.Revit;

internal static class AddinLog
{
    private const long MaxBytes = 512 * 1024;

    public static string FilePath => Path.Combine(AddinConfig.AppDataDir, "log.txt");

    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(AddinConfig.AppDataDir);
            var info = new FileInfo(FilePath);
            if (info.Exists && info.Length > MaxBytes)
                File.Move(FilePath, FilePath + ".old", overwrite: true);
            File.AppendAllText(FilePath, $"{DateTime.Now:s}  {message}{Environment.NewLine}");
        }
        catch { /* logging must never break the command */ }
    }
}
