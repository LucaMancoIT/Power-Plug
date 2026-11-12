using PowerPlug.Core.Hotkeys;
using PowerPlug.Core.Logging;
using PowerPlug.Core.Modules;
using PowerPlug.Modules.Awake;
using Xunit;

namespace PowerPlug.Tests;

public class AwakeModuleTests
{
    private sealed class Api : IExecutionStateApi
    {
        public readonly List<ExecutionState> Calls = new();
        public void Set(ExecutionState s) { lock (Calls) Calls.Add(s); }
    }

    private sealed class Ctx : IModuleContext
    {
        public ILog Log => NullLog.Instance;
        public IHotkeyService Hotkeys => throw new NotSupportedException();
    }

    [Fact]
    public async Task Sets_state_on_start_and_clears_on_stop()
    {
        var api = new Api();
        var m = new AwakeModule(api);
        await m.StartAsync(new Ctx(), CancellationToken.None);
        await m.StopAsync(CancellationToken.None);
        Assert.Equal(new[] { ExecutionState.Continuous | ExecutionState.SystemRequired, ExecutionState.Continuous }, api.Calls);
    }

    [Fact]
    public async Task Display_flag_is_set_when_requested()
    {
        var api = new Api();
        var m = new AwakeModule(api, keepDisplayOn: true);
        await m.StartAsync(new Ctx(), CancellationToken.None);
        await m.StopAsync(CancellationToken.None);
        Assert.True(api.Calls[0].HasFlag(ExecutionState.DisplayRequired));
    }
}
