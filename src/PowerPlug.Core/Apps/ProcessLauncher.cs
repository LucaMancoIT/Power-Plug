using System.Diagnostics;

namespace PowerPlug.Core.Apps;

public interface IProcessLauncher
{
    /// <summary>Starts the target without waiting for it to exit. Throws on failure.</summary>
    void Launch(LaunchTarget target);
}

public sealed class ProcessLauncher : IProcessLauncher
{
    public void Launch(LaunchTarget target)
    {
        using var process = Process.Start(BuildStartInfo(target));
    }

    /// <summary>Pure function (testable): decides how a target is started.</summary>
    public static ProcessStartInfo BuildStartInfo(LaunchTarget target)
    {
        if (string.IsNullOrWhiteSpace(target.Path))
            throw new ArgumentException("Path is empty.", nameof(target));

        ProcessStartInfo psi;
        if (string.Equals(System.IO.Path.GetExtension(target.Path), ".ps1", StringComparison.OrdinalIgnoreCase))
        {
            // .ps1 would open in an editor via the shell verb; run it explicitly instead.
            psi = new ProcessStartInfo("powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -File \"{target.Path}\" {target.Arguments}".TrimEnd())
            { UseShellExecute = false };
        }
        else
        {
            // Shell execute handles .exe, .bat, .cmd, .lnk, .rdp, URLs, protocol handlers and
            // "shell:AppsFolder\<AppUserModelID>" (Store apps) exactly like double-clicking them.
            psi = new ProcessStartInfo(target.Path, target.Arguments) { UseShellExecute = true };
        }

        if (!string.IsNullOrWhiteSpace(target.WorkingDirectory)) psi.WorkingDirectory = target.WorkingDirectory;
        return psi;
    }
}
