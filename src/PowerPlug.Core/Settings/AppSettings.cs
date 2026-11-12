using PowerPlug.Core.Apps;

namespace PowerPlug.Core.Settings;

public sealed class AppSettings
{
    /// <summary>Module id -> enabled. Missing entries fall back to the module's default.</summary>
    public Dictionary<string, bool> Modules { get; set; } = new();

    /// <summary>Apps Center: your own apps, batch files and scripts.</summary>
    public List<AppEntry> AppsCenter { get; set; } = new();

    /// <summary>Fast Boot: independent list of programs started together on demand.</summary>
    public List<FastBootEntry> FastBoot { get; set; } = new();
}
