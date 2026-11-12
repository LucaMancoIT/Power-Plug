# Power Plug – Architektur (Entwurf v0.1)

## Ziel
Windows-Desktop-App mit dem Funktionsumfang von PowerToys plus sechs Erweiterungen
(Remote-Fenster-Fix, Dateikonvertierung, Fast Boot, Tray/Apps Center, Schnellsuche, Personalisierung).

## Technologie-Entscheidung (Vorschlag)
| Schicht | Wahl | Begründung |
|---|---|---|
| Sprache/Runtime | C# / .NET 8 (LTS) | Beste Win32-/COM-/Shell-Interop, gleiche Plattform wie PowerToys |
| UI | WinUI 3 (Einstellungen, Apps Center) + WPF/Win32-Overlays | Overlays (Zonen, Zoom, Ruler) brauchen layered Windows |
| Native Hooks | P/Invoke (CsWin32) | Low-Level-Hooks, WinEvent, DWM, Raw Input |
| Persistenz | JSON in `%AppData%\PowerPlug` + SQLite (Suchindex) | Portabel, einfach sicherbar |
| Installer | MSIX / WiX | Autostart, Shell-Extensions |

## Aufbau
```
src/
  PowerPlug.App/          Tray-Host, Autostart, Einstellungs-UI
  PowerPlug.Core/         IModule, Hotkey-Service, Settings, Logging, IPC
  PowerPlug.Native/       Win32-Interop (Hooks, DWM, Shell)
  PowerPlug.Modules.*/    ein Projekt pro Modul (siehe unten)
  PowerPlug.ShellExt/     Kontextmenü (PowerRename, Image Resizer, File Locksmith, Konvertieren)
  PowerPlug.Search/       Indexer + Suchfenster
tests/
```
Jedes Modul implementiert `IModule` (Id, Start/Stop, Settings-Schema, Hotkeys) und wird
vom Host einzeln aktiviert/deaktiviert.

## Modul-Mapping
| Gruppe | Module |
|---|---|
| Fenster | FancyZones, AlwaysOnTop, CropAndLock, Workspaces, LightSwitch, Awake, ZoomIt |
| Eingabe | KeyboardManager, MouseUtilities, MouseWithoutBorders, QuickAccent |
| Suche | Run/Command Palette (→ Schnellsuche), CommandNotFound |
| Dateien | PowerRename, ImageResizer, FileLocksmith, Previewer (SVG/MD/PDF/G-Code), Peek, New+ |
| Text | AdvancedPaste, TextExtractor (Windows.Media.Ocr) |
| Dev | ColorPicker, ScreenRuler, HostsEditor, RegistryPreview, EnvVars, ShortcutGuide |

## Eigene Erweiterungen
1. **Remote-Fensterkompatibilität** – RemoteApp-/Parallels-Fenster (`TscShellContainerClass`,
   RAIL-Fenster) haben abweichende Z-Order-/Snap-Semantik. Lösung: Fensterklassifizierung +
   Adapter-Schicht für FancyZones/AlwaysOnTop/Workspaces (Positionierung per `SetWindowPos`
   mit DPI-Korrektur, Retry nach Window-Mapping, Fallback-Rahmen-Overlay). Braucht Tests auf
   realen RDP-/Parallels-Umgebungen.
2. **Dateikonvertierung** – Plugin-basierte Konverter-Registry (Format A→B Graph):
   Bilder (Magick.NET/ImageSharp), Dokumente (Pandoc), Audio/Video (FFmpeg), Daten (CSV/JSON/XML/YAML).
   Auswahl im Kontextmenü, Batch-Queue.
3. **Fast Boot** – Liste in Einstellungen (App/Argumente/Verzögerung/Workspace-Verknüpfung);
   Tray-Klick oder Autostart-Option startet alle und legt das Workspace-Layout an.
4. **Tray & Apps Center** – Tray-Icon immer sichtbar (Hinweis: Windows erlaubt kein
   erzwungenes Anpinnen; Erstinstallation führt zur Einstellung „Immer anzeigen“).
   Apps Center startet eingebettete Apps, `.bat`/`.ps1`/`.cmd` mit Ausgabefenster.
5. **Schnellsuche (Alt+Space)** – Hintergrund-Indexer (NTFS-USN-Journal für Dateien, Startmenü
   für Apps), SQLite-FTS, Fuzzy-Ranking, Overlay-Fenster. Konflikt: Alt+Space ist das
   Systemmenü-Kürzel → konfigurierbar, Standard wird beim ersten Start geprüft.
6. **Personalisierung** – Wallpaper-Bibliothek (Online-Katalog, Download, Lizenz-/Quellenangabe),
   animierte Wallpaper (Fenster hinter Desktop-Icons via WorkerW, Video/GIF/WebView2),
   Desktop-Widgets (WebView2-/XAML-Fenster, Widget-SDK).

## Risiken / offene Punkte
- Mouse Without Borders, FancyZones, ZoomIt sind komplex – ggf. Anbindung an bestehendes
  PowerToys (MIT-Lizenz) statt Neuimplementierung prüfen.
- Administrator-/UIPI-Grenzen bei Hooks und Fensterzugriff.
- Online-Wallpaper: Quelle, Lizenzen und Hosting noch zu klären.
- Advanced Paste (KI) benötigt API-Schlüssel-Verwaltung.
- Entwicklung/Test nur unter Windows möglich (diese Cloud-Umgebung ist Linux).

## Ressourcen-Strategie (messbar statt versprochen)
Eine pauschale Garantie ist nicht seriös; stattdessen gelten diese Konstruktionsregeln und Budgets:
1. **Ein schlanker Host-Prozess** (Tray, WinForms – kein WinUI/WPF im Leerlauf). Einstellungs-UI und
   Apps Center laufen als **separate On-Demand-Prozesse** und beenden sich beim Schließen.
2. **Module sind lazy**: deaktivierte Module werden nie instanziiert (Test vorhanden).
3. **Kein Polling**: Hotkeys über `RegisterHotKey`, Ereignisse statt Timer; Awake blockiert ohne CPU.
4. **Schwere Module** (Suchindex, Konvertierung, Wallpaper-Engine, Mouse Without Borders) laufen
   in eigenen Prozessen mit niedriger Priorität und nur bei Bedarf.
5. **Fehlerisolation**: Ausfall/Timeout eines Moduls beeinträchtigt andere nicht (Test vorhanden).
6. **Budgets in CI** (Windows-Runner): Leerlauf-Arbeitsspeicher des Hosts ≤ 60 MB (vorläufig, wird
   nach der ersten Messung angepasst); Log-Datei hart begrenzt (~2 MB); Installationsgröße wird je Phase geprüft.
