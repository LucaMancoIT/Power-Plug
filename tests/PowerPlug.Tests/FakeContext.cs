using PowerPlug.Core.Hotkeys;
using PowerPlug.Core.Logging;
using PowerPlug.Core.Modules;

namespace PowerPlug.Tests;

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

internal sealed class FakeContext(IHotkeyService? hotkeys = null) : IModuleContext
{
    public ILog Log => NullLog.Instance;
    public IHotkeyService Hotkeys => hotkeys ?? throw new NotSupportedException();
}
