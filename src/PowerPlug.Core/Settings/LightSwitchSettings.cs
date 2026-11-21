namespace PowerPlug.Core.Settings;

/// <summary>Options for the Light Switch module (automatic light/dark theme by time of day).</summary>
public sealed class LightSwitchSettings
{
    /// <summary>Time in "HH:mm" format. From this time on, the light theme is used.</summary>
    public string LightFrom { get; set; } = "07:00";

    /// <summary>Time in "HH:mm" format. From this time on, the dark theme is used.</summary>
    public string DarkFrom { get; set; } = "19:00";

    /// <summary>Switch the theme of apps (Explorer, Settings, ...).</summary>
    public bool ApplyToApps { get; set; } = true;

    /// <summary>Switch the theme of the system (taskbar, Start menu).</summary>
    public bool ApplyToSystem { get; set; } = true;
}
