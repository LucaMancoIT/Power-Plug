using PowerPlug.Core.Hotkeys;
using PowerPlug.Core.Logging;

namespace PowerPlug.Core.Modules;

/// <summary>Services a module may use. Deliberately small to keep modules decoupled.</summary>
public interface IModuleContext
{
    ILog Log { get; }
    IHotkeyService Hotkeys { get; }
}

/// <summary>
/// A self-contained feature. Must be idle (no threads, timers, hooks) while stopped, and release
/// everything in <see cref="StopAsync"/>. Must not throw from StopAsync under normal conditions.
/// </summary>
public interface IModule : IAsyncDisposable
{
    Task StartAsync(IModuleContext context, CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}

public sealed record ModuleDescriptor(
    string Id,
    string DisplayName,
    Func<IModule> Factory,
    bool EnabledByDefault = false);

public enum ModuleState { Stopped, Running, Faulted }

public sealed record ModuleStatus(string Id, string DisplayName, ModuleState State, string? Error);
