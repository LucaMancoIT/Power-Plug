using System.Runtime.InteropServices;
using System.Windows.Forms;
using PowerPlug.Core.Hotkeys;

namespace PowerPlug.App;

/// <summary>
/// Global keyboard shortcuts via the Windows function RegisterHotKey.
/// It uses an invisible "message-only" window that only receives messages: no keyboard hook
/// and no polling, so it costs practically nothing while idle.
/// </summary>
internal sealed class WinHotkeyService : NativeWindow, IHotkeyService, IDisposable
{
    // Constants defined by Windows.
    private const int WM_HOTKEY = 0x0312;       // message Windows sends when a registered shortcut is pressed
    private const uint MOD_NOREPEAT = 0x4000;   // do not fire repeatedly while the keys are held down
    private static readonly IntPtr HWND_MESSAGE = new(-3); // parent value that creates a message-only window

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly Dictionary<int, Action> _handlers = new(); // shortcut id -> what to do when pressed
    private int _nextId = 1;

    public WinHotkeyService() => CreateHandle(new CreateParams { Parent = HWND_MESSAGE });

    public bool TryRegister(Hotkey hotkey, Action callback, out IDisposable? registration)
    {
        var id = _nextId++;
        // Windows refuses if another program already uses this shortcut.
        if (!RegisterHotKey(Handle, id, (uint)hotkey.Modifiers | MOD_NOREPEAT, hotkey.VirtualKey))
        {
            registration = null;
            return false;
        }
        _handlers[id] = callback;
        registration = new Registration(() => { UnregisterHotKey(Handle, id); _handlers.Remove(id); });
        return true;
    }

    // Called by Windows for every message sent to our hidden window.
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && _handlers.TryGetValue((int)m.WParam, out var cb))
        {
            try { cb(); } catch { /* a faulty module callback must not stop the message loop */ }
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        foreach (var id in _handlers.Keys.ToList()) UnregisterHotKey(Handle, id);
        _handlers.Clear();
        DestroyHandle();
    }

    // Small helper object: disposing it releases the shortcut again.
    private sealed class Registration(Action onDispose) : IDisposable
    {
        private Action? _a = onDispose;
        public void Dispose() { _a?.Invoke(); _a = null; }
    }
}
