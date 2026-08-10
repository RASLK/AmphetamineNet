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

一款轻量级的 **macOS** 菜单栏工具，用于防止 Mac 进入睡眠 —— 是
[Amphetamine](https://apps.apple.com/app/amphetamine/id937984704) 的开源替代品，基于
**Avalonia + .NET 10** 构建。

常驻菜单栏（不占用 Dock 图标），可开启固定时长或无限期的防睡眠会话，并可选择在合盖或屏幕熄灭时依然保持 Mac 唤醒。

## 技术栈

| 层 | 技术 |
| --- | --- |
| UI | Avalonia 12, FluentTheme |
| Runtime | .NET 10 (LTS) |
| MVVM | CommunityToolkit.Mvvm（源代码生成器） |
| 电源管理 | IOKit `IOPMAssertion*`（系统/显示器睡眠）+ IOKit 合盖状态 + 通过范围受限、免密码的 `sudoers` 辅助脚本执行 `pmset disablesleep` |
| 原生交互 | 直接调用 `objc_msgSend`（仅菜单栏的「Accessory」激活策略） |
| 打包 | 使用 `hdiutil` 构建的 DMG，GitHub Actions |

## 功能

- 纯菜单栏应用：无 Dock 图标，无应用菜单 —— 一切功能都集中在托盘图标中
- 托盘菜单：**计时器**、**修饰选项**、**语言**，然后是**开始/停止会话**
- 从托盘启动/停止防睡眠会话；选择时长会立即开始会话
- 时长：**无限期**、5 / 15 / 30 分钟、1 / 2 / 5 小时，以及可记住的自定义时间
- 定时会话时，**运行中**旁边显示实时倒计时
- 动态胶囊图标：空闲为黑、有计时器为绿、无限期为红；修饰选项会改变填充和侧边条
- **允许合盖** —— 在接通电源或使用电池时都能让 Mac 在合盖状态下完全保持唤醒
- **保持屏幕唤醒** —— 除了阻止系统睡眠外，还能阻止屏幕熄灭
- 主要语言的界面本地化（可在托盘中选择）
- 设置在重启后依然保留（`~/Library/Application Support/AmphetamineNet/settings.json`）
- 内置针对 macOS 26 中 `Avalonia.Native` 渲染定时器崩溃问题（`CVDisplayLinkCreateWithActiveCGDisplays`，错误码 `-6661`）的规避方案，通过一个小型 interpose dylib 实现

## 「合盖」的工作原理

macOS 通常会在合盖时强制进入睡眠，除非启用了合盖模式（clamshell）覆盖（与连接外接显示器时使用的机制相同）。AmphetamineNet 结合了两种机制，在使用电池或接通电源时都能让 Mac 在合盖状态下保持唤醒：

1. **IOKit 合盖断言**（`IOPMrootDomain` 上的 `kPMSetClamshellSleepState`）—— 仅在应用运行期间有效。
2. **`pmset -a disablesleep 1`** —— IOKit 的 `SleepDisabled` 标志实际依赖的设置。要让此命令每次执行都无需输入密码，需要一次性授予 `sudo` 权限。

首次使用「允许合盖」功能时，AmphetamineNet 会通过原生 macOS 弹窗**仅请求一次**管理员凭据，用于安装一条范围极窄的 `sudoers.d` 规则（`/etc/sudoers.d/amphetamine-net`），该规则仅允许：

```
/usr/bin/pmset -a disablesleep 1
/usr/bin/pmset -a disablesleep 0
```

针对当前用户免密执行。此后，合盖会话的开始与停止都不会再弹出任何提示。后台心跳进程每 30 秒重新应用一次合盖断言，以防系统将其重置。

## 系统要求

- **macOS 12 (Monterey) 或更高版本** —— 本应用不支持其他平台，若在其他系统上启动，Avalonia 窗口会显示说明性提示
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)（用于从源码构建）
- Xcode Command Line Tools（`clang`）—— 仅需一次，用于构建小型 CVDisplayLink interpose dylib

## 构建与运行

```bash
git clone https://github.com/RASLK/AmphetamineNet.git
cd AmphetamineNet
dotnet restore
```

### 从源码运行（推荐用于开发）

`run-macos.sh` 会在缺失时构建 interpose dylib，构建应用，并使用 macOS 26 渲染定时器规避方案所需的环境变量运行它：

```bash
./run-macos.sh          # Debug 构建
./run-macos.sh Release  # Release 构建
```

日志会写入 `/tmp/amphetamine-net-run.log`（运行时日志）和 `/tmp/amphetamine-net.log`（应用内诊断日志）。

### 直接使用 `dotnet run`

```bash
dotnet run -c Release
```

这种方式同样可行，但在 macOS 26+ 上，应用会自动重启一次，并设置
`DYLD_INSERT_LIBRARIES` 环境变量指向 CVDisplayLink 修复 dylib（首次在 macOS 上构建时，由
MSBuild 目标 `BuildCvDisplayLinkFix` 自动构建）。

### 本地发布（单文件、自包含）

```bash
dotnet publish AmphetamineNet.csproj -c Release -r osx-arm64 -o publish/osx-arm64   # Apple Silicon
dotnet publish AmphetamineNet.csproj -c Release -r osx-x64   -o publish/osx-x64     # Intel
```

### 本地构建 DMG

```bash
clang -dynamiclib -o Native/libcvdisplaylink_fix.dylib Native/cvdisplaylink_fix.c \
  -framework CoreVideo -framework CoreGraphics -install_name @rpath/libcvdisplaylink_fix.dylib

dotnet publish AmphetamineNet.csproj -c Release -r osx-arm64 -o publish/osx-arm64 -p:UseAppHost=true

packaging/macos/create-app-dmg.sh \
  publish/osx-arm64/AmphetamineNet 1.0.0 dist/AmphetamineNet-1.0.0-macos-arm64.dmg \
  Assets/tray.png publish/osx-arm64/libcvdisplaylink_fix.dylib
```

### GitHub Actions（CI）

| Workflow | 作用 |
| --- | --- |
| `ci.yml` | 在每次向 `main` 推送/发起 PR 时构建项目（macOS runner） |
| `macos-pack.yml` | 发布 `osx-arm64` 与 `osx-x64`，为每个平台构建 DMG 及 `.sha256sum` —— 可手动运行，也可从 `release.yml` 触发 |
| `release.yml` | 在打上 `vX.Y.Z` 标签时 → 运行 `macos-pack.yml` → 发布包含两个 DMG 的 GitHub Release |

```bash
git tag v1.0.0
git push origin v1.0.0
```

**macOS 提示：** DMG 未经签名（没有 Apple Developer ID）。首次启动时，请右键点击应用 →
选择**打开**，以绕过 Gatekeeper 关于未知开发者的警告。

## 架构

```
AmphetamineNet/
  App.axaml(.cs)          # Avalonia 生命周期、托盘图标 + 菜单、窗口激活策略
  Program.cs               # 入口点，macOS 26 CVDisplayLink 重启规避方案
  Models/                  # 数据模型
  Services/
    AppSettings.cs          # 以 JSON 持久化的用户设置
    MacKeepAwakeService.cs  # IOPM 断言、合盖状态、pmset 编排
    PowerProtect.cs          # 为 pmset 一次性安装免密码 sudoers 规则
  Native/
    IoKitNative.cs           # IOKit / CoreFoundation 的 P/Invoke 声明
    MacAppActivation.cs      # NSApplication 激活策略 Accessory ↔ Regular 之间的切换
    cvdisplaylink_fix.c       # 针对 macOS 26 渲染定时器崩溃问题的 interpose dylib 源码
  ViewModels/               # MVVM 视图模型（CommunityToolkit.Mvvm）
  Views/                    # Avalonia 窗口
```

## 许可证

[MIT](./LICENSE) © Ruslan Khairulin
