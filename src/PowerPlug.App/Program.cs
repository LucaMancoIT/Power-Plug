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

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, @"Local\PowerPlug.SingleInstance", out var isFirst);
        if (!isFirst) return;

        ApplicationConfiguration.Initialize();
        var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PowerPlug");
        var log = new RollingFileLog(Path.Combine(dataDir, "powerplug.log"));
        AppDomain.CurrentDomain.UnhandledException += (_, e) => log.Error("Unhandled exception", e.ExceptionObject as Exception);

        Application.Run(new TrayContext(dataDir, log));
    }
}

internal sealed class TrayContext : ApplicationContext
{
    private sealed class Ctx(ILog log, IHotkeyService hotkeys) : IModuleContext
    {
        public ILog Log => log;
        public IHotkeyService Hotkeys => hotkeys;
    }

    private readonly NotifyIcon _tray;
    private readonly WinHotkeyService _hotkeys = new();
    private readonly ModuleHost _host;
    private readonly JsonSettingsStore<AppSettings> _store;
    private readonly AppSettings _settings;
    private readonly IProcessLauncher _launcher = new ProcessLauncher();
    private readonly string _dataDir;
    private readonly ILog _log;
    private readonly ToolStripMenuItem _fastBootStatus = new("Fast Boot: noch nicht ausgeführt") { Enabled = false };
    private bool _fastBootRunning;

    public TrayContext(string dataDir, ILog log)
    {
        _dataDir = dataDir;
        _log = log;
        _store = new JsonSettingsStore<AppSettings>(Path.Combine(dataDir, "settings.json"), log);
        _settings = _store.Load();
        _host = new ModuleHost(new Ctx(log, _hotkeys));
        _host.Register(new ModuleDescriptor(AwakeModule.Id, "Awake (PC wach halten)",
            () => new AwakeModule(new WindowsExecutionStateApi())));
        _host.Register(new ModuleDescriptor(AlwaysOnTopModule.Id, "Always On Top (Win+Strg+T)",
            () => new AlwaysOnTopModule(new WindowsWindowApi())));
        _host.Register(new ModuleDescriptor(LightSwitchModule.Id, "Light Switch (Hell/Dunkel nach Uhrzeit)",
            () => new LightSwitchModule(new WindowsThemeApi(), () => _store.Load().LightSwitch)));

        _tray = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application, // TODO: replace with branded icon
            Text = "Power Plug",
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };
        _ = _host.StartEnabledAsync(_settings.Modules);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        // The settings UI will be a separate on-demand process (zero idle cost); until then: settings.json.
        var fastBoot = new ToolStripMenuItem("Fast Boot starten");
        fastBoot.Click += async (_, _) => await RunFastBootAsync();
        menu.Items.Add(fastBoot);
        menu.Items.Add(_fastBootStatus);
        var apps = new ToolStripMenuItem("Apps Center");
        menu.Items.Add(apps);
        menu.Opening += (_, _) => RefreshApps(apps);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Startmenü-Apps für Fast Boot exportieren…", null, (_, _) => ExportStartMenuApps());
        menu.Items.Add("Konfigurationsordner öffnen", null, (_, _) => OpenConfigFolder());
        var autostart = new ToolStripMenuItem("Mit Windows starten") { CheckOnClick = true, Checked = Autostart.IsEnabled() };
        autostart.Click += (_, _) =>
        {
            try { Autostart.Set(autostart.Checked); }
            catch (Exception ex)
            {
                _log.Error("Autostart change failed.", ex);
                autostart.Checked = Autostart.IsEnabled();
            }
        };
        menu.Items.Add(autostart);
        menu.Items.Add(new ToolStripSeparator());
        foreach (var s in _host.GetStatus())
        {
            var id = s.Id;
            var item = new ToolStripMenuItem(s.DisplayName) { CheckOnClick = true, Checked = _settings.Modules.TryGetValue(id, out var on) && on };
            item.Click += async (_, _) =>
            {
                if (item.Checked) item.Checked = await _host.EnableAsync(id);
                else await _host.DisableAsync(id);
                _settings.Modules[id] = item.Checked;
                _store.Save(_settings);
            };
            menu.Items.Add(item);
        }
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Beenden", null, async (_, _) => await ExitAsync());
        return menu;
    }

    private void RefreshApps(ToolStripMenuItem root)
    {
        root.DropDownItems.Clear();
        var apps = _store.Load().AppsCenter; // re-read so edits to settings.json apply without restart
        if (apps.Count == 0)
        {
            root.DropDownItems.Add(new ToolStripMenuItem("(leer – Einträge in settings.json unter \"AppsCenter\")") { Enabled = false });
            return;
        }
        foreach (var app in apps)
        {
            var entry = app;
            root.DropDownItems.Add(entry.Name.Length > 0 ? entry.Name : entry.Path, null, (_, _) => LaunchOne(entry));
        }
    }

    private void LaunchOne(AppEntry entry)
    {
        try { _launcher.Launch(entry); }
        catch (Exception ex)
        {
            _log.Error($"Apps Center: '{entry.Name}' failed to start.", ex);
            _tray.ShowBalloonTip(4000, "Power Plug", $"'{entry.Name}' konnte nicht gestartet werden: {ex.Message}", ToolTipIcon.Warning);
        }
    }

    private async Task RunFastBootAsync()
    {
        if (_fastBootRunning) return;
        _fastBootRunning = true;
        try
        {
            var entries = _store.Load().FastBoot;
            _fastBootStatus.Text = $"Fast Boot: läuft ({entries.Count(e => e.Enabled)} Einträge)…";
            var result = await new FastBootRunner(_launcher, _log).RunAsync(entries);
            var text = $"Fast Boot ({DateTime.Now:HH:mm:ss}): {result.Started.Count} gestartet, {result.Failed.Count} fehlgeschlagen";
            if (entries.Count == 0) text = "Fast Boot: Liste leer (settings.json → \"FastBoot\")";
            _fastBootStatus.Text = text;
            _tray.ShowBalloonTip(3000, "Power Plug", text, result.Failed.Count == 0 ? ToolTipIcon.Info : ToolTipIcon.Warning);
        }
        catch (Exception ex)
        {
            _log.Error("Fast Boot crashed.", ex);
            _fastBootStatus.Text = "Fast Boot: Fehler (siehe Log)";
        }
        finally { _fastBootRunning = false; }
    }

    /// <summary>Writes all Start-menu apps (incl. published remote apps) as ready-to-copy Fast Boot entries.</summary>
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
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{file}\""))?.Dispose();
        }
        catch (Exception ex)
        {
            _log.Error("Start menu export failed.", ex);
            _tray.ShowBalloonTip(4000, "Power Plug", $"Export fehlgeschlagen: {ex.Message}", ToolTipIcon.Warning);
        }
    }

    private void OpenConfigFolder()
    {
        Directory.CreateDirectory(_dataDir);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_dataDir) { UseShellExecute = true })?.Dispose();
    }

    private async Task ExitAsync()
    {
        _tray.Visible = false;
        await _host.DisposeAsync();
        _hotkeys.Dispose();
        _tray.Dispose();
        ExitThread();
    }
}
