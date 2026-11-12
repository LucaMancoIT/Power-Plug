using System.Runtime.InteropServices;
using System.Windows.Forms;
using PowerPlug.Core.Hotkeys;

namespace PowerPlug.App;

/// <summary>Global hotkeys via RegisterHotKey on a message-only window (no hooks, no polling).</summary>
internal sealed class WinHotkeyService : NativeWindow, IHotkeyService, IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_NOREPEAT = 0x4000;
    private static readonly IntPtr HWND_MESSAGE = new(-3);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly Dictionary<int, Action> _handlers = new();
    private int _nextId = 1;

    public WinHotkeyService() => CreateHandle(new CreateParams { Parent = HWND_MESSAGE });

    public bool TryRegister(Hotkey hotkey, Action callback, out IDisposable? registration)
    {
        var id = _nextId++;
        if (!RegisterHotKey(Handle, id, (uint)hotkey.Modifiers | MOD_NOREPEAT, hotkey.VirtualKey))
        {
            registration = null;
            return false;
        }
        _handlers[id] = callback;
        registration = new Registration(() => { UnregisterHotKey(Handle, id); _handlers.Remove(id); });
        return true;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && _handlers.TryGetValue((int)m.WParam, out var cb))
        {
            try { cb(); } catch { /* a module callback must not kill the message loop */ }
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        foreach (var id in _handlers.Keys.ToList()) UnregisterHotKey(Handle, id);
        _handlers.Clear();
        DestroyHandle();
    }

    private sealed class Registration(Action onDispose) : IDisposable
    {
        private Action? _a = onDispose;
        public void Dispose() { _a?.Invoke(); _a = null; }
    }
}
