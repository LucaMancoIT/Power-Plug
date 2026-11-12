namespace PowerPlug.Core.Apps;

public sealed record StartMenuApp(string Name, string Path);

/// <summary>
/// Lists Start-menu shortcuts. Published remote/virtualized apps (RDS RemoteApp, Parallels, Citrix, ...)
/// register their shortcuts here too, so they can be added to Fast Boot like any local program.
/// Store (UWP) apps are not shortcuts; use "shell:AppsFolder\&lt;AppUserModelID&gt;" for those.
/// </summary>
public static class StartMenuScanner
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".lnk", ".url", ".appref-ms", ".rdp" };

    public static IReadOnlyList<string> DefaultRoots() => new[]
    {
        Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
        Environment.GetFolderPath(Environment.SpecialFolder.Programs),
    };

    public static IReadOnlyList<StartMenuApp> Scan(IEnumerable<string> roots)
    {
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        var byName = new Dictionary<string, StartMenuApp>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots.Where(r => !string.IsNullOrEmpty(r) && Directory.Exists(r)))
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", options))
            {
                if (!Extensions.Contains(Path.GetExtension(file))) continue;
                var name = Path.GetFileNameWithoutExtension(file);
                if (name.Contains("uninstall", StringComparison.OrdinalIgnoreCase)) continue;
                byName.TryAdd(name, new StartMenuApp(name, file));
            }
        }
        return byName.Values.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
