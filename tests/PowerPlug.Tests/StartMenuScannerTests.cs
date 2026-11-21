using PowerPlug.Core.Apps;
using Xunit;

namespace PowerPlug.Tests;

// Tests for the Start menu scanner using a temporary folder with fake shortcut files.
public class StartMenuScannerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pp-sm-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [Fact]
    public void Finds_shortcuts_recursively_and_ignores_other_files()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Parallels Apps"));
        File.WriteAllText(Path.Combine(_root, "Editor.lnk"), "");
        File.WriteAllText(Path.Combine(_root, "Parallels Apps", "Excel (Remote).lnk"), "");
        File.WriteAllText(Path.Combine(_root, "Work.rdp"), "");
        File.WriteAllText(Path.Combine(_root, "readme.txt"), "");
        File.WriteAllText(Path.Combine(_root, "Uninstall Foo.lnk"), "");
        var names = StartMenuScanner.Scan(new[] { _root }).Select(a => a.Name).ToList();
        Assert.Equal(new[] { "Editor", "Excel (Remote)", "Work" }, names);
    }

    [Fact]
    public void Missing_roots_are_ignored() =>
        Assert.Empty(StartMenuScanner.Scan(new[] { Path.Combine(_root, "nope") }));

    [Fact]
    public void Duplicate_names_across_roots_are_listed_once()
    {
        var other = _root + "-b";
        Directory.CreateDirectory(_root); Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(_root, "App.lnk"), "");
        File.WriteAllText(Path.Combine(other, "App.lnk"), "");
        try { Assert.Single(StartMenuScanner.Scan(new[] { _root, other })); }
        finally { Directory.Delete(other, true); }
    }
}
