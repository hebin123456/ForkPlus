# ForkPlus

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Build](https://github.com/hebin123456/ForkPlus/actions/workflows/build.yml/badge.svg)](https://github.com/hebin123456/ForkPlus/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/hebin123456/ForkPlus)](https://github.com/hebin123456/ForkPlus/releases)

クロスプラットフォーム Git クライアント：UI 層は .NET 10 + Avalonia 12 で構築され、単一コードベースで Windows / Linux / macOS 上で動作します。基盤の Rust エンジン（biturbo native）、AI 支援開発、8 言語、12 種類のテーマスキン、git mm ワークフロー、コントリビューションヒートマップやリポジトリツリーマップなどの可視化機能を利用できます。

> ビルド環境とコンパイル手順は [BUILDING.md](BUILDING.md) を参照してください。

[English](README.en.md) | [简体中文](README.md) | [繁體中文](README.zh-Hant.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Español](README.es.md)

## 主な特徴

- **クロスプラットフォーム**: Avalonia 12 ベースのクロスプラットフォーム UI 層。CI では Windows x64 / Linux x64 / macOS arm64 の 3 プラットフォーム分を並行ビルドし、自己完結型（self-contained、.NET ランタイムのインストール不要）で公開
- **多言語サポート**: 英語、簡体字中国語、繁体字中国語、日本語、한국어、Français、Deutsch、Español の 8 言語を内蔵し、JSON ファイルによる追加言語の拡張も可能
- **マルチテーマスキン**: 12 種類の内蔵スキン（Light/Dark、Solarized、GitHub、Dracula、Monokai、紫/緑のライト&ダーク）に加え、ユーザーカスタムカラーの上書きが即時反映
- **git mm ワークフロー**: `git mm` サブコマンドを内蔵し、リーンブランチング（Lean Branching）ワークフローで複数サブリポジトリの変更と同期を一元管理
- **AI 支援開発**: AI コードレビュー、コミットメッセージ自動生成、AI 支援によるコード変更を統合
- **コントリビューションヒートマップ**: GitHub 風の 53 週 × 7 日コミットヒートマップ。カラースケール凡例と統計サマリー（総コミット数 / 最長連続コミット日数 / 最もアクティブな日）を表示し、ホバーでその日のコミット数と Top 3 作成者を表示
- **リポジトリツリーマップ**: biturbo native の treemap アルゴリズムによるリポジトリのファイルサイズ可視化。クリックでドリルダウン可能
- **リモートブランチ追跡**: 右クリックの「追跡」をリモート別にグループ化した 2 段階メニューに変更。メニュー内に固定検索ボックスを内蔵し、多数のリモートブランチから高速に検索
- **パフォーマンス最適化**: 大規模リポジトリのリフレッシュ、diff レンダリング、サブモジュール管理に対する専用最適化
- **コード統計**: tokei（Rust、200+ 言語対応）を統合し、言語別のコード行数・ファイル数・コメント行・空白行を集計、円グラフで可視化、Workspace/ブランチ/tag の ref 切替に対応

## プロジェクト構成

```
ForkPlus/
├── src/
│   ├── ForkPlus/              # メインアプリケーションソース（Avalonia 12 クロスプラットフォーム UI）、XAML、アセット
│   │   ├── Biturbo/           # biturbo native ライブラリの P/Invoke バインディング
│   │   ├── Languages/         # 多言語翻訳ファイル（JSON）
│   │   │   ├── zh-Hans.json   # 簡体字中国語
│   │   │   ├── zh-Hant.json   # 繁体字中国語
│   │   │   ├── ja-JP.json     # 日本語
│   │   │   ├── ko-KR.json     # 한국어
│   │   │   ├── fr-FR.json     # フランス語
│   │   │   ├── de-DE.json     # ドイツ語
│   │   │   ├── es-ES.json     # スペイン語
│   │   │   └── README.md      # 言語ファイル形式の説明
│   │   └── ...
│   ├── ForkPlus.AskPass/      # Git/SSH パスワード入力ヘルパー
│   ├── ForkPlus.RI/           # インタラクティブ rebase エディタヘルパー
│   ├── ForkPlus.Tests/        # xUnit 単体テスト（Avalonia.Headless UI スモークテストを含む）
│   ├── ForkPlus.AskPass.Tests/# AskPass ヘルパーの単体テスト
│   └── ForkPlus.RI.Tests/     # RI ヘルパーの単体テスト
├── third_party/               # ビルド時に取得するネイティブバイナリ（下記「biturbo native ライブラリの入手元」参照）
├── gitmm/                     # git mm ワークフローリファレンスドキュメント
└── .github/workflows/         # GitHub Actions CI 設定
```

## ビルド

### 前提条件

- Windows 10 以降 / Linux / macOS（クロスプラットフォーム対応）
- .NET 10 SDK
- IDE（任意）：Visual Studio 2026（Windows。リポジトリ直下の `OpenForkPlusInVS2026.cmd` でワンクリックでソリューションを開けます）、Rider、または VS Code
- Git 2.40 以上（推奨。推奨未満の場合は起動時に警告が表示され、一部機能が正常に動作しない可能性があります。アプリは内蔵 git 2.50.1 を優先使用し、欠損時はシステム git にフォールバックします）
- git-mm 3.0 以上（git mm ワークフロー使用時に必須。未対応バージョンや未インストール時は起動時に警告、環境設定で git-mm パスを構成可能）

### ビルド手順

```bash
# ビルド（リポジトリ直下で実行）。サードパーティ成果物（OxyPlot.Avalonia、biturbo、tokei）は
# ビルド時に各リポジトリの最新 Release から自動取得されます（下記の各「入手元」節を参照）。
# GitHub へのネットワークアクセスのみが必要です：
dotnet build ForkPlus.sln -c Release
```

Visual Studio 2026 でリポジトリ直下の `ForkPlus.sln` を直接開いてビルドすることもできます。

### biturbo native ライブラリの入手元

biturbo native（Rust 製）はリポジトリツリーマップのレイアウト、コミットグラフのキャッシュ、revision header の解析などの機能を提供します。**このバイナリは本リポジトリにはコミットされず**、ビルド時に [Biturbo リポジトリ](https://github.com/hebin123456/Biturbo)の最新 Release から自動取得されます。プラットフォームごとのファイル：

| プラットフォーム | ファイル |
|------|------|
| Windows x64 | `third_party/biturbo.dll` |
| Linux x64 | `third_party/libbiturbo.so` |
| macOS arm64 | `third_party/libbiturbo.dylib` |

具体的な仕組み（[ForkPlus.csproj](src/ForkPlus/ForkPlus.csproj) 参照）：

- `RestoreBiturbo` ターゲット（`BeforeTargets=Build`）：現在のプラットフォームのネイティブライブラリが欠損している場合に自動ダウンロード（Windows は PowerShell、Linux/macOS は bash + curl で `uname -s` により `.so` / `.dylib` を選択、いずれもリトライ付き）
- `CopyHelperExecutables` / `PublishHelperExecutables` ターゲット（`AfterTargets=Build` / `Publish`）：ネイティブライブラリと AskPass/RI サブプロセスの成果物を Build / Publish 出力ディレクトリへコピー
- `.gitignore` により `third_party/` 配下のこれらのファイルは除外済み

そのため初回ビルドには GitHub へのネットワークアクセスが必要です。CI では workflow が明示的にダウンロードして検証し（空でなく 1MB 超）、csproj の `RestoreBiturbo` がフォールバックとして機能します。

### tokei の入手元

[tokei](https://github.com/XAMPPRocky/tokei)（MIT ライセンス）は統計パネルの「コード行数」機能に使用されます。ビルド時に [hebin123456/tokei](https://github.com/hebin123456/tokei) リポジトリの最新 Release から**プリコンパイル済みバイナリ**を取得するため、ローカルの Rust ツールチェーンは不要です：

- Windows x64 → 単体 exe、`third_party/tokei.exe` として保存
- Linux x64 / macOS → tar.gz（単体の `tokei` バイナリを含む）、解凍して `third_party/tokei` として保存
- macOS 用アセットは x86_64 で、Apple Silicon では Rosetta 2 経由で実行

仕組みは biturbo と同じ：`RestoreTokei` ターゲット（`BeforeTargets=Build`）が自動取得し、CI が明示的にダウンロードして検証、`.gitignore` が成果物を除外します。

### OxyPlot.Avalonia の入手元

チャートライブラリ [OxyPlot.Avalonia](https://github.com/oxyplot/oxyplot-avalonia)（MIT ライセンス）は統計パネルの描画コントロールに使用されます。**公式リポジトリは Avalonia 11 で止まっておりバイナリを公開していない**ため、本リポジトリは [hebin123456/oxyplot-avalonia](https://github.com/hebin123456/oxyplot-avalonia) フォークが発行する**プリビルド NuGet パッケージ**を利用します（Avalonia 12.1.1 対応のネイティブコンパイル、net8.0 / net10.0 デュアルターゲット）：

- パッケージバージョンには `-avalonia12.x` プレリリースサフィックスが付く（例：`2.1.2-avalonia12.1`）ため、nuget.org の公式 `2.1.2`（Avalonia 11 向け）と混同されることはなく、復元は必ずフォークのパッケージだけにヒットします
- ビルド時にフォークリポジトリの最新 Release から `OxyPlot.Avalonia.<バージョン>.nupkg` を `third_party/nuget/` へダウンロードし（リポジトリ直下の `nuget.config` に登録されたローカルディレクトリソース）、`PackageReference` で復元されます
- 取得メカニズム：ForkPlus.csproj の `RestoreOxyPlotAvalonia` ターゲット（`BeforeTargets=Restore;Build`、ローカル開発用）+ CI の明示的ダウンロードステップ（build.yml、macOS ランナーでの MSBuild Exec の差異を回避）、`.gitignore` が成果物を除外
- アップグレード = フォークリポジトリで `AvaloniaVersion` を変更し `v*` タグを打って新規 Release を公開、本リポジトリ側は `ForkPlus.csproj` の `OxyPlotAvaloniaPackageVersion` の 1 点を変更するだけです

### 継続的インテグレーション

プロジェクトには GitHub Actions（[`.github/workflows/build.yml`](.github/workflows/build.yml)）が設定されています。`v*` タグの push、または手動トリガー時に、3 プラットフォームで並列ビルドして成果物をアップロードし、Linux ランナー上で単体 + E2E テストスイート全量を実行します。すべて合格すると Release（3 プラットフォームの zip 添付）を自動公開します：

| マトリクス | Runner | RID |
|------|--------|-----|
| windows-x64 | windows-latest | win-x64 |
| linux-x64 | ubuntu-latest | linux-x64 |
| macos-arm64 | macos-latest | osx-arm64 |

成果物は**自己完結型（self-contained）パブリッシュ**（.NET 10 ランタイムを同梱、ターゲットマシンへのインストールは一切不要）で、メインアプリ、AskPass/RI ヘルパー（これらも自己完結型のため、ランタイムのない環境でも git 認証情報入力とインタラクティブ rebase のフローが動作します）、各プラットフォームの biturbo native ライブラリ、tokei、言語ファイルを含みます（linux-x64 で約 125MB）。リリース版の zip 添付は [Releases ページ](https://github.com/hebin123456/ForkPlus/releases)から、タグなしの手動ビルド成果物はリポジトリの [Actions](https://github.com/hebin123456/ForkPlus/actions) ページの該当実行からダウンロードできます（Artifacts、14 日間保持）。

## テスト

- 単体テスト: `dotnet test src/ForkPlus.Tests/ForkPlus.Tests.csproj`（クロスプラットフォーム対応の Avalonia.Headless UI スモーク・E2E テストを含み、単体テストと一緒に実行されます）
- 全 4400+ ケース。主要な修正にはすべて回帰防止テストが付いています（失敗すればテストが赤になります）
- CI ではタグビルドと手動トリガー時に ubuntu ランナーでフルテストスイート（AskPass / RI ヘルパーのテストを含む）を実行します。gitflow-avh と git-lfs に依存します（workflow のコメントを参照）

## 多言語サポート

### 内蔵言語

| 言語コード | 表示名 | 状態 |
|-----------|--------|------|
| `en` | English | ソース言語 |
| `zh-Hans` | 简体中文 | 完全 |
| `zh-Hant` | 繁體中文 | 完全 |
| `ja-JP` | 日本語 | 完全 |
| `ko-KR` | 한국어 | 完全 |
| `fr-FR` | Français | 完全 |
| `de-DE` | Deutsch | 完全 |
| `es-ES` | Español | 完全 |

### 新しい言語の追加

`src/ForkPlus/Languages/` ディレクトリに `<言語コード>.json` ファイルを新規作成するだけで、コード変更なしで追加できます。ファイル形式:

```json
{
  "code": "ko",
  "name": "한국어",
  "translations": {
    "Preferences": "환경설정",
    "General": "일반",
    "Commit": "커밋"
  }
}
```

詳細は [src/ForkPlus/Languages/README.md](src/ForkPlus/Languages/README.md) を参照してください。

### 国際化 API

コードベースでは以下の API を使用して国際化を実現しています:

- `PreferencesLocalization.Current("English text")` — 単純な文字列の翻訳
- `PreferencesLocalization.FormatCurrent("...{0}...", args)` — パラメータ付き文字列の翻訳
- `PreferencesLocalization.Translate(text, language)` — 特定の言語の翻訳

## ダウンロード

- CI ビルド成果物（タグなしの手動実行）: [Actions](https://github.com/hebin123456/ForkPlus/actions) ページ → 該当する build 実行 → Artifacts（自己完結型、.NET 10 ランタイム同梱、インストール不要）
- 正式リリース: [Releases ページ](https://github.com/hebin123456/ForkPlus/releases)（自己完結型、.NET 10 ランタイム同梱、インストール不要、3 プラットフォームの zip）
- 各バージョンの変更内容は [Release Notes](RELEASE_NOTE.md) を参照してください（元 WPF 版の履歴を含む）

## 開発規約

- アプリケーション自体を変更する場合は `src/ForkPlus` ディレクトリ内にとどまってください。`third_party/` 配下のランタイムバイナリ（biturbo native ライブラリ、tokei）はビルド時に自動取得されるため、バイナリを手動でコミットしないでください
- biturbo / tokei をアップグレードする場合は、該当リポジトリで新しい Release を公開するだけで、本リポジトリの次回ビルドが自動的に取得します
- OxyPlot.Avalonia は [hebin123456/oxyplot-avalonia](https://github.com/hebin123456/oxyplot-avalonia) フォークのプリビルド nupkg として利用されます（Avalonia 12.1.1 対応でコンパイル済み。公式パッケージは Avalonia 11 で止まっており、その XAML IL は 12 のランタイムで MissingMethodException をスローします）。ビルド時にこのフォークの最新 Release から自動取得されます

## ライセンス

本プロジェクトは [MIT License](LICENSE) の下でオープンソースです。

Copyright (c) 2026 hebin123456
