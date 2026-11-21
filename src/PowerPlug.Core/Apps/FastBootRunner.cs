using System.Net.Sockets;
using PowerPlug.Core.Logging;

namespace PowerPlug.Core.Apps;

/// <summary>One Fast Boot entry that could not be started, with the reason.</summary>
public sealed record FastBootFailure(string Id, string Error);

/// <summary>The outcome of a Fast Boot run: which entries started and which failed.</summary>
public sealed record FastBootResult(IReadOnlyList<string> Started, IReadOnlyList<FastBootFailure> Failed);

/// <summary>
/// Checks whether a server ("host:port") is reachable. A delegate (a stored piece of code) so that
/// tests can replace the real network check with a fake one.
/// </summary>
public delegate Task<bool> HostProbe(string hostPort, CancellationToken cancellationToken);

/// <summary>
/// Starts all enabled Fast Boot entries one after another, in list order.
/// For each entry it can: wait a fixed time, wait until a server is reachable, and retry starting.
/// One failing entry never stops the others.
/// (Restoring saved window layouts will be added later by the Workspaces feature.)
/// </summary>
// Note: no ConfigureAwait(false) on purpose. Starting a program via the Windows shell must keep
// running on the caller's (user-interface) thread, otherwise it can fail.
public sealed class FastBootRunner(
    IProcessLauncher launcher,
    ILog log,
    Func<TimeSpan, CancellationToken, Task>? delay = null,
    HostProbe? probe = null)
{
    // While waiting for a server we check again every 2 seconds.
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    // Real waiting and real network checks by default; tests pass fast fakes instead.
    private readonly Func<TimeSpan, CancellationToken, Task> _delay = delay ?? Task.Delay;
    private readonly HostProbe _probe = probe ?? TcpProbe;

    /// <summary>Runs the whole list and reports what started and what failed.</summary>
    public async Task<FastBootResult> RunAsync(IEnumerable<FastBootEntry> entries, CancellationToken cancellationToken = default)
    {
        var started = new List<string>();
        var failed = new List<FastBootFailure>();
        foreach (var entry in entries.Where(e => e.Enabled))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // Step 1: fixed waiting time (optional).
                if (entry.DelayMs > 0)
                    await _delay(TimeSpan.FromMilliseconds(entry.DelayMs), cancellationToken);

                // Step 2: wait until the server is reachable (optional). If it never is, skip this entry.
                if (!string.IsNullOrWhiteSpace(entry.WaitForHost)
                    && !await WaitForHostAsync(entry, cancellationToken))
                {
                    var msg = $"Host '{entry.WaitForHost}' not reachable within {entry.WaitTimeoutSec}s.";
                    log.Warn($"Fast Boot: '{entry.Name}' skipped. {msg}");
                    failed.Add(new FastBootFailure(entry.Id, msg));
                    continue;
                }

                // Step 3: start it (with retries).
                await LaunchWithRetriesAsync(entry, cancellationToken);
                started.Add(entry.Id);
            }
            catch (OperationCanceledException) { throw; } // the user cancelled: stop everything
            catch (Exception ex)
            {
                // This entry failed, but the rest of the list still runs.
                log.Error($"Fast Boot: '{entry.Name}' failed to start.", ex);
                failed.Add(new FastBootFailure(entry.Id, ex.Message));
            }
        }
        log.Info($"Fast Boot finished: {started.Count} started, {failed.Count} failed.");
        return new FastBootResult(started, failed);
    }

    // Tries to start the entry; on failure waits and tries again until the attempts are used up.
    private async Task LaunchWithRetriesAsync(FastBootEntry entry, CancellationToken ct)
    {
        var attempts = 1 + Math.Max(0, entry.Retries); // first try + configured retries
        for (var attempt = 1; ; attempt++)
        {
            try { launcher.Launch(entry); return; }
            catch (Exception ex) when (attempt < attempts && ex is not OperationCanceledException)
            {
                log.Warn($"Fast Boot: '{entry.Name}' attempt {attempt}/{attempts} failed: {ex.Message}");
                await _delay(TimeSpan.FromMilliseconds(Math.Max(0, entry.RetryDelayMs)), ct);
            }
            // On the last attempt the filter above is false, so the exception propagates to RunAsync.
        }
    }

    // Checks the server every 2 seconds until it answers or the timeout is over.
    // Time is counted by adding up our own waits (not a clock), which keeps tests fast and exact.
    private async Task<bool> WaitForHostAsync(FastBootEntry entry, CancellationToken ct)
    {
        var timeout = TimeSpan.FromSeconds(Math.Max(0, entry.WaitTimeoutSec));
        var waited = TimeSpan.Zero;
        while (true)
        {
            if (await _probe(entry.WaitForHost!, ct)) return true;
            if (waited >= timeout) return false;
            await _delay(PollInterval, ct);
            waited += PollInterval;
        }
    }

    /// <summary>Splits "host" or "host:port" into its parts. The port defaults to 3389 (remote desktop).</summary>
    public static (string Host, int Port) ParseHostPort(string hostPort)
    {
        var text = hostPort.Trim();
        var i = text.LastIndexOf(':');
        if (i < 0) return (text, 3389);
        if (i == 0 || !int.TryParse(text[(i + 1)..], out var port) || port is < 1 or > 65535)
            throw new FormatException($"Invalid host:port '{hostPort}'.");
        return (text[..i], port);
    }

    // The real server check: try to open a network connection, giving up after 2 seconds.
    private static async Task<bool> TcpProbe(string hostPort, CancellationToken ct)
    {
        var (host, port) = ParseHostPort(hostPort);
        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            await client.ConnectAsync(host, port, cts.Token);
            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return false; } // our 2 s limit ran out
        catch (SocketException) { return false; } // server refused or not found
    }
}
