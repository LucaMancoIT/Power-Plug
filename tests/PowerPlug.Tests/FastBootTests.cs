using PowerPlug.Core.Apps;
using PowerPlug.Core.Logging;
using PowerPlug.Core.Settings;
using Xunit;

namespace PowerPlug.Tests;

public class FastBootTests
{
    private sealed class Launcher(params string[] failIds) : IProcessLauncher
    {
        public readonly List<string> Launched = new();
        public void Launch(AppEntry e)
        {
            if (failIds.Contains(e.Id)) throw new FileNotFoundException("missing");
            Launched.Add(e.Id);
        }
    }

    private static AppEntry E(string id, bool boot = true, int delay = 0) =>
        new() { Id = id, Name = id, Path = id + ".exe", InFastBoot = boot, DelayMs = delay };

    [Fact]
    public async Task Starts_only_fast_boot_entries_in_order()
    {
        var l = new Launcher();
        var r = await new FastBootRunner(l, NullLog.Instance).RunAsync(new[] { E("a"), E("x", boot: false), E("b") });
        Assert.Equal(new[] { "a", "b" }, l.Launched);
        Assert.Empty(r.Failed);
    }

    [Fact]
    public async Task Failure_does_not_stop_remaining_entries()
    {
        var l = new Launcher("a");
        var r = await new FastBootRunner(l, NullLog.Instance).RunAsync(new[] { E("a"), E("b") });
        Assert.Equal(new[] { "b" }, l.Launched);
        Assert.Equal("a", r.Failed.Single().Id);
    }

    [Fact]
    public async Task Delay_is_applied_per_entry()
    {
        var waits = new List<TimeSpan>();
        var runner = new FastBootRunner(new Launcher(), NullLog.Instance, (t, _) => { waits.Add(t); return Task.CompletedTask; });
        await runner.RunAsync(new[] { E("a", delay: 500), E("b") });
        Assert.Equal(new[] { TimeSpan.FromMilliseconds(500) }, waits);
    }

    [Fact]
    public void Ps1_scripts_run_via_powershell()
    {
        var psi = ProcessLauncher.BuildStartInfo(new AppEntry { Path = @"C:\s\run.ps1", Arguments = "-x" });
        Assert.Equal("powershell.exe", psi.FileName);
        Assert.Contains(@"-File ""C:\s\run.ps1"" -x", psi.Arguments);
    }

    [Fact]
    public void Batch_files_use_shell_execute()
    {
        var psi = ProcessLauncher.BuildStartInfo(new AppEntry { Path = @"C:\s\run.bat", WorkingDirectory = @"C:\s" });
        Assert.True(psi.UseShellExecute);
        Assert.Equal(@"C:\s", psi.WorkingDirectory);
    }

    [Fact]
    public void Empty_path_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => ProcessLauncher.BuildStartInfo(new AppEntry()));
    }

    [Fact]
    public void Apps_survive_settings_roundtrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pp-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonSettingsStore<AppSettings>(Path.Combine(dir, "s.json"));
            store.Save(new AppSettings { Apps = { E("a", delay: 250) } });
            var loaded = store.Load().Apps.Single();
            Assert.Equal((true, 250), (loaded.InFastBoot, loaded.DelayMs));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
