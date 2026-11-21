namespace PowerPlug.Core.Apps;

/// <summary>
/// Anything Power Plug can start: a program, a script, a shortcut, a website and so on.
/// <see cref="Path"/> may be an executable (.exe), a script (.bat, .cmd, .ps1), a shortcut (.lnk),
/// a remote-desktop file (.rdp), a web address, or a Microsoft Store app written as
/// "shell:AppsFolder\&lt;AppUserModelID&gt;".
/// </summary>
public abstract class LaunchTarget
{
    /// <summary>Unique technical id (generated automatically). Used to report results per entry.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Name shown to the user in the tray menu and in the log.</summary>
    public string Name { get; set; } = "";

    /// <summary>What to start (see the list above).</summary>
    public string Path { get; set; } = "";

    /// <summary>Optional extra text passed to the program (command-line arguments).</summary>
    public string Arguments { get; set; } = "";

    /// <summary>Optional folder the program should start in.</summary>
    public string? WorkingDirectory { get; set; }
}

/// <summary>
/// An entry of the Apps Center: apps, batch files and scripts that you integrate yourself.
/// It has no extra options; it is started when you click it in the tray menu.
/// </summary>
public sealed class AppEntry : LaunchTarget
{
}

/// <summary>
/// An entry of the Fast Boot list: a program on this PC (including virtualized/remote apps)
/// that is started together with the other entries when you click "Start Fast Boot".
/// </summary>
public sealed class FastBootEntry : LaunchTarget
{
    /// <summary>Entries with Enabled = false are ignored. Handy to keep an entry without using it.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Milliseconds to wait before starting this entry (e.g. until VPN, network or a virtual machine is ready).</summary>
    public int DelayMs { get; set; }

    /// <summary>How many additional times to try again if starting fails.</summary>
    public int Retries { get; set; }

    /// <summary>Milliseconds to wait between two attempts.</summary>
    public int RetryDelayMs { get; set; } = 3000;

    /// <summary>
    /// Optional "host:port" (the port defaults to 3389, which is remote desktop).
    /// The entry only starts once that server accepts connections, for example the gateway
    /// behind a published remote app.
    /// </summary>
    public string? WaitForHost { get; set; }

    /// <summary>How many seconds to wait for <see cref="WaitForHost"/> before giving up on this entry.</summary>
    public int WaitTimeoutSec { get; set; } = 60;
}
