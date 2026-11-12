using PowerPlug.Core.Hotkeys;
using PowerPlug.Core.Logging;
using PowerPlug.Core.Modules;
using Xunit;

namespace PowerPlug.Tests;

public class ModuleHostTests
{
    private sealed class Ctx : IModuleContext
    {
        public ILog Log => NullLog.Instance;
        public IHotkeyService Hotkeys => throw new NotSupportedException();
    }

    private sealed class FakeModule(Action? onStart = null) : IModule
    {
        public int Starts, Stops, Disposes;
        public Task StartAsync(IModuleContext c, CancellationToken ct) { Starts++; onStart?.Invoke(); return Task.CompletedTask; }
        public Task StopAsync(CancellationToken ct) { Stops++; return Task.CompletedTask; }
        public ValueTask DisposeAsync() { Disposes++; return ValueTask.CompletedTask; }
    }

    [Fact]
    public async Task Disabled_module_is_never_instantiated()
    {
        var created = 0;
        await using var host = new ModuleHost(new Ctx());
        host.Register(new ModuleDescriptor("a", "A", () => { created++; return new FakeModule(); }));
        await host.StartEnabledAsync(new Dictionary<string, bool>());
        Assert.Equal(0, created);
    }

    [Fact]
    public async Task Enable_then_disable_runs_full_lifecycle()
    {
        var m = new FakeModule();
        await using var host = new ModuleHost(new Ctx());
        host.Register(new ModuleDescriptor("a", "A", () => m));
        Assert.True(await host.EnableAsync("a"));
        Assert.Equal(ModuleState.Running, host.GetStatus().Single().State);
        await host.DisableAsync("a");
        Assert.Equal((1, 1, 1), (m.Starts, m.Stops, m.Disposes));
        Assert.Equal(ModuleState.Stopped, host.GetStatus().Single().State);
    }

    [Fact]
    public async Task Failing_module_is_isolated_and_marked_faulted()
    {
        var good = new FakeModule();
        await using var host = new ModuleHost(new Ctx());
        host.Register(new ModuleDescriptor("bad", "Bad", () => new FakeModule(() => throw new InvalidOperationException("boom"))));
        host.Register(new ModuleDescriptor("good", "Good", () => good));
        Assert.False(await host.EnableAsync("bad"));
        Assert.True(await host.EnableAsync("good"));
        var status = host.GetStatus().ToDictionary(s => s.Id);
        Assert.Equal(ModuleState.Faulted, status["bad"].State);
        Assert.Equal("boom", status["bad"].Error);
        Assert.Equal(ModuleState.Running, status["good"].State);
    }

    [Fact]
    public async Task Enable_is_idempotent()
    {
        var m = new FakeModule();
        await using var host = new ModuleHost(new Ctx());
        host.Register(new ModuleDescriptor("a", "A", () => m));
        await host.EnableAsync("a");
        await host.EnableAsync("a");
        Assert.Equal(1, m.Starts);
    }

    [Fact]
    public async Task Duplicate_registration_throws()
    {
        await using var host = new ModuleHost(new Ctx());
        host.Register(new ModuleDescriptor("a", "A", () => new FakeModule()));
        Assert.Throws<ArgumentException>(() => host.Register(new ModuleDescriptor("a", "A", () => new FakeModule())));
    }

    [Fact]
    public async Task Dispose_stops_running_modules()
    {
        var m = new FakeModule();
        var host = new ModuleHost(new Ctx());
        host.Register(new ModuleDescriptor("a", "A", () => m));
        await host.EnableAsync("a");
        await host.DisposeAsync();
        Assert.Equal(1, m.Stops);
    }
}
