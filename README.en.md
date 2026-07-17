# ForkPlus

A high-performance Git GUI client with a Rust-rewritten underlying engine, featuring AI-assisted development, 8 languages, 12 theme skins, git mm workflow, and visualizations like contribution heatmaps and repository treemaps.

[English](README.en.md) | [简体中文](README.md) | [繁體中文](README.zh-Hant.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Español](README.es.md)

## Key Features

- **Multi-language support**: Built-in English, Simplified Chinese, Traditional Chinese, and Japanese, with JSON-based extensibility for more languages
- **Multiple themes**: 12 built-in skins (Light/Dark, Solarized, GitHub, Dracula, Monokai, Purple/Green light & dark) plus user-customizable color overrides applied instantly
- **git mm workflow**: Bundled `git mm` subcommand providing Lean Branching workflows
- **AI-assisted development**: Integrated AI code review, automatic commit message generation, and AI-assisted code modification
- **Performance optimizations**: Targeted improvements for large repository refresh, diff rendering, and submodule management
- **Bug fixes**: Resolved multiple issues from the original Fork (empty modifications, AI queue generation failures, etc.)

## Repository Layout

```
ForkPlus/
├── src/
│   ├── ForkPlus/              # Main WPF application source, XAML, assets
│   │   ├── Languages/         # Localization translation files (JSON)
│   │   │   ├── zh-Hans.json   # Simplified Chinese
│   │   │   ├── zh-Hant.json   # Traditional Chinese
│   │   │   ├── ja-JP.json     # Japanese
│   │   │   ├── ko-KR.json     # Korean
│   │   │   ├── fr-FR.json     # French
│   │   │   ├── de-DE.json     # German
│   │   │   ├── es-ES.json     # Spanish
│   │   │   └── README.md      # Language file format description
│   │   └── ...
│   ├── ForkPlus.AskPass/      # Git/SSH askpass helper
│   ├── ForkPlus.RI/           # Interactive rebase editor helper
│   ├── ForkPlus.Tests/        # xUnit unit tests
│   └── ForkPlus.AutomationTests/  # FlaUI UI smoke tests
├── third_party/               # Runtime tools and native binaries
├── gitmm/                     # git mm workflow reference docs
└── .github/workflows/         # GitHub Actions CI config
```

## Build

### Prerequisites

- Windows 10 or later
- Visual Studio 2019/2022, or .NET SDK + MSBuild
- .NET Framework 4.7.2 targeting pack
- Git 2.31 or later (2.40+ recommended; older versions trigger a warning on startup and some features may not work)
- git-mm 3.0 or later (required for git mm workflow; a warning is shown on startup if older or missing; configure git-mm.exe path in Preferences)

### Build Steps

- Open `ForkPlus.sln` in Visual Studio, select Release configuration and build
- Or run from command line: `msbuild ForkPlus.sln /p:Configuration=Release`

### Continuous Integration

The project is configured with GitHub Actions ([`.github/workflows/build-windows.yml`](.github/workflows/build-windows.yml)). Pushing a `v*` tag automatically builds on Windows and publishes a complete runtime zip to GitHub Release.

```bash
git tag v1.3.0
git push origin v1.3.0
```

The build artifact includes `ForkPlus.exe`, all dependency DLLs, `biturbo.dll`, language files, and more—just unzip and run.

## Tests

- Unit tests: `dotnet test src/ForkPlus.Tests/ForkPlus.Tests.csproj`
- UI smoke tests: set `FORKPLUS_AUTOMATION_EXE` environment variable to a built `ForkPlus.exe`, then run `dotnet test src/ForkPlus.AutomationTests/ForkPlus.AutomationTests.csproj`

## Multi-language Support

### Built-in Languages

| Language Code | Display Name | Status |
|---------------|-------------|--------|
| `en` | English | Source language |
| `zh-Hans` | 简体中文 | Complete |
| `zh-Hant` | 繁體中文 | Complete |
| `ja-JP` | 日本語 | Complete |
| `ko-KR` | 한국어 | Complete |
| `fr-FR` | Français | Complete |
| `de-DE` | Deutsch | Complete |
| `es-ES` | Español | Complete |

### Adding a New Language

Create a new `<language-code>.json` file in the `src/ForkPlus/Languages/` directory—no code changes required. File format:

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

See [src/ForkPlus/Languages/README.md](src/ForkPlus/Languages/README.md) for details.

### Internationalization API

The codebase uses the following APIs for internationalization:

- `PreferencesLocalization.Current("English text")` — Simple string translation
- `PreferencesLocalization.FormatCurrent("...{0}...", args)` — Parameterized string translation
- `PreferencesLocalization.Translate(text, language)` — Translation for a specific language

## Download

For the latest release, visit the [Releases page](https://github.com/hebin123456/ForkPlus/releases).

For changes in each version, see the [Release Notes](RELEASE_NOTE.md).

## Development Convention

When modifying the application itself, stay within `src/ForkPlus` unless intentionally updating runtime files under `third_party`.
