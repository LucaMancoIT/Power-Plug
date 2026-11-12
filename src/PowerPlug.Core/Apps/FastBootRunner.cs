using System.Net.Sockets;
using PowerPlug.Core.Logging;

namespace PowerPlug.Core.Apps;

public sealed record FastBootFailure(string Id, string Error);
public sealed record FastBootResult(IReadOnlyList<string> Started, IReadOnlyList<FastBootFailure> Failed);

public delegate Task<bool> HostProbe(string hostPort, CancellationToken cancellationToken);

/// <summary>
/// Starts all enabled Fast Boot entries in list order. One failing entry never stops the others.
/// Per entry: optional delay, optional wait until a host is reachable, start with retries.
/// (Workspace layouts are applied by the Workspaces module in a later phase.)
/// </summary>
// Note: no ConfigureAwait(false) on purpose - ShellExecute must keep running on the caller's (UI/STA) thread.
public sealed class FastBootRunner(
    IProcessLauncher launcher,
    ILog log,
    Func<TimeSpan, CancellationToken, Task>? delay = null,
    HostProbe? probe = null)
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private readonly Func<TimeSpan, CancellationToken, Task> _delay = delay ?? Task.Delay;
    private readonly HostProbe _probe = probe ?? TcpProbe;

    public async Task<FastBootResult> RunAsync(IEnumerable<FastBootEntry> entries, CancellationToken cancellationToken = default)
    {
        var started = new List<string>();
        var failed = new List<FastBootFailure>();
        foreach (var entry in entries.Where(e => e.Enabled))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (entry.DelayMs > 0)
                    await _delay(TimeSpan.FromMilliseconds(entry.DelayMs), cancellationToken);

                if (!string.IsNullOrWhiteSpace(entry.WaitForHost)
                    && !await WaitForHostAsync(entry, cancellationToken))
                {
                    var msg = $"Host '{entry.WaitForHost}' not reachable within {entry.WaitTimeoutSec}s.";
                    log.Warn($"Fast Boot: '{entry.Name}' skipped. {msg}");
                    failed.Add(new FastBootFailure(entry.Id, msg));
                    continue;
                }

                await LaunchWithRetriesAsync(entry, cancellationToken);
                started.Add(entry.Id);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                log.Error($"Fast Boot: '{entry.Name}' failed to start.", ex);
                failed.Add(new FastBootFailure(entry.Id, ex.Message));
            }
        }
        log.Info($"Fast Boot finished: {started.Count} started, {failed.Count} failed.");
        return new FastBootResult(started, failed);
    }

    private async Task LaunchWithRetriesAsync(FastBootEntry entry, CancellationToken ct)
    {
        var attempts = 1 + Math.Max(0, entry.Retries);
        for (var attempt = 1; ; attempt++)
        {
            try { launcher.Launch(entry); return; }
            catch (Exception ex) when (attempt < attempts && ex is not OperationCanceledException)
            {
                log.Warn($"Fast Boot: '{entry.Name}' attempt {attempt}/{attempts} failed: {ex.Message}");
                await _delay(TimeSpan.FromMilliseconds(Math.Max(0, entry.RetryDelayMs)), ct);
            }
        }
    }

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

    /// <summary>Parses "host" or "host:port"; the port defaults to 3389 (RDP).</summary>
    public static (string Host, int Port) ParseHostPort(string hostPort)
    {
        var text = hostPort.Trim();
        var i = text.LastIndexOf(':');
        if (i < 0) return (text, 3389);
        if (i == 0 || !int.TryParse(text[(i + 1)..], out var port) || port is < 1 or > 65535)
            throw new FormatException($"Invalid host:port '{hostPort}'.");
        return (text[..i], port);
    }

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
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return false; }
        catch (SocketException) { return false; }
    }
}
