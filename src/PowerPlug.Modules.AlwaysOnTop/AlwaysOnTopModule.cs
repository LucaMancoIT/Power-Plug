using System.Runtime.InteropServices;
using PowerPlug.Core.Hotkeys;
using PowerPlug.Core.Modules;

namespace PowerPlug.Modules.AlwaysOnTop;

/// <summary>
/// The Windows window functions this module needs, wrapped in an interface so the logic
/// can be tested on any computer with a fake version.
/// </summary>
public interface IWindowApi
{
    /// <summary>The window the user is currently working in (0 if there is none).</summary>
    IntPtr GetForegroundWindow();

    /// <summary>Is this window currently pinned above all others?</summary>
    bool IsTopmost(IntPtr window);

    /// <summary>Pins (true) or unpins (false) a window. Returns false if Windows refused.</summary>
    bool SetTopmost(IntPtr window, bool topmost);
}

/// <summary>The real version: talks to Windows (user32.dll). A window is identified by its "handle" (IntPtr).</summary>
public sealed class WindowsWindowApi : IWindowApi
{
    // Constants defined by Windows. Their values come from the official Windows headers.
    private const int GWL_EXSTYLE = -20;          // "read the extended window style"
    private const long WS_EX_TOPMOST = 0x8;       // style flag: window is always on top
    private const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10; // "change nothing but the z-order"
    private static readonly IntPtr HWND_TOPMOST = new(-1), HWND_NOTOPMOST = new(-2);

    [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")] private static extern IntPtr GetFg();
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    public IntPtr GetForegroundWindow() => GetFg();
    public bool IsTopmost(IntPtr window) => ((long)GetWindowLongPtr(window, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0;

    // Moves the window in the stacking order only: position, size and focus stay untouched.
    public bool SetTopmost(IntPtr window, bool topmost) =>
        SetWindowPos(window, topmost ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
}

/// <summary>
/// Win+Ctrl+T pins or unpins the active window above all others.
/// When the module is switched off, every window it pinned is released again.
/// </summary>
public sealed class AlwaysOnTopModule(IWindowApi api) : IModule
{
    /// <summary>Technical name, used as the key in settings.json.</summary>
    public const string Id = "always-on-top";

    /// <summary>The keyboard shortcut: Windows key + Ctrl + T (0x54 is the key code of the letter T).</summary>
    public static readonly Hotkey DefaultHotkey = new(HotkeyModifiers.Win | HotkeyModifiers.Control, 0x54 /* T */);

    private readonly HashSet<IntPtr> _pinned = new(); // windows pinned by us, so we can release them later
    private readonly object _lock = new();
    private IDisposable? _registration;               // our claim on the keyboard shortcut

    public Task StartAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        if (!context.Hotkeys.TryRegister(DefaultHotkey, Toggle, out _registration))
            throw new InvalidOperationException("Hotkey Win+Ctrl+T is already in use.");
        return Task.CompletedTask;
    }

    /// <summary>Pins the active window if it is not pinned yet, otherwise unpins it.</summary>
    public void Toggle()
    {
        lock (_lock)
        {
            var window = api.GetForegroundWindow();
            if (window == IntPtr.Zero) return;                  // no active window: nothing to do
            var makeTopmost = !api.IsTopmost(window);
            if (!api.SetTopmost(window, makeTopmost)) return;   // Windows said no: do not remember it
            if (makeTopmost) _pinned.Add(window); else _pinned.Remove(window);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _registration?.Dispose(); // give the keyboard shortcut back
        _registration = null;
        lock (_lock)
        {
            foreach (var window in _pinned) api.SetTopmost(window, false); // best effort: the window may be gone
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
