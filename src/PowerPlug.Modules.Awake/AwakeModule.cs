using PowerPlug.Core.Modules;

namespace PowerPlug.Modules.Awake;

/// <summary>
/// Flags Windows understands for "please do not go to sleep".
/// They can be combined (for example Continuous + SystemRequired).
/// </summary>
[Flags]
public enum ExecutionState : uint
{
    /// <summary>Keep the request active until we clear it (instead of only once).</summary>
    Continuous = 0x80000000,
    /// <summary>Do not let the PC go to sleep.</summary>
    SystemRequired = 0x00000001,
    /// <summary>Do not turn the screen off.</summary>
    DisplayRequired = 0x00000002,
}

/// <summary>
/// The one Windows call this module needs, wrapped in an interface so the logic
/// can be tested on any computer with a fake version.
/// </summary>
public interface IExecutionStateApi
{
    void Set(ExecutionState state);
}

/// <summary>The real version: calls the Windows function SetThreadExecutionState.</summary>
public sealed class WindowsExecutionStateApi : IExecutionStateApi
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint flags);

    public void Set(ExecutionState state) => SetThreadExecutionState((uint)state);
}

/// <summary>
/// Keeps the PC awake without touching the power plan settings.
/// Windows remembers the "stay awake" request per thread, so a dedicated thread holds it
/// and simply waits (using no CPU) until the module is stopped.
/// </summary>
public sealed class AwakeModule(IExecutionStateApi api, bool keepDisplayOn = false) : IModule
{
    /// <summary>Technical name, used as the key in settings.json.</summary>
    public const string Id = "awake";

    private Thread? _thread;
    private readonly ManualResetEventSlim _stop = new(false); // "wake up and finish" signal for the thread

    public Task StartAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        if (_thread is not null) return Task.CompletedTask; // already running

        var state = ExecutionState.Continuous | ExecutionState.SystemRequired
                    | (keepDisplayOn ? ExecutionState.DisplayRequired : 0);
        var ready = new ManualResetEventSlim(false); // set once the request is active

        _thread = new Thread(() =>
        {
            api.Set(state);          // tell Windows: stay awake
            ready.Set();
            _stop.Wait();            // sleep here (no CPU) until the module is stopped
            api.Set(ExecutionState.Continuous); // clear the request: Windows may sleep again
        })
        { IsBackground = true, Name = "PowerPlug.Awake" };
        _thread.Start();
        ready.Wait(cancellationToken); // return only after the request is really active
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _stop.Set();                                   // let the thread finish and clear the request
        _thread?.Join(TimeSpan.FromSeconds(2));        // wait briefly for it to end
        _thread = null;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _stop.Set();
        return ValueTask.CompletedTask;
    }
}
