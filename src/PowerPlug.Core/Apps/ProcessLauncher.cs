using System.Diagnostics;

namespace PowerPlug.Core.Apps;

/// <summary>Starts programs. A fake version is used in tests so nothing real gets launched.</summary>
public interface IProcessLauncher
{
    /// <summary>Starts the target without waiting for it to finish. Throws an exception if it cannot be started.</summary>
    void Launch(LaunchTarget target);
}

/// <summary>The real launcher: asks Windows to start the target.</summary>
public sealed class ProcessLauncher : IProcessLauncher
{
    public void Launch(LaunchTarget target)
    {
        // We only start the program; we do not wait for it. "using" just releases our handle.
        using var process = Process.Start(BuildStartInfo(target));
    }

    /// <summary>
    /// Decides HOW a target is started. This is a pure calculation (nothing is launched here),
    /// which makes it easy to test.
    /// </summary>
    public static ProcessStartInfo BuildStartInfo(LaunchTarget target)
    {
        if (string.IsNullOrWhiteSpace(target.Path))
            throw new ArgumentException("Path is empty.", nameof(target));

        ProcessStartInfo psi;
        if (string.Equals(System.IO.Path.GetExtension(target.Path), ".ps1", StringComparison.OrdinalIgnoreCase))
        {
            // Double-clicking a .ps1 file would only open it in an editor, so we run it through
            // PowerShell explicitly. "-ExecutionPolicy Bypass" lets it run without changing system settings.
            psi = new ProcessStartInfo("powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -File \"{target.Path}\" {target.Arguments}".TrimEnd())
            { UseShellExecute = false };
        }
        else
        {
            // "Shell execute" means: start it exactly like a double-click in Explorer would.
            // This handles programs, .bat/.cmd files, shortcuts, .rdp files, web addresses and
            // Store apps ("shell:AppsFolder\<AppUserModelID>").
            psi = new ProcessStartInfo(target.Path, target.Arguments) { UseShellExecute = true };
        }

        if (!string.IsNullOrWhiteSpace(target.WorkingDirectory)) psi.WorkingDirectory = target.WorkingDirectory;
        return psi;
    }
}
