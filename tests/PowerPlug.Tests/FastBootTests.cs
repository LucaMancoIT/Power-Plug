using PowerPlug.Core.Apps;
using PowerPlug.Core.Logging;
using PowerPlug.Core.Settings;
using Xunit;

namespace PowerPlug.Tests;

// Tests for Fast Boot (ordering, delays, retries, waiting for a server) and for the
// decision how each kind of file is started. Fakes replace real programs, time and network,
// so these tests are fast and safe.
public class FastBootTests
{
    // Fake launcher: records what would have been started and can be told to fail.
    private sealed class Launcher(Func<LaunchTarget, int, bool>? failWhen = null) : IProcessLauncher
    {
        public readonly List<string> Launched = new();
        private readonly Dictionary<string, int> _calls = new();
        public void Launch(LaunchTarget t)
        {
            _calls[t.Id] = _calls.GetValueOrDefault(t.Id) + 1;
            if (failWhen?.Invoke(t, _calls[t.Id]) == true) throw new FileNotFoundException("missing");
            Launched.Add(t.Id);
        }
    }

    private static FastBootEntry E(string id, bool enabled = true, int delay = 0) =>
        new() { Id = id, Name = id, Path = id + ".exe", Enabled = enabled, DelayMs = delay };

    private static FastBootRunner Runner(IProcessLauncher l, List<TimeSpan>? waits = null, HostProbe? probe = null) =>
        new(l, NullLog.Instance, (t, _) => { waits?.Add(t); return Task.CompletedTask; }, probe);

    [Fact]
    public async Task Starts_only_enabled_entries_in_order()
    {
        var l = new Launcher();
        var r = await Runner(l).RunAsync(new[] { E("a"), E("x", enabled: false), E("b") });
        Assert.Equal(new[] { "a", "b" }, l.Launched);
        Assert.Empty(r.Failed);
    }

    [Fact]
    public async Task Failure_does_not_stop_remaining_entries()
    {
        var l = new Launcher((t, _) => t.Id == "a");
        var r = await Runner(l).RunAsync(new[] { E("a"), E("b") });
        Assert.Equal(new[] { "b" }, l.Launched);
        Assert.Equal("a", r.Failed.Single().Id);
    }

    [Fact]
    public async Task Delay_is_applied_per_entry()
    {
        var waits = new List<TimeSpan>();
        await Runner(new Launcher(), waits).RunAsync(new[] { E("a", delay: 500), E("b") });
        Assert.Equal(new[] { TimeSpan.FromMilliseconds(500) }, waits);
    }

    [Fact]
    public async Task Retries_until_start_succeeds()
    {
        var l = new Launcher((_, call) => call < 3); // fails twice, succeeds on 3rd attempt
        var entry = E("vm"); entry.Retries = 2; entry.RetryDelayMs = 1000;
        var waits = new List<TimeSpan>();
        var r = await Runner(l, waits).RunAsync(new[] { entry });
        Assert.Equal(new[] { "vm" }, r.Started);
        Assert.Equal(2, waits.Count);
    }

    [Fact]
    public async Task Fails_after_all_retries_are_used()
    {
        var entry = E("vm"); entry.Retries = 1;
        var r = await Runner(new Launcher((_, _) => true)).RunAsync(new[] { entry });
        Assert.Single(r.Failed);
    }

    [Fact]
    public async Task Waits_for_host_then_starts()
    {
        var probes = 0;
        var entry = E("remote"); entry.WaitForHost = "gateway:3389"; entry.WaitTimeoutSec = 60;
        var l = new Launcher();
        var r = await Runner(l, probe: (_, _) => Task.FromResult(++probes >= 3)).RunAsync(new[] { entry });
        Assert.Equal(3, probes);
        Assert.Equal(new[] { "remote" }, l.Launched);
        Assert.Empty(r.Failed);
    }

    [Fact]
    public async Task Unreachable_host_skips_entry_after_timeout_without_blocking_others()
    {
        var entry = E("remote"); entry.WaitForHost = "gateway"; entry.WaitTimeoutSec = 4;
        var l = new Launcher();
        var r = await Runner(l, probe: (_, _) => Task.FromResult(false)).RunAsync(new[] { entry, E("local") });
        Assert.Equal(new[] { "local" }, l.Launched);
        Assert.Equal("remote", r.Failed.Single().Id);
    }

    [Theory]
    [InlineData("srv", "srv", 3389)]
    [InlineData("srv:3390", "srv", 3390)]
    public void Host_and_port_are_parsed(string input, string host, int port) =>
        Assert.Equal((host, port), FastBootRunner.ParseHostPort(input));

    [Fact]
    public void Invalid_port_is_rejected() =>
        Assert.Throws<FormatException>(() => FastBootRunner.ParseHostPort("srv:abc"));

    [Fact]
    public void Ps1_scripts_run_via_powershell()
    {
        var psi = ProcessLauncher.BuildStartInfo(new AppEntry { Path = @"C:\s\run.ps1", Arguments = "-x" });
        Assert.Equal("powershell.exe", psi.FileName);
        Assert.Contains(@"-File ""C:\s\run.ps1"" -x", psi.Arguments);
    }

    [Theory]
    [InlineData(@"C:\s\run.bat")]
    [InlineData(@"C:\Users\me\app.rdp")]
    [InlineData(@"C:\Start Menu\RemoteApp.lnk")]
    [InlineData(@"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App")]
    [InlineData("https://example.com")]
    public void Everything_else_uses_shell_execute(string path)
    {
        var psi = ProcessLauncher.BuildStartInfo(new FastBootEntry { Path = path, WorkingDirectory = @"C:\s" });
        Assert.True(psi.UseShellExecute);
        Assert.Equal(path, psi.FileName);
        Assert.Equal(@"C:\s", psi.WorkingDirectory);
    }

    [Fact]
    public void Empty_path_is_rejected() =>
        Assert.Throws<ArgumentException>(() => ProcessLauncher.BuildStartInfo(new AppEntry()));

    [Fact]
    public void Both_lists_survive_settings_roundtrip_independently()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pp-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonSettingsStore<AppSettings>(Path.Combine(dir, "s.json"));
            var fb = E("a", delay: 250); fb.WaitForHost = "gw:3390"; fb.Retries = 2;
            store.Save(new AppSettings { FastBoot = { fb }, AppsCenter = { new AppEntry { Name = "Skript", Path = "x.bat" } } });
            var loaded = store.Load();
            Assert.Equal((250, 2, "gw:3390"), (loaded.FastBoot[0].DelayMs, loaded.FastBoot[0].Retries, loaded.FastBoot[0].WaitForHost));
            Assert.Equal("Skript", loaded.AppsCenter.Single().Name);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
