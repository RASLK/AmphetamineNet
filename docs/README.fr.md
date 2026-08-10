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

Un utilitaire léger pour la barre de menus **macOS** qui empêche votre Mac de s'endormir — une
alternative open source à [Amphetamine](https://apps.apple.com/app/amphetamine/id937984704),
développée avec **Avalonia + .NET 10**.

Vit dans la barre de menus (pas d'icône dans le Dock), démarre une session pour une durée fixe ou
indéfiniment, et peut en option empêcher le Mac de s'endormir même capot fermé ou écran éteint.

## Stack

| Couche | Technologie |
| --- | --- |
| UI | Avalonia 12, FluentTheme |
| Runtime | .NET 10 (LTS) |
| MVVM | CommunityToolkit.Mvvm (générateurs de code source) |
| Gestion de l'énergie | IOKit `IOPMAssertion*` (veille système/écran) + état du capot via IOKit + `pmset disablesleep` via un assistant `sudoers` restreint et sans mot de passe |
| Interop native | Appels directs à `objc_msgSend` (politique d'activation « Accessory » réservée à la barre de menus) |
| Packaging | DMG construit avec `hdiutil`, GitHub Actions |

## Fonctionnalités

- Application uniquement dans la barre de menus : pas d'icône dans le Dock, pas de menu d'application — tout se passe dans l'icône de la barre système
- Menu : **Minuteur**, **Modificateurs**, **Langue**, puis **Démarrer/Arrêter la session**
- Démarrer / arrêter une session depuis la barre système ; choisir une durée démarre la session
- Durées : **Indéfiniment**, 5 / 15 / 30 minutes, 1 / 2 / 5 heures, plus une durée personnalisée mémorisée
- Compte à rebours en direct à côté de **Actif** pour les sessions minutées
- Icône pilule dynamique : noire au repos, verte avec minuteur, rouge sans limite ; les modificateurs changent le remplissage et les barres
- **Autoriser le capot fermé** — garde le Mac totalement éveillé capot fermé, sur secteur comme sur batterie
- **Garder l'écran allumé** — en plus d'empêcher la veille système, empêche aussi l'écran de s'éteindre
- Localisation de l'interface pour les langues principales (choix dans la barre système)
- Les réglages sont conservés d'un lancement à l'autre (`~/Library/Application Support/AmphetamineNet/settings.json`)
- Contournement intégré pour le plantage du timer de rendu `Avalonia.Native` sous macOS 26 (`CVDisplayLinkCreateWithActiveCGDisplays`, erreur `-6661`) via une petite dylib d'interposition

## Comment fonctionne le « capot fermé »

macOS force normalement la mise en veille lorsque le capot est fermé, sauf si un mode clamshell
est actif (le même mécanisme utilisé lorsqu'un écran externe est connecté). AmphetamineNet combine
deux mécanismes pour garder le Mac éveillé capot fermé, aussi bien sur batterie que sur secteur :

1. **Assertion clamshell IOKit** (`kPMSetClamshellSleepState` sur `IOPMrootDomain`) — fonctionne tant que l'application tourne.
2. **`pmset -a disablesleep 1`** — le réglage dont dépend réellement le drapeau `SleepDisabled` d'IOKit. Exécuter cette commande sans invite de mot de passe à chaque fois nécessite une autorisation `sudo` accordée une seule fois.

À la première utilisation de « Autoriser le capot fermé », AmphetamineNet demande **une seule
fois** les identifiants administrateur, via une invite native macOS, pour installer une règle
`sudoers.d` étroitement limitée (`/etc/sudoers.d/amphetamine-net`) qui n'autorise que :

```
/usr/bin/pmset -a disablesleep 1
/usr/bin/pmset -a disablesleep 0
```

pour l'utilisateur courant, sans mot de passe. À partir de là, les sessions capot fermé démarrent
et s'arrêtent sans aucune autre invite. Un heartbeat en arrière-plan réapplique l'assertion
clamshell toutes les 30 secondes au cas où le système la réinitialiserait.

## Prérequis

- **macOS 12 (Monterey) ou ultérieur** — cette application ne fonctionne sur aucune autre plateforme, et la fenêtre Avalonia affiche un message explicatif si elle est lancée ailleurs
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (pour compiler depuis les sources)
- Xcode Command Line Tools (`clang`) — nécessaires une fois, pour compiler la petite dylib d'interposition CVDisplayLink

## Compilation et exécution

```bash
git clone https://github.com/RASLK/AmphetamineNet.git
cd AmphetamineNet
dotnet restore
```

### Exécuter depuis les sources (recommandé pour le développement)

`run-macos.sh` compile la dylib d'interposition si elle est absente, compile l'application et la
lance avec les variables d'environnement nécessaires au contournement du problème de timer de
rendu de macOS 26 :

```bash
./run-macos.sh          # Build Debug
./run-macos.sh Release  # Build Release
```

Les journaux sont écrits dans `/tmp/amphetamine-net-run.log` (exécution) et
`/tmp/amphetamine-net.log` (diagnostics internes de l'application).

### `dotnet run` simple

```bash
dotnet run -c Release
```

Cela fonctionne aussi, mais sous macOS 26+ l'application se relance elle-même une fois avec la
variable `DYLD_INSERT_LIBRARIES` pointant vers la dylib de correctif CVDisplayLink (construite
automatiquement par la cible MSBuild `BuildCvDisplayLinkFix` lors de la première compilation sous
macOS).

### Publication locale (fichier unique, autonome)

```bash
dotnet publish AmphetamineNet.csproj -c Release -r osx-arm64 -o publish/osx-arm64   # Apple Silicon
dotnet publish AmphetamineNet.csproj -c Release -r osx-x64   -o publish/osx-x64     # Intel
```

### Construire un DMG en local

```bash
clang -dynamiclib -o Native/libcvdisplaylink_fix.dylib Native/cvdisplaylink_fix.c \
  -framework CoreVideo -framework CoreGraphics -install_name @rpath/libcvdisplaylink_fix.dylib

dotnet publish AmphetamineNet.csproj -c Release -r osx-arm64 -o publish/osx-arm64 -p:UseAppHost=true

packaging/macos/create-app-dmg.sh \
  publish/osx-arm64/AmphetamineNet 1.0.0 dist/AmphetamineNet-1.0.0-macos-arm64.dmg \
  Assets/tray.png publish/osx-arm64/libcvdisplaylink_fix.dylib
```

### GitHub Actions (CI)

| Workflow | Rôle |
| --- | --- |
| `ci.yml` | Compile le projet à chaque push/PR sur `main` (runner macOS) |
| `macos-pack.yml` | Publie `osx-arm64` et `osx-x64`, construit un DMG + `.sha256sum` pour chacun — exécutable manuellement ou depuis `release.yml` |
| `release.yml` | Sur un tag `vX.Y.Z` → exécute `macos-pack.yml` → publie une Release GitHub avec les deux DMG |

```bash
git tag v1.0.0
git push origin v1.0.0
```

**Remarque macOS :** les DMG ne sont pas signés (pas d'Apple Developer ID). Au premier
lancement, effectuez un clic droit sur l'application → **Ouvrir** pour contourner l'avertissement
Gatekeeper concernant un développeur non identifié.

## Architecture

```
AmphetamineNet/
  App.axaml(.cs)          # cycle de vie Avalonia, icône de barre système + menu, politique d'activation des fenêtres
  Program.cs               # point d'entrée, contournement de relance CVDisplayLink pour macOS 26
  Models/                  # modèles de données
  Services/
    AppSettings.cs          # réglages utilisateur persistés en JSON
    MacKeepAwakeService.cs  # assertions IOPM, état du capot, orchestration de pmset
    PowerProtect.cs          # installation ponctuelle et sans mot de passe des sudoers pour pmset
  Native/
    IoKitNative.cs           # déclarations P/Invoke pour IOKit / CoreFoundation
    MacAppActivation.cs      # bascule de la politique d'activation NSApplication Accessory ↔ Regular
    cvdisplaylink_fix.c       # source de la dylib d'interposition pour le plantage du timer de rendu sous macOS 26
  ViewModels/               # view models MVVM (CommunityToolkit.Mvvm)
  Views/                    # fenêtres Avalonia
```

## Licence

[MIT](./LICENSE) © Ruslan Khairulin
