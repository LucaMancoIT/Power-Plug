# Power Plug
Erweiterung der Windows-Funktionen durch eine Sammlung von Mikro-Tools und ein eigenes Apps-Center
(PowerToys-Funktionsumfang plus Remote-Fensterfix, Dateikonvertierung, Fast Boot, Schnellsuche, Personalisierung).

- [Architektur](docs/ARCHITEKTUR.md)
- [Roadmap](docs/ROADMAP.md)

Status: Phase 0 (Gerüst: Core, Modul-Host, Awake, Tray-Host, CI). Noch nicht unter Windows gebaut oder gemessen.

## Apps Center / Fast Boot konfigurieren
Vorläufig (bis die Einstellungs-Oberfläche existiert) in `%AppData%\PowerPlug\settings.json`:
```json
{
  "Modules": { "awake": true },
  "Apps": [
    { "Name": "Editor", "Path": "notepad.exe", "InFastBoot": true, "DelayMs": 0 },
    { "Name": "Mein Skript", "Path": "C:\\Skripte\\sync.ps1" },
    { "Name": "Backup", "Path": "C:\\Skripte\\backup.bat", "InFastBoot": true, "DelayMs": 2000 }
  ]
}
```
Tray-Menü: „Fast Boot starten“ startet alle Einträge mit `InFastBoot`, „Apps Center“ listet alle Einträge.
