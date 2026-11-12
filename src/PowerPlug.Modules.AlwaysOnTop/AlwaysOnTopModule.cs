using System.Runtime.InteropServices;
using PowerPlug.Core.Hotkeys;
using PowerPlug.Core.Modules;

namespace PowerPlug.Modules.AlwaysOnTop;

/// <summary>Abstraction over Win32 window calls so the logic is testable off-Windows.</summary>
public interface IWindowApi
{
    IntPtr GetForegroundWindow();
    bool IsTopmost(IntPtr window);
    bool SetTopmost(IntPtr window, bool topmost);
}

public sealed class WindowsWindowApi : IWindowApi
{
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOPMOST = 0x8;
    private const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10;
    private static readonly IntPtr HWND_TOPMOST = new(-1), HWND_NOTOPMOST = new(-2);

    [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")] private static extern IntPtr GetFg();
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    public IntPtr GetForegroundWindow() => GetFg();
    public bool IsTopmost(IntPtr window) => ((long)GetWindowLongPtr(window, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0;
    public bool SetTopmost(IntPtr window, bool topmost) =>
        SetWindowPos(window, topmost ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
}

/// <summary>Win+Ctrl+T pins/unpins the active window above all others. Pins are removed when the module stops.</summary>
public sealed class AlwaysOnTopModule(IWindowApi api) : IModule
{
    public const string Id = "always-on-top";
    public static readonly Hotkey DefaultHotkey = new(HotkeyModifiers.Win | HotkeyModifiers.Control, 0x54 /* T */);

    private readonly HashSet<IntPtr> _pinned = new();
    private readonly object _lock = new();
    private IDisposable? _registration;

    public Task StartAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        if (!context.Hotkeys.TryRegister(DefaultHotkey, Toggle, out _registration))
            throw new InvalidOperationException("Hotkey Win+Ctrl+T is already in use.");
        return Task.CompletedTask;
    }

    public void Toggle()
    {
        lock (_lock)
        {
            var window = api.GetForegroundWindow();
            if (window == IntPtr.Zero) return;
            var makeTopmost = !api.IsTopmost(window);
            if (!api.SetTopmost(window, makeTopmost)) return;
            if (makeTopmost) _pinned.Add(window); else _pinned.Remove(window);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _registration?.Dispose();
        _registration = null;
        lock (_lock)
        {
            foreach (var window in _pinned) api.SetTopmost(window, false); // best effort: window may be gone
            _pinned.Clear();
        }
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _registration?.Dispose();
        return ValueTask.CompletedTask;
    }
}
