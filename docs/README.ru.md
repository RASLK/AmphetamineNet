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

Лёгкая утилита для строки меню **macOS**, которая не даёт Mac уснуть — открытая альтернатива
приложению [Amphetamine](https://apps.apple.com/app/amphetamine/id937984704), написанная на **Avalonia + .NET 10**.

Живёт в строке меню (без иконки в Dock), запускает сеанс на фиксированное время или бессрочно
и, при желании, не даёт Mac уснуть даже с закрытой крышкой или погасшим экраном.

## Стек

| Слой | Технология |
| --- | --- |
| UI | Avalonia 12, FluentTheme |
| Runtime | .NET 10 (LTS) |
| MVVM | CommunityToolkit.Mvvm (генераторы кода) |
| Управление питанием | IOKit `IOPMAssertion*` (сон системы/экрана) + состояние крышки через IOKit + `pmset disablesleep` через ограниченный passwordless-хелпер `sudoers` |
| Нативное взаимодействие | Прямые вызовы `objc_msgSend` (политика активации «Accessory» — только строка меню) |
| Упаковка | DMG, собранный через `hdiutil`, GitHub Actions |

## Возможности

- Приложение только в строке меню: без иконки в Dock, без строки меню приложения — всё живёт в иконке трея
- Меню трея: **Таймер**, **Модификаторы**, **Язык**, затем **Запустить/Остановить сессию**
- Запуск / остановка сеанса «не спать» из трея; выбор длительности сразу запускает сессию
- Длительности: **Бессрочно**, 5 / 15 / 30 минут, 1 / 2 / 5 часов, плюс запоминаемое своё время
- Живой обратный отсчёт рядом с **Активна** для сеанса с таймером
- Динамическая иконка-таблетка: чёрная в покое, зелёная с таймером, красная без таймера; модификаторы меняют заливку и полоски
- **Разрешить закрытую крышку** — держит Mac полностью активным при закрытой крышке, как от сети, так и от батареи
- **Не гасить экран** — помимо предотвращения сна системы, также не даёт экрану погаснуть
- Локализация UI на основные языки (выбор из трея)
- Настройки сохраняются между запусками (`~/Library/Application Support/AmphetamineNet/settings.json`)
- Встроенный обходной путь для сбоя таймера рендеринга `Avalonia.Native` в macOS 26 (`CVDisplayLinkCreateWithActiveCGDisplays`, ошибка `-6661`) через небольшую interpose-библиотеку dylib

## Как работает «закрытая крышка»

По умолчанию macOS принудительно усыпляет систему при закрытии крышки, если не активен режим
переопределения clamshell (тот же механизм, что используется при подключённом внешнем дисплее).
AmphetamineNet сочетает два механизма, чтобы держать Mac активным с закрытой крышкой — как от
батареи, так и от сети:

1. **IOKit-ассерция clamshell** (`kPMSetClamshellSleepState` на `IOPMrootDomain`) — работает, пока приложение запущено.
2. **`pmset -a disablesleep 1`** — настройка, от которой на самом деле зависит флаг `SleepDisabled` в IOKit. Чтобы выполнять эту команду без запроса пароля каждый раз, требуется одноразовая выдача прав `sudo`.

При первом использовании «Разрешить закрытую крышку» AmphetamineNet **один раз** запрашивает
права администратора через нативный диалог macOS, чтобы установить узко ограниченное правило
`sudoers.d` (`/etc/sudoers.d/amphetamine-net`), которое разрешает только:

```
/usr/bin/pmset -a disablesleep 1
/usr/bin/pmset -a disablesleep 0
```

для текущего пользователя, без пароля. С этого момента сеансы с закрытой крышкой запускаются и
останавливаются без каких-либо дополнительных запросов. Фоновый heartbeat переустанавливает
ассерцию clamshell каждые 30 секунд на случай, если система её сбросит.

## Требования

- **macOS 12 (Monterey) или новее** — приложение не работает на других платформах, и окно Avalonia покажет поясняющее сообщение при запуске в другой ОС
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (для сборки из исходников)
- Xcode Command Line Tools (`clang`) — нужны один раз, для сборки небольшой interpose-библиотеки CVDisplayLink

## Сборка и запуск

```bash
git clone https://github.com/RASLK/AmphetamineNet.git
cd AmphetamineNet
dotnet restore
```

### Запуск из исходников (рекомендуется для разработки)

`run-macos.sh` собирает interpose-библиотеку, если её нет, собирает приложение и запускает его с
переменными окружения, необходимыми для обхода бага таймера рендеринга в macOS 26:

```bash
./run-macos.sh          # Debug-сборка
./run-macos.sh Release  # Release-сборка
```

Логи пишутся в `/tmp/amphetamine-net-run.log` (рантайм) и `/tmp/amphetamine-net.log`
(диагностика приложения).

### Обычный `dotnet run`

```bash
dotnet run -c Release
```

Это тоже работает, но на macOS 26+ приложение один раз перезапускает само себя с установленной
переменной `DYLD_INSERT_LIBRARIES`, указывающей на dylib-фикс CVDisplayLink (собирается
автоматически MSBuild-таргетом `BuildCvDisplayLinkFix` при первой сборке на macOS).

### Локальная публикация (single-file, self-contained)

```bash
dotnet publish AmphetamineNet.csproj -c Release -r osx-arm64 -o publish/osx-arm64   # Apple Silicon
dotnet publish AmphetamineNet.csproj -c Release -r osx-x64   -o publish/osx-x64     # Intel
```

### Локальная сборка DMG

```bash
clang -dynamiclib -o Native/libcvdisplaylink_fix.dylib Native/cvdisplaylink_fix.c \
  -framework CoreVideo -framework CoreGraphics -install_name @rpath/libcvdisplaylink_fix.dylib

dotnet publish AmphetamineNet.csproj -c Release -r osx-arm64 -o publish/osx-arm64 -p:UseAppHost=true

packaging/macos/create-app-dmg.sh \
  publish/osx-arm64/AmphetamineNet 1.0.0 dist/AmphetamineNet-1.0.0-macos-arm64.dmg \
  Assets/tray.png publish/osx-arm64/libcvdisplaylink_fix.dylib
```

### GitHub Actions (CI)

| Workflow | Назначение |
| --- | --- |
| `ci.yml` | Собирает проект при каждом push/PR в `main` (раннер macOS) |
| `macos-pack.yml` | Публикует `osx-arm64` и `osx-x64`, собирает DMG и `.sha256sum` для каждого — запускается вручную или из `release.yml` |
| `release.yml` | При тегировании `vX.Y.Z` → запускает `macos-pack.yml` → публикует GitHub Release с обоими DMG |

```bash
git tag v1.0.0
git push origin v1.0.0
```

**Про macOS:** DMG не подписаны (нет Apple Developer ID). При первом запуске щёлкните приложение
правой кнопкой мыши → **Открыть**, чтобы обойти предупреждение Gatekeeper о неопознанном разработчике.

## Архитектура

```
AmphetamineNet/
  App.axaml(.cs)          # жизненный цикл Avalonia, иконка трея + меню, политика активации окна
  Program.cs               # точка входа, обходной путь релонча для CVDisplayLink в macOS 26
  Models/                  # модели данных
  Services/
    AppSettings.cs          # пользовательские настройки, сохраняемые в JSON
    MacKeepAwakeService.cs  # ассерции IOPM, состояние крышки, оркестрация pmset
    PowerProtect.cs          # одноразовая установка passwordless sudoers для pmset
  Native/
    IoKitNative.cs           # P/Invoke-объявления для IOKit / CoreFoundation
    MacAppActivation.cs      # переключение политики активации NSApplication Accessory ↔ Regular
    cvdisplaylink_fix.c       # исходник interpose-библиотеки для бага таймера рендеринга в macOS 26
  ViewModels/               # MVVM view-модели (CommunityToolkit.Mvvm)
  Views/                    # окна Avalonia
```

## Лицензия

[MIT](./LICENSE) © Ruslan Khairulin
