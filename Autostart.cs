using System.Diagnostics;
using Microsoft.Win32;

namespace Math2Tex;

/// <summary>
/// User-level Windows autostart via the Run registry key.
/// No admin required; affects only the current user.
/// </summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "Math2Tex";

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                var stored = key?.GetValue(AppName) as string;
                if (string.IsNullOrEmpty(stored)) return false;

                var current = GetExePath();
                if (string.IsNullOrEmpty(current)) return true;
                return stored.Contains(current, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
    }

    public static bool Enable()
    {
        var exe = GetExePath();
        if (string.IsNullOrEmpty(exe)) return false;
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true)!;
            key.SetValue(AppName, $"\"{exe}\"");
            return true;
        }
        catch { return false; }
    }

    public static bool Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            key?.DeleteValue(AppName, throwOnMissingValue: false);
            return true;
        }
        catch { return false; }
    }

    private static string? GetExePath()
    {
        try { return Process.GetCurrentProcess().MainModule?.FileName; }
        catch { return null; }
    }
}
