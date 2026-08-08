## Language / Язык / Sprache / Langue / 语言 / 言語

| | |
| --- | --- |
| English (default) | [../README.md](../README.md) |
| Русский | [README.ru.md](./README.ru.md) |
| Deutsch | [README.de.md](./README.de.md) |
| Français | [README.fr.md](./README.fr.md) |
| 中文 | [README.zh.md](./README.zh.md) |
| 日本語 | [README.ja.md](./README.ja.md) |

---

# AmphetamineNet

[![CI](https://github.com/RASLK/AmphetamineNet/actions/workflows/ci.yml/badge.svg)](https://github.com/RASLK/AmphetamineNet/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](./LICENSE)
[![Platform: macOS](https://img.shields.io/badge/platform-macOS-lightgrey.svg)](#requirements)

Ein schlankes **macOS**-Menüleisten-Tool, das deinen Mac wach hält — eine quelloffene Alternative
zu [Amphetamine](https://apps.apple.com/app/amphetamine/id937984704), gebaut mit **Avalonia + .NET 10**.

Lebt in der Menüleiste (kein Dock-Icon), startet eine Sitzung für eine feste Dauer oder unbegrenzt
und hält den Mac optional auch bei geschlossenem Klapp-Deckel oder ausgeschaltetem Display wach.

## Stack

| Schicht | Technologie |
| --- | --- |
| UI | Avalonia 12, FluentTheme |
| Runtime | .NET 10 (LTS) |
| MVVM | CommunityToolkit.Mvvm (Source-Generatoren) |
| Energieverwaltung | IOKit `IOPMAssertion*` (System-/Display-Schlaf) + IOKit-Klappzustand + `pmset disablesleep` über einen eng begrenzten, passwortlosen `sudoers`-Helfer |
| Native Interop | Direkte `objc_msgSend`-Aufrufe (reine Menüleisten-„Accessory“-Aktivierungsrichtlinie) |
| Packaging | Mit `hdiutil` gebautes DMG, GitHub Actions |

## Funktionen

- Reine Menüleisten-App: kein Dock-Icon, kein Anwendungsmenü — alles läuft über das Tray-Symbol
- Keep-awake-Sitzung über das Tray starten/stoppen, mit Dauerauswahl: **Unbegrenzt**, 5 / 15 / 30 Minuten, 1 / 2 / 5 Stunden
- **Geschlossenen Deckel erlauben** — hält den Mac bei geschlossenem Deckel voll wach, sowohl am Netzteil als auch im Akkubetrieb
- **Display wach halten** — verhindert zusätzlich zum System-Schlaf auch das Einschlafen des Displays
- Ein kleines Einstellungsfenster (über das Tray-Menü zu öffnen) spiegelt dieselben Steuerelemente auf größerer Fläche
- Sitzungsstatus und Deckelzustand werden live sowohl im Tray-Menü als auch im Einstellungsfenster angezeigt
- Einstellungen bleiben über Neustarts hinweg erhalten (`~/Library/Application Support/AmphetamineNet/settings.json`)
- Eingebaute Umgehung für den `Avalonia.Native`-Render-Timer-Absturz unter macOS 26 (`CVDisplayLinkCreateWithActiveCGDisplays`, Fehler `-6661`) über eine kleine Interpose-Dylib

## Wie „geschlossener Deckel“ funktioniert

macOS erzwingt normalerweise den Ruhezustand bei geschlossenem Deckel, außer ein Clamshell-Modus-
Override ist aktiv (derselbe Mechanismus, der bei angeschlossenem externem Display greift).
AmphetamineNet kombiniert zwei Bausteine, um den Mac bei geschlossenem Deckel wach zu halten —
sowohl im Akku- als auch im Netzbetrieb:

1. **IOKit-Clamshell-Assertion** (`kPMSetClamshellSleepState` auf `IOPMrootDomain`) — funktioniert, solange die App läuft.
2. **`pmset -a disablesleep 1`** — die Einstellung, von der IOKits `SleepDisabled`-Flag tatsächlich abhängt. Um diesen Befehl ohne Passwortabfrage bei jedem Mal auszuführen, ist eine einmalige `sudo`-Freigabe nötig.

Beim ersten Einsatz von „Geschlossenen Deckel erlauben“ fragt AmphetamineNet **einmalig** über
einen nativen macOS-Dialog nach Administratorrechten, um eine eng begrenzte `sudoers.d`-Regel
(`/etc/sudoers.d/amphetamine-net`) zu installieren, die ausschließlich Folgendes erlaubt:

```
/usr/bin/pmset -a disablesleep 1
/usr/bin/pmset -a disablesleep 0
```

für den aktuellen Benutzer, ohne Passwort. Ab diesem Zeitpunkt starten und stoppen Sitzungen mit
geschlossenem Deckel ohne weitere Abfragen. Ein Hintergrund-Heartbeat legt die Clamshell-Assertion
alle 30 Sekunden erneut an, falls das System sie zurücksetzt.

## Voraussetzungen

- **macOS 12 (Monterey) oder neuer** — diese App läuft auf keiner anderen Plattform, das Avalonia-Fenster zeigt beim Start anderswo einen erklärenden Hinweis
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (zum Bauen aus dem Quellcode)
- Xcode Command Line Tools (`clang`) — einmalig nötig, um die kleine CVDisplayLink-Interpose-Dylib zu bauen

## Bauen & starten

```bash
git clone https://github.com/RASLK/AmphetamineNet.git
cd AmphetamineNet
dotnet restore
```

### Aus dem Quellcode starten (empfohlen für die Entwicklung)

`run-macos.sh` baut die Interpose-Dylib, falls sie fehlt, baut die App und startet sie mit den
Umgebungsvariablen, die für die Umgehung des macOS-26-Render-Timer-Problems nötig sind:

```bash
./run-macos.sh          # Debug-Build
./run-macos.sh Release  # Release-Build
```

Logs werden nach `/tmp/amphetamine-net-run.log` (Laufzeit) und `/tmp/amphetamine-net.log`
(App-interne Diagnose) geschrieben.

### Einfaches `dotnet run`

```bash
dotnet run -c Release
```

Das funktioniert ebenfalls, aber unter macOS 26+ startet sich die App einmalig selbst neu, mit
gesetzter `DYLD_INSERT_LIBRARIES`-Variable, die auf die CVDisplayLink-Fix-Dylib zeigt (wird beim
ersten Bauen unter macOS automatisch vom MSBuild-Target `BuildCvDisplayLinkFix` erzeugt).

### Lokale Veröffentlichung (Single-File, self-contained)

```bash
dotnet publish AmphetamineNet.csproj -c Release -r osx-arm64 -o publish/osx-arm64   # Apple Silicon
dotnet publish AmphetamineNet.csproj -c Release -r osx-x64   -o publish/osx-x64     # Intel
```

### DMG lokal bauen

```bash
clang -dynamiclib -o Native/libcvdisplaylink_fix.dylib Native/cvdisplaylink_fix.c \
  -framework CoreVideo -framework CoreGraphics -install_name @rpath/libcvdisplaylink_fix.dylib

dotnet publish AmphetamineNet.csproj -c Release -r osx-arm64 -o publish/osx-arm64 -p:UseAppHost=true

packaging/macos/create-app-dmg.sh \
  publish/osx-arm64/AmphetamineNet 1.0.0 dist/AmphetamineNet-1.0.0-macos-arm64.dmg \
  Assets/tray.png publish/osx-arm64/libcvdisplaylink_fix.dylib
```

### GitHub Actions (CI)

| Workflow | Zweck |
| --- | --- |
| `ci.yml` | Baut das Projekt bei jedem Push/PR auf `main` (macOS-Runner) |
| `macos-pack.yml` | Veröffentlicht `osx-arm64` und `osx-x64`, baut je ein DMG + `.sha256sum` — manuell oder aus `release.yml` startbar |
| `release.yml` | Bei einem `vX.Y.Z`-Tag → führt `macos-pack.yml` aus → veröffentlicht ein GitHub Release mit beiden DMGs |

```bash
git tag v1.0.0
git push origin v1.0.0
```

**Hinweis zu macOS:** Die DMGs sind unsigniert (keine Apple Developer ID). Beim ersten Start
Rechtsklick auf die App → **Öffnen**, um Gatekeepers Warnung vor nicht identifizierten
Entwicklern zu umgehen.

## Architektur

```
AmphetamineNet/
  App.axaml(.cs)          # Avalonia-Lebenszyklus, Tray-Symbol + Menü, Fensteraktivierungsrichtlinie
  Program.cs               # Einstiegspunkt, Neustart-Umgehung für CVDisplayLink unter macOS 26
  Models/                  # Datenmodelle
  Services/
    AppSettings.cs          # JSON-persistierte Benutzereinstellungen
    MacKeepAwakeService.cs  # IOPM-Assertions, Deckelzustand, pmset-Orchestrierung
    PowerProtect.cs          # einmalige passwortlose sudoers-Installation für pmset
  Native/
    IoKitNative.cs           # P/Invoke-Deklarationen für IOKit / CoreFoundation
    MacAppActivation.cs      # Umschalten der NSApplication-Aktivierungsrichtlinie Accessory ↔ Regular
    cvdisplaylink_fix.c       # Quelle der Interpose-Dylib für den Render-Timer-Absturz unter macOS 26
  ViewModels/               # MVVM-View-Modelle (CommunityToolkit.Mvvm)
  Views/                    # Avalonia-Fenster
```

## Lizenz

[MIT](./LICENSE) © Ruslan Khairulin
