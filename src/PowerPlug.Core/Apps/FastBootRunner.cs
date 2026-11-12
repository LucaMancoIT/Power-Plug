using PowerPlug.Core.Logging;

namespace PowerPlug.Core.Apps;

public sealed record FastBootFailure(string Id, string Error);
public sealed record FastBootResult(IReadOnlyList<string> Started, IReadOnlyList<FastBootFailure> Failed);

/// <summary>
/// Starts all Fast Boot entries in list order. One failing entry never stops the others.
/// (Workspace layouts are applied by the Workspaces module in a later phase.)
/// </summary>
public sealed class FastBootRunner(
    IProcessLauncher launcher,
    ILog log,
    Func<TimeSpan, CancellationToken, Task>? delay = null)
{
    private readonly Func<TimeSpan, CancellationToken, Task> _delay = delay ?? Task.Delay;

    public async Task<FastBootResult> RunAsync(IEnumerable<AppEntry> apps, CancellationToken cancellationToken = default)
    {
        var started = new List<string>();
        var failed = new List<FastBootFailure>();
        foreach (var app in apps.Where(a => a.InFastBoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (app.DelayMs > 0) await _delay(TimeSpan.FromMilliseconds(app.DelayMs), cancellationToken).ConfigureAwait(false);
                launcher.Launch(app);
                started.Add(app.Id);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                log.Error($"Fast Boot: '{app.Name}' failed to start.", ex);
                failed.Add(new FastBootFailure(app.Id, ex.Message));
            }
        }
        return new FastBootResult(started, failed);
    }
}
