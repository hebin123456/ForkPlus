# ForkPlus

Rust で基盤エンジンを書き直した高性能な Git GUI クライアント。AI 支援開発、8 言語、12 種類のテーマスキン、git mm ワークフロー、コントリビューションヒートマップやリポジトリツリーマップなどの可視化機能を内蔵しています。

[English](README.en.md) | [简体中文](README.md) | [繁體中文](README.zh-Hant.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Español](README.es.md)

## 主な特徴

- **多言語サポート**: 英語、簡体字中国語、繁体字中国語、日本語を内蔵し、JSON ファイルによる追加言語の拡張も可能
- **マルチテーマスキン**: 12 種類の内蔵スキン（Light/Dark、Solarized、GitHub、Dracula、Monokai、紫/緑のライト&ダーク）に加え、ユーザーカスタムカラーの上書きが即時反映
- **git mm ワークフロー**: `git mm` サブコマンドを内蔵し、リーンブランチング（Lean Branching）ワークフローを提供
- **AI 支援開発**: AI コードレビュー、コミットメッセージ自動生成、AI 支援によるコード変更を統合
- **パフォーマンス最適化**: 大規模リポジトリのリフレッシュ、diff レンダリング、サブモジュール管理に対する専用最適化
- **コード統計**: tokei（Rust、200+ 言語対応）を統合し、言語別のコード行数・ファイル数・コメント行・空白行を集計、円グラフで可視化、Workspace/ブランチ/tag の ref 切替に対応

## プロジェクト構成

```
ForkPlus/
├── src/
│   ├── ForkPlus/              # メイン WPF アプリケーションソース、XAML、アセット
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
│   ├── ForkPlus.Tests/        # xUnit 単体テスト
│   └── ForkPlus.AutomationTests/  # FlaUI UI スモークテスト
├── third_party/               # アプリと共に配布されるランタイムツールとネイティブバイナリ
├── gitmm/                     # git mm ワークフローリファレンスドキュメント
└── .github/workflows/         # GitHub Actions CI 設定
```

## ビルド

### 前提条件

- Windows 10 以降
- Visual Studio 2022 17.13+、または .NET 10 SDK
- .NET 10 SDK（Windows Desktop runtime 含む）
- Git 2.31 以上（2.40+ 推奨、これ未満の場合は起動時に警告が表示され、一部機能が正常に動作しない可能性があります）
- git-mm 3.0 以上（git mm ワークフロー使用時に必須、未対応バージョンや未インストール時は起動時に警告、環境設定で git-mm.exe パスを構成可能）

### ビルド手順

- Visual Studio 2022 17.13+ で `ForkPlus.sln` を開き、Release 構成を選択してビルド
- またはコマンドラインから: `dotnet build ForkPlus.sln -c Release`

### 継続的インテグレーション

プロジェクトには GitHub Actions（[`.github/workflows/build.yml`](.github/workflows/build.yml)）が設定されています。`v*` で始まる tag をプッシュすると Windows 環境で自動ビルドされ、完全なランタイム zip パッケージが GitHub Release に公開されます。

```bash
git tag v1.3.0
git push origin v1.3.0
```

ビルド成果物には `ForkPlus.exe`、すべての依存 DLL、`biturbo.dll`、言語ファイルなどが含まれ、解凍するだけで実行できます。

## テスト

- 単体テスト: `dotnet test src/ForkPlus.Tests/ForkPlus.Tests.csproj`
- UI スモークテスト: `FORKPLUS_AUTOMATION_EXE` 環境変数をビルド済みの `ForkPlus.exe` に設定し、`dotnet test src/ForkPlus.AutomationTests/ForkPlus.AutomationTests.csproj` を実行

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

最新版は [Releases ページ](https://github.com/hebin123456/ForkPlus/releases) からダウンロードしてください。

各バージョンの変更内容は [Release Notes](RELEASE_NOTE.md) を参照してください。

## 開発規約

アプリケーション自体を変更する場合は、`third_party` 配下のランタイムファイルを意図的に更新する場合を除き、`src/ForkPlus` ディレクトリ内に留めてください。
