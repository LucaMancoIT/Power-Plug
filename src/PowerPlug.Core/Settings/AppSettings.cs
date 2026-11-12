using PowerPlug.Core.Apps;

namespace PowerPlug.Core.Settings;

public sealed class AppSettings
{
    /// <summary>Module id -> enabled. Missing entries fall back to the module's default.</summary>
    public Dictionary<string, bool> Modules { get; set; } = new();

    /// <summary>Apps Center entries (apps, batch files, scripts). Entries flagged InFastBoot start together.</summary>
    public List<AppEntry> Apps { get; set; } = new();
}
