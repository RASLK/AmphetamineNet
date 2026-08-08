## Language / Язык / Sprache / Langue / 语言 / 言語

| | |
| --- | --- |
| English (default) | [README.md](./README.md) |
| Русский | [docs/README.ru.md](./docs/README.ru.md) |
| Deutsch | [docs/README.de.md](./docs/README.de.md) |
| Français | [docs/README.fr.md](./docs/README.fr.md) |
| 中文 | [docs/README.zh.md](./docs/README.zh.md) |
| 日本語 | [docs/README.ja.md](./docs/README.ja.md) |

---

# AmphetamineNet

[![CI](https://github.com/RASLK/AmphetamineNet/actions/workflows/ci.yml/badge.svg)](https://github.com/RASLK/AmphetamineNet/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](./LICENSE)
[![Platform: macOS](https://img.shields.io/badge/platform-macOS-lightgrey.svg)](#requirements)

A lightweight **macOS** menu bar utility that keeps your Mac awake — an open-source alternative
to [Amphetamine](https://apps.apple.com/app/amphetamine/id937984704), built with **Avalonia + .NET 10**.

Live in the menu bar (no Dock icon), start a session for a fixed duration or indefinitely, and
optionally keep the Mac awake even with the lid closed or the display asleep.

## Stack

| Layer | Technology |
| --- | --- |
| UI | Avalonia 12, FluentTheme |
| Runtime | .NET 10 (LTS) |
| MVVM | CommunityToolkit.Mvvm (source generators) |
| Power management | IOKit `IOPMAssertion*` (system/display sleep) + IOKit clamshell state + `pmset disablesleep` via a scoped, passwordless `sudoers` helper |
| Native interop | Direct `objc_msgSend` calls (menu bar–only "Accessory" activation policy) |
| Packaging | `hdiutil`-built DMG, GitHub Actions |

## Features

- Menu bar–only app: no Dock icon, no application menu — everything lives in the tray icon
- Start / stop a keep-awake session from the tray, with a duration picker: **Indefinitely**, 5 / 15 / 30 minutes, 1 / 2 / 5 hours
- **Allow closed lid** — keeps the Mac fully awake with the lid closed, on both AC and battery power
- **Keep display awake** — in addition to preventing system sleep, also prevents the display from sleeping
- A small settings window (opened via the tray menu) mirrors the same controls for a larger surface
- Session state and lid status are reflected live in both the tray menu and the settings window
- Settings persist across launches (`~/Library/Application Support/AmphetamineNet/settings.json`)
- Built-in workaround for the macOS 26 `Avalonia.Native` render-timer crash (`CVDisplayLinkCreateWithActiveCGDisplays`, error `-6661`) via a small interpose dylib

## How "closed lid" works

macOS normally forces sleep when the lid is closed, unless a clamshell-mode override is active
(the same mechanism used when an external display is connected). AmphetamineNet combines two
pieces to keep the Mac awake with the lid closed, on battery as well as on AC power:

1. **IOKit clamshell assertion** (`kPMSetClamshellSleepState` on `IOPMrootDomain`) — works while the app is running.
2. **`pmset -a disablesleep 1`** — the setting IOKit's `SleepDisabled` flag actually depends on. Running this without a password prompt every time requires a one-time `sudo` grant.

On first use of "Allow closed lid", AmphetamineNet asks for administrator credentials **once**,
via a native macOS prompt, to install a narrowly scoped `sudoers.d` rule
(`/etc/sudoers.d/amphetamine-net`) that allows only:

```
/usr/bin/pmset -a disablesleep 1
/usr/bin/pmset -a disablesleep 0
```

for the current user, without a password. From then on, sessions with a closed lid start and
stop without any further prompts. A background heartbeat re-applies the clamshell assertion
every 30 seconds in case the OS resets it.

## Requirements

- **macOS 12 (Monterey) or later** — this app does not run on other platforms, and the Avalonia window shows an explanatory message if launched elsewhere
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (for building from source)
- Xcode Command Line Tools (`clang`) — needed once, to build the small CVDisplayLink interpose dylib

## Build & run

```bash
git clone https://github.com/RASLK/AmphetamineNet.git
cd AmphetamineNet
dotnet restore
```

### Run from source (recommended for development)

`run-macos.sh` builds the interpose dylib if missing, builds the app, and runs it with the
environment variables the macOS 26 render-timer workaround needs:

```bash
./run-macos.sh          # Debug build
./run-macos.sh Release  # Release build
```

Logs are written to `/tmp/amphetamine-net-run.log` (runtime) and `/tmp/amphetamine-net.log`
(in-app diagnostics).

### Plain `dotnet run`

```bash
dotnet run -c Release
```

This also works, but on macOS 26+ the app self-relaunches once with
`DYLD_INSERT_LIBRARIES` set to the CVDisplayLink fix dylib (built automatically by the
`BuildCvDisplayLinkFix` MSBuild target the first time you build on macOS).

### Local publish (single-file, self-contained)

```bash
dotnet publish AmphetamineNet.csproj -c Release -r osx-arm64 -o publish/osx-arm64   # Apple Silicon
dotnet publish AmphetamineNet.csproj -c Release -r osx-x64   -o publish/osx-x64     # Intel
```

### Build a DMG locally

```bash
clang -dynamiclib -o Native/libcvdisplaylink_fix.dylib Native/cvdisplaylink_fix.c \
  -framework CoreVideo -framework CoreGraphics -install_name @rpath/libcvdisplaylink_fix.dylib

dotnet publish AmphetamineNet.csproj -c Release -r osx-arm64 -o publish/osx-arm64 -p:UseAppHost=true

packaging/macos/create-app-dmg.sh \
  publish/osx-arm64/AmphetamineNet 1.0.0 dist/AmphetamineNet-1.0.0-macos-arm64.dmg \
  Assets/tray.png publish/osx-arm64/libcvdisplaylink_fix.dylib
```

### GitHub Actions (CI)

| Workflow | Purpose |
| --- | --- |
| `ci.yml` | Builds the project on every push/PR to `main` (macOS runner) |
| `macos-pack.yml` | Publishes `osx-arm64` and `osx-x64`, builds a DMG + `.sha256sum` for each — runnable manually or from `release.yml` |
| `release.yml` | On a `vX.Y.Z` tag → runs `macos-pack.yml` → publishes a GitHub Release with both DMGs |

```bash
git tag v1.0.0
git push origin v1.0.0
```

**macOS note:** DMGs are unsigned (no Apple Developer ID). On first launch, right-click the app
→ **Open** to bypass Gatekeeper's unidentified-developer warning.

## Architecture

```
AmphetamineNet/
  App.axaml(.cs)          # Avalonia lifecycle, tray icon + menu, window activation policy
  Program.cs               # entry point, macOS 26 CVDisplayLink relaunch workaround
  Models/                  # data models
  Services/
    AppSettings.cs          # JSON-persisted user settings
    MacKeepAwakeService.cs  # IOPM assertions, clamshell state, pmset orchestration
    PowerProtect.cs          # one-time passwordless sudoers installation for pmset
  Native/
    IoKitNative.cs           # IOKit / CoreFoundation P/Invoke declarations
    MacAppActivation.cs      # Accessory ↔ Regular NSApplication activation policy switching
    cvdisplaylink_fix.c       # interpose dylib source for the macOS 26 render-timer crash
  ViewModels/               # MVVM view models (CommunityToolkit.Mvvm)
  Views/                    # Avalonia windows
```

## License

[MIT](./LICENSE) © Ruslan Khairulin
