using PowerPlug.Core.Modules;
using PowerPlug.Core.Settings;

namespace PowerPlug.Modules.LightSwitch;

/// <summary>
/// What the module needs from Windows. The real version lives in the App project because
/// it reads and writes the Windows registry, which needs a Windows-only project.
/// </summary>
public interface IThemeApi
{
    /// <summary>The theme Windows currently uses for apps.</summary>
    Theme GetCurrent();

    /// <summary>Switches the theme for apps and/or the system (taskbar, Start menu).</summary>
    void Set(Theme theme, bool apps, bool system);

    /// <summary>Calls <paramref name="onChange"/> when the clock is changed or the PC wakes from sleep.</summary>
    IDisposable WatchClockChanges(Action onChange);
}

/// <summary>
/// Switches between the light and dark Windows theme by time of day.
/// It is event-driven: one timer is set to the next switch time, plus a watcher for clock
/// changes and wake-up from sleep. Nothing runs in the background in between (no polling).
/// </summary>
public sealed class LightSwitchModule(IThemeApi api, Func<LightSwitchSettings> settings, Func<DateTime>? clock = null) : IModule
{
    /// <summary>Technical name, used as the key in settings.json.</summary>
    public const string Id = "light-switch";

    // Used if the times in the settings file are missing or invalid.
    private static readonly TimeSpan DefaultLight = TimeSpan.FromHours(7), DefaultDark = TimeSpan.FromHours(19);

    private readonly Func<DateTime> _clock = clock ?? (() => DateTime.Now); // replaceable in tests
    private readonly object _lock = new();
    private IModuleContext? _context;
    private Timer? _timer;          // fires at the next switch time
    private IDisposable? _watcher;  // reacts to clock changes and wake-up from sleep
    private bool _stopped;

    public Task StartAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        _context = context;
        lock (_lock)
        {
            _stopped = false;
            // The timer starts switched off; Evaluate() arms it with the right time.
            _timer = new Timer(_ => Evaluate(), null, Timeout.Infinite, Timeout.Infinite);
            _watcher = api.WatchClockChanges(Evaluate);
        }
        Evaluate(); // apply the correct theme right away
        return Task.CompletedTask;
    }

    /// <summary>
    /// Applies the theme that fits the current time and sets the timer for the next switch.
    /// Safe to call from any thread. Never throws.
    /// </summary>
    public void Evaluate()
    {
        try
        {
            lock (_lock)
            {
                if (_stopped || _timer is null) return; // module is off: ignore late events

                // The settings are re-read every time, so edits to settings.json are picked up.
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
                    // Already correct. This is logged so "nothing happened" is not a mystery.
                    _context?.Log.Info($"Light Switch: {target} already active at {now:HH:mm}, no change.");
                }

                // Wake up again one second after the next switch time (a small safety margin).
                var due = ThemeSchedule.NextChange(now, light, dark) - now + TimeSpan.FromSeconds(1);
                _timer.Change(due, Timeout.InfiniteTimeSpan);
            }
        }
        catch (Exception ex)
        {
            // A timer callback must never crash the whole app, so errors are only logged.
            _context?.Log.Error("Light Switch evaluation failed.", ex);
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
