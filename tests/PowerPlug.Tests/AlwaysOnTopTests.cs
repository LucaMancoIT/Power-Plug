using PowerPlug.Modules.AlwaysOnTop;
using Xunit;

namespace PowerPlug.Tests;

public class AlwaysOnTopTests
{
    private sealed class Api : IWindowApi
    {
        public IntPtr Foreground = new(42);
        public readonly HashSet<IntPtr> Topmost = new();
        public IntPtr GetForegroundWindow() => Foreground;
        public bool IsTopmost(IntPtr w) => Topmost.Contains(w);
        public bool SetTopmost(IntPtr w, bool t) { if (t) Topmost.Add(w); else Topmost.Remove(w); return true; }
    }

    [Fact]
    public async Task Hotkey_toggles_foreground_window()
    {
        var api = new Api(); var keys = new FakeHotkeys();
        var m = new AlwaysOnTopModule(api);
        await m.StartAsync(new FakeContext(keys), CancellationToken.None);
        keys.Callback!();
        Assert.Contains(new IntPtr(42), api.Topmost);
        keys.Callback!();
        Assert.Empty(api.Topmost);
    }

    [Fact]
    public async Task Stop_unpins_all_windows_and_releases_hotkey()
    {
        var api = new Api(); var keys = new FakeHotkeys();
        var m = new AlwaysOnTopModule(api);
        await m.StartAsync(new FakeContext(keys), CancellationToken.None);
        keys.Callback!();
        await m.StopAsync(CancellationToken.None);
        Assert.Empty(api.Topmost);
        Assert.True(keys.Disposed);
    }

    [Fact]
    public async Task Start_fails_clearly_when_hotkey_is_taken()
    {
        var m = new AlwaysOnTopModule(new Api());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            m.StartAsync(new FakeContext(new FakeHotkeys { Accept = false }), CancellationToken.None));
    }

    [Fact]
    public async Task No_foreground_window_is_ignored()
    {
        var api = new Api { Foreground = IntPtr.Zero }; var keys = new FakeHotkeys();
        await new AlwaysOnTopModule(api).StartAsync(new FakeContext(keys), CancellationToken.None);
        keys.Callback!();
        Assert.Empty(api.Topmost);
    }
}
