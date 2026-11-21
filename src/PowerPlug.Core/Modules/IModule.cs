using PowerPlug.Core.Hotkeys;
using PowerPlug.Core.Logging;

namespace PowerPlug.Core.Modules;

// ---------------------------------------------------------------------------
// Big picture
// Power Plug is a "house" made of small, independent rooms called MODULES
// (Awake, Always On Top, Light Switch, ...). Each module implements IModule.
// The ModuleHost (see ModuleHost.cs) is the caretaker that switches modules on
// and off. A module only talks to the outside world through IModuleContext.
// ---------------------------------------------------------------------------

/// <summary>
/// The tools a module is allowed to use. It is deliberately small so that modules
/// stay independent of each other and are easy to test with fake versions.
/// </summary>
public interface IModuleContext
{
    /// <summary>Where a module writes messages about what it is doing (or what went wrong).</summary>
    ILog Log { get; }

    /// <summary>Lets a module react to a global keyboard shortcut (for example Win+Ctrl+T).</summary>
    IHotkeyService Hotkeys { get; }
}

/// <summary>
/// A self-contained feature of Power Plug.
/// Rules every module must follow:
///  1. While it is stopped it must use no resources (no threads, timers or hooks).
///  2. <see cref="StopAsync"/> must release everything the module started.
///  3. <see cref="StopAsync"/> should not throw under normal conditions.
/// </summary>
public interface IModule : IAsyncDisposable
{
    /// <summary>Switch the feature on. Throw an exception if it cannot start; the host handles it.</summary>
    Task StartAsync(IModuleContext context, CancellationToken cancellationToken);

    /// <summary>Switch the feature off and clean up.</summary>
    Task StopAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The "business card" of a module: who it is and how to create it.
/// The host only creates the real module when the user turns it on.
/// </summary>
/// <param name="Id">Short technical name, also used as the key in settings.json (e.g. "awake").</param>
/// <param name="DisplayName">Name shown to the user in the tray menu.</param>
/// <param name="Factory">Creates a fresh module instance on demand.</param>
/// <param name="EnabledByDefault">Whether the module starts automatically before the user chooses.</param>
public sealed record ModuleDescriptor(
    string Id,
    string DisplayName,
    Func<IModule> Factory,
    bool EnabledByDefault = false);

/// <summary>The three states a module can be in.</summary>
public enum ModuleState
{
    /// <summary>Switched off. Uses no resources.</summary>
    Stopped,
    /// <summary>Switched on and working.</summary>
    Running,
    /// <summary>Tried to start but failed (see <see cref="ModuleStatus.Error"/>).</summary>
    Faulted
}

/// <summary>A read-only snapshot of one module, used to build the tray menu.</summary>
public sealed record ModuleStatus(string Id, string DisplayName, ModuleState State, string? Error);
