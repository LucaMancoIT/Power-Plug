using System.Windows.Forms;
using PowerPlug.Core.Hotkeys;
using PowerPlug.Core.Logging;
using PowerPlug.Core.Modules;
using PowerPlug.Core.Settings;
using PowerPlug.Modules.Awake;

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

    public TrayContext(string dataDir, ILog log)
    {
        _store = new JsonSettingsStore<AppSettings>(Path.Combine(dataDir, "settings.json"), log);
        _settings = _store.Load();
        _host = new ModuleHost(new Ctx(log, _hotkeys));
        _host.Register(new ModuleDescriptor(AwakeModule.Id, "Awake (PC wach halten)",
            () => new AwakeModule(new WindowsExecutionStateApi())));

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
        // Settings UI and Apps Center will be separate on-demand processes (zero idle cost).
        menu.Items.Add(new ToolStripMenuItem("Einstellungen öffnen…") { Enabled = false });
        menu.Items.Add(new ToolStripMenuItem("Apps Center…") { Enabled = false });
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

    private async Task ExitAsync()
    {
        _tray.Visible = false;
        await _host.DisposeAsync();
        _hotkeys.Dispose();
        _tray.Dispose();
        ExitThread();
    }
}
