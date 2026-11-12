namespace PowerPlug.Core.Apps;

/// <summary>An entry of the Apps Center: an application, batch file or script.</summary>
public sealed class AppEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    /// <summary>Executable, .bat/.cmd/.ps1 script, document or URL.</summary>
    public string Path { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string? WorkingDirectory { get; set; }
    /// <summary>Started by "Fast Boot".</summary>
    public bool InFastBoot { get; set; }
    /// <summary>Wait before launching this entry during Fast Boot.</summary>
    public int DelayMs { get; set; }
}
