using PowerPlug.Core.Modules;

namespace PowerPlug.Modules.Awake;

[Flags]
public enum ExecutionState : uint
{
    Continuous = 0x80000000,
    SystemRequired = 0x00000001,
    DisplayRequired = 0x00000002,
}

/// <summary>Abstraction over SetThreadExecutionState so the logic is testable off-Windows.</summary>
public interface IExecutionStateApi
{
    void Set(ExecutionState state);
}

public sealed class WindowsExecutionStateApi : IExecutionStateApi
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint flags);

    public void Set(ExecutionState state) => SetThreadExecutionState((uint)state);
}

/// <summary>
/// Keeps the PC awake without touching power plans. The execution state is per-thread, so a
/// dedicated thread holds it and blocks (zero CPU) until the module stops.
/// </summary>
public sealed class AwakeModule(IExecutionStateApi api, bool keepDisplayOn = false) : IModule
{
    public const string Id = "awake";

    private Thread? _thread;
    private readonly ManualResetEventSlim _stop = new(false);

    public Task StartAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        if (_thread is not null) return Task.CompletedTask;
        var state = ExecutionState.Continuous | ExecutionState.SystemRequired
                    | (keepDisplayOn ? ExecutionState.DisplayRequired : 0);
        var ready = new ManualResetEventSlim(false);
        _thread = new Thread(() =>
        {
            api.Set(state);
            ready.Set();
            _stop.Wait();
            api.Set(ExecutionState.Continuous); // clears the request
        })
        { IsBackground = true, Name = "PowerPlug.Awake" };
        _thread.Start();
        ready.Wait(cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _stop.Set();
        _thread?.Join(TimeSpan.FromSeconds(2));
        _thread = null;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _stop.Set();
        return ValueTask.CompletedTask;
    }
}
