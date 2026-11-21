using PowerPlug.Core.Hotkeys;
using PowerPlug.Core.Logging;
using PowerPlug.Core.Modules;

namespace PowerPlug.Tests;

// Test helpers shared by several test classes.

// Fake keyboard-shortcut service: remembers the callback so a test can "press" the shortcut,
// and can be told to refuse (simulating a shortcut that is already taken).
internal sealed class FakeHotkeys : IHotkeyService
{
    public Action? Callback;
    public bool Accept = true;
    public bool Disposed;
    public bool TryRegister(Hotkey hotkey, Action callback, out IDisposable? registration)
    {
        if (!Accept) { registration = null; return false; }
        Callback = callback;
        registration = new Reg(this);
        return true;
    }
    private sealed class Reg(FakeHotkeys owner) : IDisposable { public void Dispose() => owner.Disposed = true; }
}

// Fake module context: no log output, and the fake shortcut service on request.
internal sealed class FakeContext(IHotkeyService? hotkeys = null) : IModuleContext
{
    public ILog Log => NullLog.Instance;
    public IHotkeyService Hotkeys => hotkeys ?? throw new NotSupportedException();
}
