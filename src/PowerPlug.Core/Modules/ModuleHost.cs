using PowerPlug.Core.Logging;

namespace PowerPlug.Core.Modules;

/// <summary>
/// The caretaker of all modules. It switches modules on and off and protects the app
/// from misbehaving modules:
///  - Modules are created lazily: a switched-off module costs nothing.
///  - Faults are isolated: if one module crashes or hangs, the others keep working.
///  - Every start/stop has a time limit, so a stuck module cannot freeze the app.
/// </summary>
public sealed class ModuleHost : IAsyncDisposable
{
    // Internal bookkeeping for one registered module.
    private sealed class Entry(ModuleDescriptor descriptor)
    {
        public ModuleDescriptor Descriptor { get; } = descriptor;
        public IModule? Instance;                       // null while the module is not running
        public ModuleState State = ModuleState.Stopped;
        public string? Error;                           // last error message, if Faulted
    }

    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    // Only one start/stop operation at a time, so two clicks cannot race each other.
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IModuleContext _context;
    private readonly TimeSpan _timeout;

    public ModuleHost(IModuleContext context, TimeSpan? operationTimeout = null)
    {
        _context = context;
        _timeout = operationTimeout ?? TimeSpan.FromSeconds(10);
    }

    /// <summary>Makes a module known to the host. Does not start it.</summary>
    public void Register(ModuleDescriptor descriptor)
    {
        if (!_entries.TryAdd(descriptor.Id, new Entry(descriptor)))
            throw new ArgumentException($"Module '{descriptor.Id}' is already registered.", nameof(descriptor));
    }

    /// <summary>Returns the current state of every module (for the tray menu).</summary>
    public IReadOnlyList<ModuleStatus> GetStatus() =>
        _entries.Values.Select(e => new ModuleStatus(e.Descriptor.Id, e.Descriptor.DisplayName, e.State, e.Error)).ToList();

    /// <summary>
    /// Switches a module on. Returns true on success. If the module fails, the error is logged,
    /// the module is marked Faulted and false is returned - the app itself keeps running.
    /// </summary>
    public async Task<bool> EnableAsync(string id, CancellationToken cancellationToken = default)
    {
        var entry = Get(id);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (entry.State == ModuleState.Running) return true; // already on: nothing to do

            IModule? module = null;
            try
            {
                module = entry.Descriptor.Factory(); // create the module only now

                // Give the module a limited time to start, so a stuck start cannot block us forever.
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_timeout);
                await module.StartAsync(_context, cts.Token).ConfigureAwait(false);

                entry.Instance = module;
                entry.State = ModuleState.Running;
                entry.Error = null;
                _context.Log.Info($"Module '{id}' started.");
                return true;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // The module failed: remember why, clean up, and carry on without it.
                _context.Log.Error($"Module '{id}' failed to start.", ex);
                entry.State = ModuleState.Faulted;
                entry.Error = ex.Message;
                entry.Instance = null;
                if (module is not null) await SafeDisposeAsync(id, module).ConfigureAwait(false);
                return false;
            }
        }
        finally { _gate.Release(); }
    }

    /// <summary>Switches a module off and releases everything it used.</summary>
    public async Task DisableAsync(string id, CancellationToken cancellationToken = default)
    {
        var entry = Get(id);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await StopEntryAsync(entry).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    /// <summary>
    /// Called once at startup: switches on every module the user enabled
    /// (or that is enabled by default when the user has not chosen yet).
    /// </summary>
    public async Task StartEnabledAsync(IReadOnlyDictionary<string, bool> userChoice, CancellationToken cancellationToken = default)
    {
        foreach (var entry in _entries.Values.ToList())
        {
            var enabled = userChoice.TryGetValue(entry.Descriptor.Id, out var v) ? v : entry.Descriptor.EnabledByDefault;
            if (enabled) await EnableAsync(entry.Descriptor.Id, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Stops all modules. Called when the app exits.</summary>
    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var entry in _entries.Values) await StopEntryAsync(entry).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    // Stops one module. Errors are logged but never thrown, so shutdown always completes.
    private async Task StopEntryAsync(Entry entry)
    {
        var module = entry.Instance;
        entry.Instance = null;
        if (module is not null)
        {
            try
            {
                using var cts = new CancellationTokenSource(_timeout);
                await module.StopAsync(cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _context.Log.Error($"Module '{entry.Descriptor.Id}' failed to stop cleanly.", ex);
            }
            await SafeDisposeAsync(entry.Descriptor.Id, module).ConfigureAwait(false);
            _context.Log.Info($"Module '{entry.Descriptor.Id}' stopped.");
        }
        entry.State = ModuleState.Stopped;
        entry.Error = null;
    }

    // Disposing a module must never crash the host, so errors are only logged.
    private async Task SafeDisposeAsync(string id, IModule module)
    {
        try { await module.DisposeAsync().ConfigureAwait(false); }
        catch (Exception ex) { _context.Log.Error($"Module '{id}' failed to dispose.", ex); }
    }

    private Entry Get(string id) =>
        _entries.TryGetValue(id, out var e) ? e : throw new KeyNotFoundException($"Unknown module '{id}'.");
}
