using Microsoft.Win32;

namespace PowerPlug.App;

/// <summary>Per-user autostart via HKCU\...\Run (no admin rights, no scheduled task).</summary>
internal static class Autostart
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PowerPlug";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(ValueName) is string;
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        if (enabled)
            key.SetValue(ValueName, $"\"{Environment.ProcessPath ?? throw new InvalidOperationException("Process path unknown.")}\"");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
