using PowerPlug.Core.Settings;
using Xunit;

namespace PowerPlug.Tests;

// Tests for saving and loading settings: defaults when nothing exists, a lossless round trip,
// and graceful recovery from a damaged file. Uses a temporary folder that is removed afterwards.
public class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pp-tests-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    [Fact]
    public void Missing_file_returns_defaults()
    {
        var store = new JsonSettingsStore<AppSettings>(Path.Combine(_dir, "s.json"));
        Assert.Empty(store.Load().Modules);
    }

    [Fact]
    public void Roundtrip_preserves_values()
    {
        var store = new JsonSettingsStore<AppSettings>(Path.Combine(_dir, "s.json"));
        store.Save(new AppSettings { Modules = { ["awake"] = true } });
        Assert.True(store.Load().Modules["awake"]);
    }

    [Fact]
    public void Corrupt_file_falls_back_to_defaults_and_is_kept_aside()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "s.json");
        File.WriteAllText(path, "{ not json");
        var store = new JsonSettingsStore<AppSettings>(path);
        Assert.Empty(store.Load().Modules);
        Assert.True(File.Exists(path + ".corrupt"));
    }
}
