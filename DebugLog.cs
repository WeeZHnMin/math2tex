using System.IO;

namespace Math2Tex;

internal static class DebugLog
{
    private static readonly object _lock = new();

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Math2Tex",
        "debug.log");

    public static void Write(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff}  {message}";
        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.AppendAllText(FilePath, line + Environment.NewLine);
            }
        }
        catch { /* logging is best-effort */ }
    }
}
