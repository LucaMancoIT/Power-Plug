using System.Runtime.InteropServices;
using Microsoft.Win32;
using PowerPlug.Modules.LightSwitch;

namespace PowerPlug.App;

internal sealed class WindowsThemeApi : IThemeApi
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private static readonly IntPtr HWND_BROADCAST = new(0xFFFF);
    private const uint WM_SETTINGCHANGE = 0x001A, SMTO_ABORTIFHUNG = 0x2;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, UIntPtr wParam, string lParam,
        uint flags, uint timeout, out UIntPtr result);

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
            if (apps) key.SetValue("AppsUseLightTheme", value, RegistryValueKind.DWord);
            if (system) key.SetValue("SystemUsesLightTheme", value, RegistryValueKind.DWord);
        }
        // Tell running apps and the shell to re-read the theme.
        SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, UIntPtr.Zero, "ImmersiveColorSet", SMTO_ABORTIFHUNG, 200, out _);
    }

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

    private sealed class Watch(Action onDispose) : IDisposable
    {
        private Action? _a = onDispose;
        public void Dispose() { _a?.Invoke(); _a = null; }
    }
}
