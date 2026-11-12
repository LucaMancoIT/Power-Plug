namespace PowerPlug.Core.Settings;

public sealed class LightSwitchSettings
{
    /// <summary>"HH:mm" - from this time the light theme is active.</summary>
    public string LightFrom { get; set; } = "07:00";
    /// <summary>"HH:mm" - from this time the dark theme is active.</summary>
    public string DarkFrom { get; set; } = "19:00";
    public bool ApplyToApps { get; set; } = true;
    public bool ApplyToSystem { get; set; } = true;
}
