namespace PowerPlug.Core.Apps;

/// <summary>
/// Something that can be started. <see cref="Path"/> may be an executable, .bat/.cmd/.ps1 script,
/// shortcut (.lnk), RDP file (.rdp), URL/protocol handler, or a "shell:AppsFolder\..." Store-app id.
/// </summary>
public abstract class LaunchTarget
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string? WorkingDirectory { get; set; }
}

/// <summary>Apps Center entry: apps, batch files and scripts you integrate yourself.</summary>
public sealed class AppEntry : LaunchTarget
{
}

/// <summary>
/// Fast Boot entry: an independent list of programs on this PC (including virtualized/remote apps)
/// that are started together on demand.
/// </summary>
public sealed class FastBootEntry : LaunchTarget
{
    public bool Enabled { get; set; } = true;
    /// <summary>Wait before starting this entry (e.g. for VPN, network or the VM to come up).</summary>
    public int DelayMs { get; set; }
    /// <summary>Additional start attempts if starting throws.</summary>
    public int Retries { get; set; }
    public int RetryDelayMs { get; set; } = 3000;
    /// <summary>
    /// Optional "host:port" (port defaults to 3389). The entry starts only once the host accepts
    /// connections, e.g. the RDP/Parallels RAS gateway behind a published remote app.
    /// </summary>
    public string? WaitForHost { get; set; }
    public int WaitTimeoutSec { get; set; } = 60;
}
