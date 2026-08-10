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

Mac をスリープさせないための軽量な **macOS** メニューバーユーティリティ ——
[Amphetamine](https://apps.apple.com/app/amphetamine/id937984704) のオープンソース版代替アプリで、
**Avalonia + .NET 10** で構築されています。

メニューバーに常駐し（Dock アイコンなし）、決まった時間または無期限でセッションを開始でき、
オプションで蓋を閉じた状態やディスプレイがスリープした状態でも Mac を起こしたままにできます。

## スタック

| 層 | 技術 |
| --- | --- |
| UI | Avalonia 12, FluentTheme |
| Runtime | .NET 10 (LTS) |
| MVVM | CommunityToolkit.Mvvm（ソースジェネレーター） |
| 電源管理 | IOKit `IOPMAssertion*`（システム/ディスプレイのスリープ制御）+ IOKit のクラムシェル状態 + スコープを限定したパスワード不要の `sudoers` ヘルパー経由の `pmset disablesleep` |
| ネイティブ連携 | `objc_msgSend` の直接呼び出し（メニューバー専用の「Accessory」アクティベーションポリシー） |
| パッケージング | `hdiutil` でビルドした DMG、GitHub Actions |

## 機能

- メニューバー専用アプリ：Dock アイコンなし、アプリケーションメニューなし —— すべてトレイアイコンに集約
- トレイメニュー：**タイマー**、**修飾機能**、**言語**、その後 **セッション開始/停止**
- トレイからキープアウェイクセッションを開始／停止；時間を選ぶとすぐにセッション開始
- 時間：**無期限**、5 / 15 / 30 分、1 / 2 / 5 時間、および記憶されるカスタム時間
- タイマー付きセッションでは **動作中** の横にリアルタイムのカウントダウン
- 動的なカプセルアイコン：停止中は黒、タイマーありは緑、無期限は赤；修飾機能で塗りつぶしとサイドバーが変化
- **蓋を閉じても許可** —— AC 電源でもバッテリー駆動でも、蓋を閉じた状態で Mac を完全に起こしたままにする
- **ディスプレイをスリープさせない** —— システムスリープの防止に加え、ディスプレイのスリープも防ぐ
- 主要言語の UI ローカライズ（トレイから選択）
- 設定は起動をまたいで保持される（`~/Library/Application Support/AmphetamineNet/settings.json`）
- macOS 26 の `Avalonia.Native` レンダータイマークラッシュ（`CVDisplayLinkCreateWithActiveCGDisplays`、エラー `-6661`）に対する回避策を、小さなインターポーズ dylib として内蔵

## 「蓋を閉じる」の仕組み

macOS は通常、クラムシェルモードのオーバーライドが有効でない限り（外部ディスプレイ接続時と同じ
仕組み）、蓋を閉じると強制的にスリープします。AmphetamineNet は、バッテリー駆動でも AC 電源
でも蓋を閉じた状態で Mac を起こしたままにするため、2 つの仕組みを組み合わせています。

1. **IOKit クラムシェルアサーション**（`IOPMrootDomain` の `kPMSetClamshellSleepState`）—— アプリが実行中の間だけ有効。
2. **`pmset -a disablesleep 1`** —— IOKit の `SleepDisabled` フラグが実際に依存している設定。このコマンドを毎回パスワードなしで実行するには、一度だけ `sudo` 権限の付与が必要です。

「蓋を閉じても許可」を初めて使用する際、AmphetamineNet はネイティブの macOS ダイアログを通じて
**一度だけ** 管理者権限を要求し、次のコマンドのみを許可する、範囲を厳しく限定した `sudoers.d`
ルール（`/etc/sudoers.d/amphetamine-net`）をインストールします。

```
/usr/bin/pmset -a disablesleep 1
/usr/bin/pmset -a disablesleep 0
```

現在のユーザーに対してパスワードなしで許可します。それ以降、蓋を閉じたセッションの開始・停止は
それ以上のプロンプトなしで行われます。バックグラウンドのハートビートが 30 秒ごとにクラムシェル
アサーションを再適用し、OS がそれをリセットした場合に備えます。

## 動作環境

- **macOS 12 (Monterey) 以降** —— このアプリは他のプラットフォームでは動作せず、他の環境で起動すると Avalonia ウィンドウに説明メッセージが表示されます
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)（ソースからビルドする場合）
- Xcode Command Line Tools（`clang`）—— 小さな CVDisplayLink インターポーズ dylib をビルドするために一度だけ必要

## ビルドと実行

```bash
git clone https://github.com/RASLK/AmphetamineNet.git
cd AmphetamineNet
dotnet restore
```

### ソースから実行（開発時に推奨）

`run-macos.sh` は、インターポーズ dylib が存在しなければビルドし、アプリをビルドしたうえで、
macOS 26 のレンダータイマー回避策に必要な環境変数を設定して起動します。

```bash
./run-macos.sh          # Debug ビルド
./run-macos.sh Release  # Release ビルド
```

ログは `/tmp/amphetamine-net-run.log`（ランタイムログ）と `/tmp/amphetamine-net.log`
（アプリ内診断ログ）に書き込まれます。

### 通常の `dotnet run`

```bash
dotnet run -c Release
```

これでも動作しますが、macOS 26 以降ではアプリが一度自分自身を再起動し、その際
`DYLD_INSERT_LIBRARIES` に CVDisplayLink 修正用 dylib を設定します（macOS で初めてビルドした際に
MSBuild ターゲット `BuildCvDisplayLinkFix` が自動的にビルドします）。

### ローカルでの発行（単一ファイル、自己完結型）

```bash
dotnet publish AmphetamineNet.csproj -c Release -r osx-arm64 -o publish/osx-arm64   # Apple Silicon
dotnet publish AmphetamineNet.csproj -c Release -r osx-x64   -o publish/osx-x64     # Intel
```

### DMG をローカルでビルド

```bash
clang -dynamiclib -o Native/libcvdisplaylink_fix.dylib Native/cvdisplaylink_fix.c \
  -framework CoreVideo -framework CoreGraphics -install_name @rpath/libcvdisplaylink_fix.dylib

dotnet publish AmphetamineNet.csproj -c Release -r osx-arm64 -o publish/osx-arm64 -p:UseAppHost=true

packaging/macos/create-app-dmg.sh \
  publish/osx-arm64/AmphetamineNet 1.0.0 dist/AmphetamineNet-1.0.0-macos-arm64.dmg \
  Assets/tray.png publish/osx-arm64/libcvdisplaylink_fix.dylib
```

### GitHub Actions（CI）

| Workflow | 目的 |
| --- | --- |
| `ci.yml` | `main` への push/PR のたびにプロジェクトをビルド（macOS ランナー） |
| `macos-pack.yml` | `osx-arm64` と `osx-x64` を発行し、それぞれ DMG と `.sha256sum` をビルド —— 手動実行、または `release.yml` から実行可能 |
| `release.yml` | `vX.Y.Z` タグの push で → `macos-pack.yml` を実行 → 両方の DMG を含む GitHub Release を公開 |

```bash
git tag v1.0.0
git push origin v1.0.0
```

**macOS に関する注意：** DMG は未署名です（Apple Developer ID なし）。初回起動時は、アプリを
右クリック → **開く** を選び、Gatekeeper の「不明な開発元」警告を回避してください。

## アーキテクチャ

```
AmphetamineNet/
  App.axaml(.cs)          # Avalonia のライフサイクル、トレイアイコン + メニュー、ウィンドウのアクティベーションポリシー
  Program.cs               # エントリーポイント、macOS 26 の CVDisplayLink 再起動回避策
  Models/                  # データモデル
  Services/
    AppSettings.cs          # JSON で永続化されるユーザー設定
    MacKeepAwakeService.cs  # IOPM アサーション、蓋の状態、pmset のオーケストレーション
    PowerProtect.cs          # pmset 用のパスワード不要 sudoers の一度きりのインストール
  Native/
    IoKitNative.cs           # IOKit / CoreFoundation の P/Invoke 宣言
    MacAppActivation.cs      # NSApplication のアクティベーションポリシー Accessory ↔ Regular の切り替え
    cvdisplaylink_fix.c       # macOS 26 のレンダータイマークラッシュに対するインターポーズ dylib のソース
  ViewModels/               # MVVM のビューモデル（CommunityToolkit.Mvvm）
  Views/                    # Avalonia のウィンドウ
```

## ライセンス

[MIT](./LICENSE) © Ruslan Khairulin
