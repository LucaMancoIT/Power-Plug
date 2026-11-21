using PowerPlug.Core.Apps;

namespace PowerPlug.Core.Settings;

/// <summary>
/// Everything the user can configure, stored in %AppData%\PowerPlug\settings.json.
/// Each property below is one section of that file.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Which modules are switched on. Key = module id. Missing entries use the module's default.</summary>
    public Dictionary<string, bool> Modules { get; set; } = new();

    /// <summary>Apps Center: your own apps, batch files and scripts, shown in the tray menu.</summary>
    public List<AppEntry> AppsCenter { get; set; } = new();

    /// <summary>Times and options for the Light Switch module.</summary>
    public LightSwitchSettings LightSwitch { get; set; } = new();

    /// <summary>Fast Boot: a separate list of programs that are all started together with one click.</summary>
    public List<FastBootEntry> FastBoot { get; set; } = new();
}
