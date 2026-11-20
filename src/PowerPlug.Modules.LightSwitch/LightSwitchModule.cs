using PowerPlug.Core.Modules;
using PowerPlug.Core.Settings;

namespace PowerPlug.Modules.LightSwitch;

/// <summary>Windows access, implemented in the App project (registry needs a Windows target).</summary>
public interface IThemeApi
{
    Theme GetCurrent();
    void Set(Theme theme, bool apps, bool system);
    /// <summary>Raises the callback when the clock changes or the PC resumes from sleep.</summary>
    IDisposable WatchClockChanges(Action onChange);
}

/// <summary>
/// Switches between light and dark theme by time of day. Event-driven: one timer set to the next
/// switch time plus a clock/resume watcher - no polling.
/// </summary>
public sealed class LightSwitchModule(IThemeApi api, Func<LightSwitchSettings> settings, Func<DateTime>? clock = null) : IModule
{
    public const string Id = "light-switch";
    private static readonly TimeSpan DefaultLight = TimeSpan.FromHours(7), DefaultDark = TimeSpan.FromHours(19);

    private readonly Func<DateTime> _clock = clock ?? (() => DateTime.Now);
    private readonly object _lock = new();
    private IModuleContext? _context;
    private Timer? _timer;
    private IDisposable? _watcher;
    private bool _stopped;

    public Task StartAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        _context = context;
        lock (_lock)
        {
            _stopped = false;
            _timer = new Timer(_ => Evaluate(), null, Timeout.Infinite, Timeout.Infinite);
            _watcher = api.WatchClockChanges(Evaluate);
        }
        Evaluate();
        return Task.CompletedTask;
    }

    /// <summary>Applies the theme for the current time and schedules the next switch. Thread-safe, never throws.</summary>
    public void Evaluate()
    {
        try
        {
            lock (_lock)
            {
                if (_stopped || _timer is null) return;
                var s = settings();
                var light = ThemeSchedule.ParseOrDefault(s.LightFrom, DefaultLight);
                var dark = ThemeSchedule.ParseOrDefault(s.DarkFrom, DefaultDark);
                var now = _clock();

                var target = ThemeSchedule.Resolve(now.TimeOfDay, light, dark);
                var current = api.GetCurrent();
                if (current != target)
                {
                    api.Set(target, s.ApplyToApps, s.ApplyToSystem);
                    _context?.Log.Info($"Light Switch: {current} -> {target} (apps={s.ApplyToApps}, system={s.ApplyToSystem}).");
                }
                else
                {
                    _context?.Log.Info($"Light Switch: {target} already active at {now:HH:mm}, no change.");
                }

                var due = ThemeSchedule.NextChange(now, light, dark) - now + TimeSpan.FromSeconds(1);
                _timer.Change(due, Timeout.InfiniteTimeSpan);
            }
        }
        catch (Exception ex)
        {
            _context?.Log.Error("Light Switch evaluation failed.", ex); // a timer callback must never crash the host
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _stopped = true;
            _watcher?.Dispose(); _watcher = null;
            _timer?.Dispose(); _timer = null;
        }
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => new(StopAsync(CancellationToken.None));
}
