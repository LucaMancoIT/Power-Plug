# Power Plug - Architecture (draft v0.2)

## Goal

A Windows desktop app with the feature set of PowerToys plus six extras: remote window fix, file conversion,
Fast Boot, tray Apps Center, system-wide quick search and personalization.

## Technology

| Layer | Choice | Why |
|---|---|---|
| Language / runtime | C# on .NET 8 (LTS) | Best access to Windows APIs; same platform as PowerToys |
| Everyday UI | Windows Forms (tray icon and menu only) | Very light; nothing heavy is loaded while idle |
| Larger windows later | WinUI 3 / WPF in a separate, on-demand process | Settings and the Apps Center editor do not cost anything while closed |
| Native access | P/Invoke and the registry | Hotkeys, windows, theme |
| Persistence | JSON in `%AppData%\PowerPlug`; SQLite for the search index (planned) | Simple and portable |
| Tests | xUnit | Logic is tested without needing Windows |
| Installer (planned) | MSIX / WiX | Autostart, shell extensions |

## Structure

```
src/
  PowerPlug.App/              Tray host (Windows only): icon, menu, global hotkeys, registry access
  PowerPlug.Core/             Module interface and host, settings, log, hotkey abstraction, Fast Boot, launcher
  PowerPlug.Modules.*/        One project per tool (Awake, AlwaysOnTop, LightSwitch, ...)
tests/
  PowerPlug.Tests/            Automatic tests
```

The idea is a house with rooms:

- **The house** is the tray host. It shows the icon and the menu.
- **The caretaker** (`ModuleHost`) switches rooms on and off and contains failures.
- **The rooms** are modules (`IModule`). A switched-off module is never created and costs nothing.

Anything that needs Windows (registry, window functions) sits behind a small interface
(`IWindowApi`, `IThemeApi`, `IExecutionStateApi`, `IHotkeyService`, `IProcessLauncher`).
Tests use fake versions, so all logic can be tested on any computer.

## Module overview

| Group | Modules |
|---|---|
| Window and desktop | FancyZones, AlwaysOnTop, CropAndLock, Workspaces, LightSwitch, Awake, ZoomIt |
| Input | KeyboardManager, MouseUtilities, MouseWithoutBorders, QuickAccent |
| Search | Run / Command Palette (merged into quick search), CommandNotFound |
| Files | PowerRename, ImageResizer, FileLocksmith, previewers (SVG/Markdown/PDF/G-code), Peek, New+ |
| Text | AdvancedPaste, TextExtractor (Windows.Media.Ocr) |
| Developer and system | ColorPicker, ScreenRuler, HostsEditor, RegistryPreview, EnvVars, ShortcutGuide |

## The six extras

1. **Remote window compatibility** - windows of remote apps (RemoteApp, Parallels) behave differently
   regarding stacking order and snapping. Plan: classify windows, add an adapter layer for FancyZones,
   AlwaysOnTop and Workspaces (positioning with DPI correction, retry after the window is mapped,
   fallback overlay). Needs tests on real RDP/Parallels setups.
2. **File conversion** - a plug-in based converter registry (format A to format B graph): images, documents,
   audio/video, data (CSV/JSON/XML/YAML). Context menu entry and a batch queue.
3. **Fast Boot** (done) - a separate list in settings. A click starts all entries, with per-entry delay,
   retries and "wait until host is reachable" for remote apps. Saved workspace layouts follow with Workspaces.
4. **Tray and Apps Center** (done, editor planned) - the tray icon is the entry point; the Apps Center starts
   your own apps, batch files and scripts. Note: Windows does not allow forcing an icon to stay visible;
   the user turns this on once in the taskbar settings.
5. **Quick search** (planned) - background indexer (NTFS change journal for files, Start menu for apps),
   SQLite full-text search, fuzzy ranking, overlay window. Alt+Space is the Windows window-menu shortcut,
   so the hotkey is configurable.
6. **Personalization** (planned) - online wallpaper library, animated wallpapers (window behind the desktop
   icons, video/GIF/WebView2) and desktop widgets.

## Resource strategy (measured, not promised)

A blanket guarantee is not honest; these rules and budgets apply instead:

1. **One lean host process** (tray, Windows Forms - no heavy UI framework while idle). The settings window and
   the Apps Center editor will be separate on-demand processes that end when closed.
2. **Modules are lazy**: switched-off modules are never created (covered by a test).
3. **No polling**: hotkeys use `RegisterHotKey`; Light Switch uses one timer set to the next switch plus clock/resume events;
   Awake waits without using CPU.
4. **Heavy modules** (search index, conversion, wallpaper engine, Mouse Without Borders) will run in their own
   low-priority processes and only when needed.
5. **Fault isolation**: a failing or hanging module does not affect the others (covered by a test).
6. **Budgets in CI** (Windows runner): private idle memory of the host at most 25 MB
   (measured on Windows: 10.8 MB; the working set of 52.9 MB mostly contains shared pages and is not a budget value);
   the log file is capped at about 2 MB.

## Risks and open points

- Mouse Without Borders, FancyZones and ZoomIt are complex; reusing the MIT-licensed PowerToys code may be better than rewriting.
- Administrator rights and Windows UI privilege isolation limit hooks and window access.
- Online wallpapers: source, licenses and hosting are still to be decided.
- Advanced Paste (AI) needs secure API-key handling.
- Windows Smart App Control can block unsigned programs; code signing is required before distribution.
- Development and real tests are only possible on Windows (the cloud environment used for coding is Linux).
