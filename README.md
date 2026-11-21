# Power Plug

Power Plug extends Windows with a collection of small tools and its own app launcher.
It combines the feature set of Microsoft PowerToys with extra features:

- fixes for window management with remote apps (Remote Desktop, Parallels, ...),
- file conversion,
- "Fast Boot" (start a list of apps with one click),
- a tray icon with an Apps Center for your own apps and scripts,
- system-wide quick search,
- personalization (wallpapers and desktop widgets).

Power Plug runs quietly in the background and is designed to use very little memory and CPU.

- [Architecture](docs/ARCHITECTURE.md)
- [Roadmap](docs/ROADMAP.md)

## Status

Phase 0 and Phase 1 are done: core, module host, tray host, Awake, Always On Top, Light Switch,
Apps Center, Fast Boot and Autostart. See the [roadmap](docs/ROADMAP.md) for what comes next.

## Build and test (Windows, .NET 8 SDK)

```bash
dotnet build PowerPlug.sln -c Release
dotnet test PowerPlug.sln -c Release --no-build
dotnet run --project src/PowerPlug.App -c Release
```

After starting, look for the Power Plug icon next to the clock. On Windows 11 it may first be hidden in the
overflow menu (the small arrow). To keep it visible: Settings, Personalization, Taskbar,
"Other system tray icons", then switch on "PowerPlug".

## Configuration

Settings are stored in `%AppData%\PowerPlug\settings.json` (until a settings window exists you edit this file;
the tray menu has "Open configuration folder"). Logs are in `%AppData%\PowerPlug\powerplug.log`.

### Turning tools on and off

Use the tray menu (right-click the icon). Your choice is remembered in the `Modules` section.

### Apps Center and Fast Boot

These are two separate lists:

- **Apps Center** (tray sub-menu): your own apps, batch files and scripts.
- **Fast Boot** (tray: "Start Fast Boot"): a list of any programs on your PC that are started together.

```json
{
  "Modules": { "always-on-top": true },
  "AppsCenter": [
    { "Name": "My script", "Path": "C:\\Scripts\\sync.ps1" },
    { "Name": "Backup", "Path": "C:\\Scripts\\backup.bat" }
  ],
  "FastBoot": [
    { "Name": "Editor", "Path": "notepad.exe" },
    { "Name": "Excel (remote)", "Path": "C:\\Users\\me\\AppData\\Roaming\\Microsoft\\Windows\\Start Menu\\Programs\\Excel.lnk",
      "DelayMs": 5000, "Retries": 2, "RetryDelayMs": 5000,
      "WaitForHost": "gateway.example.com:3389", "WaitTimeoutSec": 120 },
    { "Name": "Calculator (Store app)", "Path": "shell:AppsFolder\\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App" }
  ]
}
```

- What can be started: `.exe`, `.bat`, `.cmd`, `.ps1` (run with `-ExecutionPolicy Bypass`), shortcuts (`.lnk`),
  remote desktop files (`.rdp`), web addresses, and Microsoft Store apps (`shell:AppsFolder\...`;
  list them with `Get-StartApps` in PowerShell).
- **Virtualized or remote apps** (RemoteApp, Parallels, Citrix, ...) usually create Start menu shortcuts.
  The tray item "Export Start menu apps for Fast Boot..." writes all of them to `startmenu-apps.json`.
  Copy the ones you want into `FastBoot` and set `Enabled` to `true`.
- Options per Fast Boot entry:

  | Option | Meaning |
  | --- | --- |
  | `Enabled` | `false` skips the entry. |
  | `DelayMs` | Wait this many milliseconds before starting. |
  | `Retries`, `RetryDelayMs` | Try again this many times, waiting between attempts. |
  | `WaitForHost` | Start only when `host:port` accepts connections (port defaults to 3389). |
  | `WaitTimeoutSec` | How long to wait for `WaitForHost` before skipping the entry. |

- The tray item "Start with Windows" adds Power Plug to the Windows startup list. Fast Boot itself runs when you click it.

### Light Switch

Module `light-switch` (switch it on in the tray menu). It changes between the light and dark Windows theme by time of day:

```json
"LightSwitch": { "LightFrom": "07:00", "DarkFrom": "19:00", "ApplyToApps": true, "ApplyToSystem": true }
```

Times use the format `HH:mm`. Schedules that cross midnight work too (for example light from 22:00, dark from 06:00).
The times are re-read on every check; the module itself is switched on or off in the tray menu.

## Project layout

```
src/
  PowerPlug.Core/                  Shared building blocks (module host, settings, log, Fast Boot, ...)
  PowerPlug.Modules.Awake/         Keep the PC awake
  PowerPlug.Modules.AlwaysOnTop/   Keep a window on top (Win+Ctrl+T)
  PowerPlug.Modules.LightSwitch/   Light/dark theme by time of day
  PowerPlug.App/                   The tray program (Windows only)
tests/
  PowerPlug.Tests/                 Automatic tests
docs/                              Architecture and roadmap
```

Everything except `PowerPlug.App` is platform-neutral and covered by automated tests.

## License

See [LICENSE](LICENSE).
