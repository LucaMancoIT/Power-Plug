using PowerPlug.Core.Logging;

namespace PowerPlug.Core.Modules;

/// <summary>
/// Owns module lifecycles. Modules are instantiated lazily on enable (disabled modules cost nothing)
/// and are fault-isolated: an exception or timeout in one module never affects the others.
/// </summary>
public sealed class ModuleHost : IAsyncDisposable
{
    private sealed class Entry(ModuleDescriptor descriptor)
    {
        public ModuleDescriptor Descriptor { get; } = descriptor;
        public IModule? Instance;
        public ModuleState State = ModuleState.Stopped;
        public string? Error;
    }

    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IModuleContext _context;
    private readonly TimeSpan _timeout;

    public ModuleHost(IModuleContext context, TimeSpan? operationTimeout = null)
    {
        _context = context;
        _timeout = operationTimeout ?? TimeSpan.FromSeconds(10);
    }

    public void Register(ModuleDescriptor descriptor)
    {
        if (!_entries.TryAdd(descriptor.Id, new Entry(descriptor)))
            throw new ArgumentException($"Module '{descriptor.Id}' is already registered.", nameof(descriptor));
    }

    public IReadOnlyList<ModuleStatus> GetStatus() =>
        _entries.Values.Select(e => new ModuleStatus(e.Descriptor.Id, e.Descriptor.DisplayName, e.State, e.Error)).ToList();

    public async Task<bool> EnableAsync(string id, CancellationToken cancellationToken = default)
    {
        var entry = Get(id);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (entry.State == ModuleState.Running) return true;

            IModule? module = null;
            try
            {
                module = entry.Descriptor.Factory();
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

    public async Task DisableAsync(string id, CancellationToken cancellationToken = default)
    {
        var entry = Get(id);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await StopEntryAsync(entry).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    public async Task StartEnabledAsync(IReadOnlyDictionary<string, bool> userChoice, CancellationToken cancellationToken = default)
    {
        foreach (var entry in _entries.Values.ToList())
        {
            var enabled = userChoice.TryGetValue(entry.Descriptor.Id, out var v) ? v : entry.Descriptor.EnabledByDefault;
            if (enabled) await EnableAsync(entry.Descriptor.Id, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var entry in _entries.Values) await StopEntryAsync(entry).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

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

    private async Task SafeDisposeAsync(string id, IModule module)
    {
        try { await module.DisposeAsync().ConfigureAwait(false); }
        catch (Exception ex) { _context.Log.Error($"Module '{id}' failed to dispose.", ex); }
    }

    private Entry Get(string id) =>
        _entries.TryGetValue(id, out var e) ? e : throw new KeyNotFoundException($"Unknown module '{id}'.");
}
