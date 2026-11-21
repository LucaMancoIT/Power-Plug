using Microsoft.Win32;

namespace PowerPlug.App;

/// <summary>
/// "Start with Windows" for the current user only. It adds or removes an entry in the registry list
/// that Windows reads at sign-in (HKCU\...\Run). No administrator rights and no scheduled task needed.
/// </summary>
internal static class Autostart
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PowerPlug";

    /// <summary>Is Power Plug currently in the Windows startup list?</summary>
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(ValueName) is string;
    }

    /// <summary>Adds (true) or removes (false) Power Plug from the startup list.</summary>
    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        if (enabled)
            // The path is quoted so folders with spaces work.
            key.SetValue(ValueName, $"\"{Environment.ProcessPath ?? throw new InvalidOperationException("Process path unknown.")}\"");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false); // no error if it was not there
    }
}
