using System.Text.Json;
using System.Windows.Forms;
using PowerPlug.Core.Apps;
using PowerPlug.Core.Hotkeys;
using PowerPlug.Core.Logging;
using PowerPlug.Core.Modules;
using PowerPlug.Core.Settings;
using PowerPlug.Modules.AlwaysOnTop;
using PowerPlug.Modules.Awake;
using PowerPlug.Modules.LightSwitch;

namespace PowerPlug.App;

/// <summary>Entry point of Power Plug: starts the tray icon and keeps the app running in the background.</summary>
internal static class Program
{
    [STAThread] // required by Windows Forms: the user interface must run on a single-threaded apartment
    private static void Main()
    {
        // Only one copy of Power Plug may run. The mutex is a system-wide "name tag":
        // if another instance already holds it, we simply exit.
        using var mutex = new Mutex(true, @"Local\PowerPlug.SingleInstance", out var isFirst);
        if (!isFirst) return;

        ApplicationConfiguration.Initialize();

        // All settings and logs live in %AppData%\PowerPlug.
        var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PowerPlug");
        var log = new RollingFileLog(Path.Combine(dataDir, "powerplug.log"));

        // Last line of defense: write unexpected crashes to the log so they can be investigated.
        AppDomain.CurrentDomain.UnhandledException += (_, e) => log.Error("Unhandled exception", e.ExceptionObject as Exception);

        Application.Run(new TrayContext(dataDir, log)); // runs until the user chooses "Exit"
    }
}

/// <summary>
/// The heart of the running app: owns the tray icon and its menu and connects everything
/// (modules, settings, Apps Center, Fast Boot). There is deliberately no main window;
/// that keeps memory use and CPU use very low while idle.
/// </summary>
internal sealed class TrayContext : ApplicationContext
{
    // The small set of tools that modules receive (see IModuleContext).
    private sealed class Ctx(ILog log, IHotkeyService hotkeys) : IModuleContext
    {
        public ILog Log => log;
        public IHotkeyService Hotkeys => hotkeys;
    }

    private readonly NotifyIcon _tray;                         // the icon next to the clock
    private readonly WinHotkeyService _hotkeys = new();        // global keyboard shortcuts
    private readonly ModuleHost _host;                         // switches modules on and off
    private readonly JsonSettingsStore<AppSettings> _store;    // reads/writes settings.json
    private readonly AppSettings _settings;
    private readonly IProcessLauncher _launcher = new ProcessLauncher();
    private readonly string _dataDir;
    private readonly ILog _log;
    private readonly ToolStripMenuItem _fastBootStatus = new("Fast Boot: not run yet") { Enabled = false };
    private bool _fastBootRunning; // prevents starting a second Fast Boot while one is still running

    public TrayContext(string dataDir, ILog log)
    {
        _dataDir = dataDir;
        _log = log;
        _store = new JsonSettingsStore<AppSettings>(Path.Combine(dataDir, "settings.json"), log);
        _settings = _store.Load();

        // Register every module. They are only created when the user switches them on.
        _host = new ModuleHost(new Ctx(log, _hotkeys));
        _host.Register(new ModuleDescriptor(AwakeModule.Id, "Awake (keep PC awake)",
            () => new AwakeModule(new WindowsExecutionStateApi())));
        _host.Register(new ModuleDescriptor(AlwaysOnTopModule.Id, "Always On Top (Win+Ctrl+T)",
            () => new AlwaysOnTopModule(new WindowsWindowApi())));
        _host.Register(new ModuleDescriptor(LightSwitchModule.Id, "Light Switch (light/dark theme by time of day)",
            () => new LightSwitchModule(new WindowsThemeApi(), () => _store.Load().LightSwitch)));

        _tray = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application, // TODO: replace with the branded icon
            Text = "Power Plug",
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };

        // Switch on the modules the user enabled earlier (fire and forget; errors are logged by the host).
        _ = _host.StartEnabledAsync(_settings.Modules);
    }

    // Builds the right-click menu of the tray icon.
    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        // The settings window will be a separate on-demand program later (zero cost while idle).
        // Until then, settings are edited in settings.json.

        // Fast Boot: one click starts the whole Fast Boot list.
        var fastBoot = new ToolStripMenuItem("Start Fast Boot");
        fastBoot.Click += async (_, _) => await RunFastBootAsync();
        menu.Items.Add(fastBoot);
        menu.Items.Add(_fastBootStatus); // shows the result of the last run

        // Apps Center: a sub-menu that is refilled every time the menu opens.
        var apps = new ToolStripMenuItem("Apps Center");
        menu.Items.Add(apps);
        menu.Opening += (_, _) => RefreshApps(apps);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Export Start menu apps for Fast Boot…", null, (_, _) => ExportStartMenuApps());
        menu.Items.Add("Open configuration folder", null, (_, _) => OpenConfigFolder());

        // Autostart: a check mark that adds/removes Power Plug from the Windows startup list.
        var autostart = new ToolStripMenuItem("Start with Windows") { CheckOnClick = true, Checked = Autostart.IsEnabled() };
        autostart.Click += (_, _) =>
        {
            try { Autostart.Set(autostart.Checked); }
            catch (Exception ex)
            {
                _log.Error("Autostart change failed.", ex);
                autostart.Checked = Autostart.IsEnabled(); // show the real state again
            }
        };
        menu.Items.Add(autostart);

        menu.Items.Add(new ToolStripSeparator());

        // One check-mark item per module: click = switch on/off, and remember the choice.
        foreach (var s in _host.GetStatus())
        {
            var id = s.Id;
            var item = new ToolStripMenuItem(s.DisplayName) { CheckOnClick = true, Checked = _settings.Modules.TryGetValue(id, out var on) && on };
            item.Click += async (_, _) =>
            {
                if (item.Checked) item.Checked = await _host.EnableAsync(id); // unchecks itself if starting failed
                else await _host.DisableAsync(id);
                _settings.Modules[id] = item.Checked;
                _store.Save(_settings);
            };
            menu.Items.Add(item);
        }

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, async (_, _) => await ExitAsync());
        return menu;
    }

    // Fills the Apps Center sub-menu from settings.json.
    private void RefreshApps(ToolStripMenuItem root)
    {
        root.DropDownItems.Clear();
        var apps = _store.Load().AppsCenter; // re-read so edits to settings.json apply without a restart
        if (apps.Count == 0)
        {
            root.DropDownItems.Add(new ToolStripMenuItem("(empty - add entries under \"AppsCenter\" in settings.json)") { Enabled = false });
            return;
        }
        foreach (var app in apps)
        {
            var entry = app; // copy, so each menu item keeps its own entry
            root.DropDownItems.Add(entry.Name.Length > 0 ? entry.Name : entry.Path, null, (_, _) => LaunchOne(entry));
        }
    }

    // Starts a single Apps Center entry and reports problems to the user.
    private void LaunchOne(AppEntry entry)
    {
        try { _launcher.Launch(entry); }
        catch (Exception ex)
        {
            _log.Error($"Apps Center: '{entry.Name}' failed to start.", ex);
            _tray.ShowBalloonTip(4000, "Power Plug", $"'{entry.Name}' could not be started: {ex.Message}", ToolTipIcon.Warning);
        }
    }

    // Runs the Fast Boot list and shows the result in the menu and as a notification.
    private async Task RunFastBootAsync()
    {
        if (_fastBootRunning) return; // ignore a second click while the first run is still busy
        _fastBootRunning = true;
        try
        {
            var entries = _store.Load().FastBoot;
            _fastBootStatus.Text = $"Fast Boot: running ({entries.Count(e => e.Enabled)} entries)…";
            var result = await new FastBootRunner(_launcher, _log).RunAsync(entries);

            var text = $"Fast Boot ({DateTime.Now:HH:mm:ss}): {result.Started.Count} started, {result.Failed.Count} failed";
            if (entries.Count == 0) text = "Fast Boot: list is empty (see \"FastBoot\" in settings.json)";
            _fastBootStatus.Text = text; // visible in the menu even if Windows hides notifications
            _tray.ShowBalloonTip(3000, "Power Plug", text, result.Failed.Count == 0 ? ToolTipIcon.Info : ToolTipIcon.Warning);
        }
        catch (Exception ex)
        {
            _log.Error("Fast Boot crashed.", ex);
            _fastBootStatus.Text = "Fast Boot: error (see log)";
        }
        finally { _fastBootRunning = false; }
    }

    /// <summary>
    /// Writes all Start menu apps (including published remote apps) into a file as ready-to-copy
    /// Fast Boot entries and shows the file in Explorer. All entries are disabled by default.
    /// </summary>
    private void ExportStartMenuApps()
    {
        try
        {
            var entries = StartMenuScanner.Scan(StartMenuScanner.DefaultRoots())
                .Select(a => new FastBootEntry { Name = a.Name, Path = a.Path, Enabled = false })
                .ToList();
            Directory.CreateDirectory(_dataDir);
            var file = Path.Combine(_dataDir, "startmenu-apps.json");
            File.WriteAllText(file, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
            // Open Explorer with the new file selected.
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{file}\""))?.Dispose();
        }
        catch (Exception ex)
        {
            _log.Error("Start menu export failed.", ex);
            _tray.ShowBalloonTip(4000, "Power Plug", $"Export failed: {ex.Message}", ToolTipIcon.Warning);
        }
    }

    // Opens %AppData%\PowerPlug in Explorer (settings.json and the log live there).
    private void OpenConfigFolder()
    {
        Directory.CreateDirectory(_dataDir);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_dataDir) { UseShellExecute = true })?.Dispose();
    }

    // Clean shutdown: hide the icon, stop all modules, release shortcuts, then end the app.
    private async Task ExitAsync()
    {
        _tray.Visible = false;
        await _host.DisposeAsync();
        _hotkeys.Dispose();
        _tray.Dispose();
        ExitThread();
    }
}
