using System.Diagnostics;

namespace PowerPlug.Core.Apps;

public interface IProcessLauncher
{
    /// <summary>Starts the entry without waiting for it to exit. Throws on failure.</summary>
    void Launch(AppEntry entry);
}

public sealed class ProcessLauncher : IProcessLauncher
{
    public void Launch(AppEntry entry)
    {
        using var process = Process.Start(BuildStartInfo(entry));
    }

    /// <summary>Pure function (testable): decides how an entry is started.</summary>
    public static ProcessStartInfo BuildStartInfo(AppEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Path))
            throw new ArgumentException("Path is empty.", nameof(entry));

        ProcessStartInfo psi;
        if (string.Equals(System.IO.Path.GetExtension(entry.Path), ".ps1", StringComparison.OrdinalIgnoreCase))
        {
            // .ps1 would open in an editor via the shell verb; run it explicitly instead.
            psi = new ProcessStartInfo("powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -File \"{entry.Path}\" {entry.Arguments}".TrimEnd())
            { UseShellExecute = false };
        }
        else
        {
            // Shell execute handles .exe, .bat, .cmd, documents, URLs and App Paths entries.
            psi = new ProcessStartInfo(entry.Path, entry.Arguments ?? "") { UseShellExecute = true };
        }

        if (!string.IsNullOrWhiteSpace(entry.WorkingDirectory)) psi.WorkingDirectory = entry.WorkingDirectory;
        return psi;
    }
}
