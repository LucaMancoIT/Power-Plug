# Power Plug
Erweiterung der Windows-Funktionen durch eine Sammlung von Mikro-Tools und ein eigenes Apps-Center
(PowerToys-Funktionsumfang plus Remote-Fensterfix, Dateikonvertierung, Fast Boot, Schnellsuche, Personalisierung).

- [Architektur](docs/ARCHITEKTUR.md)
- [Roadmap](docs/ROADMAP.md)

Status: Phase 0 (Gerüst: Core, Modul-Host, Awake, Tray-Host, CI). Noch nicht unter Windows gebaut oder gemessen.

## Apps Center und Fast Boot konfigurieren
Zwei getrennte Listen in `%AppData%\PowerPlug\settings.json` (bis die Einstellungs-Oberfläche existiert):
```json
{
  "Modules": { "always-on-top": true },
  "AppsCenter": [
    { "Name": "Mein Skript", "Path": "C:\\Skripte\\sync.ps1" },
    { "Name": "Backup", "Path": "C:\\Skripte\\backup.bat" }
  ],
  "FastBoot": [
    { "Name": "Editor", "Path": "notepad.exe" },
    { "Name": "Excel (Remote)", "Path": "C:\\Users\\ich\\AppData\\Roaming\\Microsoft\\Windows\\Start Menu\\Programs\\Excel.lnk",
      "DelayMs": 5000, "Retries": 2, "RetryDelayMs": 5000,
      "WaitForHost": "gateway.firma.de:3389", "WaitTimeoutSec": 120 },
    { "Name": "Rechner (Store-App)", "Path": "shell:AppsFolder\\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App" }
  ]
}
```
- **Apps Center** (Tray-Untermenü): eigene Apps, Batch-Dateien und Skripte (`.ps1` läuft mit `-ExecutionPolicy Bypass`).
- **Fast Boot** (Tray: „Fast Boot starten“): eigene, unabhängige Liste beliebiger Programme. Erlaubt sind `.exe`, `.lnk`, `.rdp`, URLs und `shell:AppsFolder\…` (Store-Apps; Liste per `Get-StartApps`).
- **Virtualisierte/Remote-Apps** (RDS RemoteApp, Parallels, Citrix …) legen Startmenü-Verknüpfungen an. „Startmenü-Apps für Fast Boot exportieren…“ erzeugt `startmenu-apps.json` mit allen Verknüpfungen; gewünschte Einträge nach `FastBoot` kopieren und `Enabled` auf `true` setzen.
- Pro Fast-Boot-Eintrag: `DelayMs` (Wartezeit), `Retries`/`RetryDelayMs` (Wiederholung), `WaitForHost` (startet erst, wenn `host:port` erreichbar ist, Standardport 3389) und `WaitTimeoutSec`.
- Tray: „Mit Windows starten“ aktiviert den Autostart der App (Fast Boot selbst startet per Klick).

## Light Switch
Modul `light-switch` (Tray-Schalter). Wechselt nach Uhrzeit zwischen hellem und dunklem Design:
```json
"LightSwitch": { "LightFrom": "07:00", "DarkFrom": "19:00", "ApplyToApps": true, "ApplyToSystem": true }
```
Zeiten im Format `HH:mm`; Pläne über Mitternacht (z. B. hell ab 22:00, dunkel ab 06:00) funktionieren. Änderungen an den Zeiten gelten ab der nächsten Auswertung (Moduls neu schalten oder Neustart der App).
