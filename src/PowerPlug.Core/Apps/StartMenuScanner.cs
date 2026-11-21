namespace PowerPlug.Core.Apps;

/// <summary>A shortcut found in the Start menu.</summary>
public sealed record StartMenuApp(string Name, string Path);

/// <summary>
/// Finds the shortcuts in the Windows Start menu folders.
/// Remote and virtualized apps (RemoteApp, Parallels, Citrix, ...) usually place their shortcuts
/// there too, so they can be added to Fast Boot just like local programs.
/// Microsoft Store apps are not shortcuts; for those use "shell:AppsFolder\&lt;AppUserModelID&gt;".
/// </summary>
public static class StartMenuScanner
{
    // Only these file types are treated as launchable shortcuts.
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".lnk", ".url", ".appref-ms", ".rdp" };

    /// <summary>The two Start menu folders: the one for all users and the one for the current user.</summary>
    public static IReadOnlyList<string> DefaultRoots() => new[]
    {
        Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
        Environment.GetFolderPath(Environment.SpecialFolder.Programs),
    };

    /// <summary>Searches the given folders (including sub-folders) and returns the shortcuts sorted by name.</summary>
    public static IReadOnlyList<StartMenuApp> Scan(IEnumerable<string> roots)
    {
        // IgnoreInaccessible: skip folders we are not allowed to read instead of failing.
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        var byName = new Dictionary<string, StartMenuApp>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots.Where(r => !string.IsNullOrEmpty(r) && Directory.Exists(r)))
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", options))
            {
                if (!Extensions.Contains(Path.GetExtension(file))) continue;
                var name = Path.GetFileNameWithoutExtension(file);
                if (name.Contains("uninstall", StringComparison.OrdinalIgnoreCase)) continue; // not useful in a start list
                byName.TryAdd(name, new StartMenuApp(name, file)); // same name twice: keep the first
            }
        }
        return byName.Values.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
