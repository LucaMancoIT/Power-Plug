using System.Runtime.InteropServices;
using Microsoft.Win32;
using PowerPlug.Modules.LightSwitch;

namespace PowerPlug.App;

/// <summary>
/// Reads and changes the Windows light/dark theme.
/// Windows stores the theme in the registry (its settings database) for the current user.
/// This lives in the App project because the registry only exists on Windows.
/// </summary>
internal sealed class WindowsThemeApi : IThemeApi
{
    // Registry location of the theme settings. Value 1 = light, 0 = dark.
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    // Constants defined by Windows for the "settings changed" announcement.
    private static readonly IntPtr HWND_BROADCAST = new(0xFFFF); // "send to all top-level windows"
    private const uint WM_SETTINGCHANGE = 0x001A, SMTO_ABORTIFHUNG = 0x2;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, UIntPtr wParam, string lParam,
        uint flags, uint timeout, out UIntPtr result);

    /// <summary>Which theme apps currently use. If the value is missing, Windows defaults to light.</summary>
    public Theme GetCurrent()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue("AppsUseLightTheme") is int v && v == 0 ? Theme.Dark : Theme.Light;
    }

    public void Set(Theme theme, bool apps, bool system)
    {
        using (var key = Registry.CurrentUser.CreateSubKey(KeyPath))
        {
            var value = theme == Theme.Light ? 1 : 0;
            if (apps) key.SetValue("AppsUseLightTheme", value, RegistryValueKind.DWord);       // Explorer, Settings, ...
            if (system) key.SetValue("SystemUsesLightTheme", value, RegistryValueKind.DWord);  // taskbar, Start menu
        }
        // Tell running programs and the desktop to re-read the theme so the change shows immediately.
        // A time limit (200 ms) and "abort if hung" make sure a frozen program cannot block us.
        SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, UIntPtr.Zero, "ImmersiveColorSet", SMTO_ABORTIFHUNG, 200, out _);
    }

    /// <summary>
    /// Starts listening for "clock was changed" and "PC woke up from sleep" events.
    /// Dispose the returned object to stop listening.
    /// </summary>
    public IDisposable WatchClockChanges(Action onChange)
    {
        EventHandler timeChanged = (_, _) => onChange();
        PowerModeChangedEventHandler powerChanged = (_, e) => { if (e.Mode == PowerModes.Resume) onChange(); };
        SystemEvents.TimeChanged += timeChanged;
        SystemEvents.PowerModeChanged += powerChanged;
        return new Watch(() =>
        {
            SystemEvents.TimeChanged -= timeChanged;
            SystemEvents.PowerModeChanged -= powerChanged;
        });
    }

    // Small helper object: disposing it removes the event listeners again.
    private sealed class Watch(Action onDispose) : IDisposable
    {
        private Action? _a = onDispose;
        public void Dispose() { _a?.Invoke(); _a = null; }
    }
}
