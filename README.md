# ForkPlus

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Build](https://github.com/hebin123456/ForkPlus/actions/workflows/build.yml/badge.svg)](https://github.com/hebin123456/ForkPlus/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/hebin123456/ForkPlus)](https://github.com/hebin123456/ForkPlus/releases)

一款使用 Rust 重写底层引擎的高性能 Git 图形化客户端，内置 AI 辅助开发、8 种语言、12 套主题皮肤、git mm 工作流，以及贡献热力图、仓库树图等可视化能力。

[English](README.en.md) | [简体中文](README.md) | [繁體中文](README.zh-Hant.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Español](README.es.md)

## 主要特性

- **多语言支持**：内置英语、简体中文、繁體中文、日本語、한국어、Français、Deutsch、Español 8 种语言，并支持通过 JSON 文件扩展更多语言
- **多主题皮肤**：内置 12 套预设皮肤（Light/Dark、Solarized、GitHub、Dracula、Monokai、紫色/绿色浅色深色），并支持用户自定义颜色覆盖，即时生效
- **git mm 工作流**：内置 `git mm` 子命令，提供精益分支（Lean Branching）工作流，统一管理多子仓的变更与同步
- **AI 辅助开发**：集成 AI 代码审查、自动生成提交信息、AI 辅助修改代码
- **贡献热力图**：GitHub 风格 53 周 × 7 天提交热力图，附带色阶图例和统计摘要（总提交数 / 最长连续提交天数 / 最活跃日期），鼠标悬停显示当日提交数和 Top 3 作者
- **仓库树图**：基于 biturbo native treemap 算法的仓库文件大小可视化，支持逐级钻取点击
- **远端分支跟踪**：右键"跟踪"改为按远端分组的二级菜单，菜单内嵌置顶搜索框，支持大量远端分支快速检索
- **性能优化**：针对大型仓库的刷新、diff 渲染、子模块管理做了专项优化
- **代码统计**：集成 tokei（Rust 编写，支持 200+ 语言），按语言统计代码行数、文件数、注释行、空白行，饼图可视化，支持 Workspace/分支/tag 切换 ref

## 项目结构

```
ForkPlus/
├── src/
│   ├── ForkPlus/              # 主 WPF 应用程序源码、XAML、资源
│   │   ├── Biturbo/           # biturbo native 三方件的 P/Invoke 绑定
│   │   ├── Languages/         # 多语言翻译文件（JSON）
│   │   │   ├── zh-Hans.json   # 简体中文
│   │   │   ├── zh-Hant.json   # 繁體中文
│   │   │   ├── ja-JP.json     # 日本語
│   │   │   ├── ko-KR.json     # 한국어
│   │   │   ├── fr-FR.json     # Français
│   │   │   ├── de-DE.json     # Deutsch
│   │   │   ├── es-ES.json     # Español
│   │   │   └── README.md      # 语言文件格式说明
│   │   └── ...
│   ├── ForkPlus.AskPass/      # Git/SSH 密码输入辅助程序
│   ├── ForkPlus.RI/           # 交互式 rebase 编辑器辅助程序
│   ├── ForkPlus.Tests/        # xUnit 单元测试
│   └── ForkPlus.AutomationTests/  # FlaUI UI 冒烟测试
├── third_party/               # 构建期拉取的原生二进制（见下文「biturbo.dll 来源」）
├── gitmm/                     # git mm 工作流参考文档
└── .github/workflows/         # GitHub Actions CI 配置
```

## 编译

### 环境要求

- Windows 10 或更高版本
- Visual Studio 2022 17.13+，或 .NET 10 SDK
- .NET 10 SDK（含 Windows Desktop runtime）
- Git 2.31 或更高版本（推荐 2.40+，低于此版本启动时会警告，部分功能可能异常）
- git-mm 3.0 或更高版本（使用 git mm 工作流时必需，低于此版本启动时会警告；未安装时 git mm 工作区功能不可用，可在偏好设置中配置 git-mm.exe 路径）

### 编译步骤

- 用 Visual Studio 2022 17.13+ 打开 `ForkPlus.sln`，选择 Release 配置编译
- 或命令行执行：`dotnet build ForkPlus.sln -c Release`

### biturbo.dll 来源

`biturbo.dll` 是 biturbo native 三方件，提供仓库树图布局、提交图缓存、revision header 解析等能力。**该文件不再以二进制形式提交到本仓库**，而是在构建期自动从 [Biturbo 仓库](https://github.com/hebin123456/Biturbo) 的最新 Release 拉取。

具体机制（见 [ForkPlus.csproj](src/ForkPlus/ForkPlus.csproj)）：

- `RestoreBiturbo` target（`BeforeTargets=Build`）：检测到 `third_party/biturbo.dll` 缺失时，用 PowerShell 从 `https://github.com/hebin123456/Biturbo/releases/latest/download/biturbo.dll` 下载到 `third_party/` 目录
- `CopyHelperExecutables` target（`AfterTargets=Build`）：将下载好的 `biturbo.dll` 拷贝到输出目录
- `.gitignore` 已忽略 `third_party/biturbo.dll`，避免污染仓库

因此首次编译需要网络访问 GitHub；后续编译若 `third_party/biturbo.dll` 已存在则跳过下载。CI 每次全新 checkout 都会拉取最新版。

### tokei.exe 来源

`tokei.exe` 是代码行数统计工具 [tokei](https://github.com/XAMPPRocky/tokei)（MIT 协议），用于统计面板的"代码行数"功能。tokei 上游 release 只发布源码、不发布预编译二进制，因此 **构建期用 `cargo install` 从源码编译**。

具体机制（见 [ForkPlus.csproj](src/ForkPlus/ForkPlus.csproj)）：

- `RestoreTokei` target（`BeforeTargets=Build`）：检测到 `third_party/tokei.exe` 缺失时，调 `cargo install tokei --version 14.0.0 --locked` 编译到临时目录，再移动到 `third_party/tokei.exe`；cargo 不在 PATH 时报错提示
- `CopyHelperExecutables` target（`AfterTargets=Build`）：将编译好的 `tokei.exe` 拷贝到输出目录
- `.gitignore` 已忽略 `third_party/tokei.exe` 和 `third_party/.tokei-install/`

因此**本地首次编译需要安装 [Rust 工具链](https://rustup.rs)**（`cargo` 需在 PATH）；或自行编译 tokei 后将 `tokei.exe` 放到 `third_party/` 目录。CI 环境自带 Rust，并用 `actions/cache` 缓存 `tokei.exe`，命中后跳过编译。

### 持续集成

项目配置了 GitHub Actions（[`.github/workflows/build.yml`](.github/workflows/build.yml)），打 `v*` 开头的 tag 会自动在 Windows 环境编译，并发布完整运行时 zip 包到 GitHub Release。

```bash
git tag v1.7.0
git push origin v1.7.0
```

编译产物包含 `ForkPlus.exe`、所有依赖 dll、`biturbo.dll`、语言文件等，解压即可运行。

## 测试

- 单元测试：`dotnet test src/ForkPlus.Tests/ForkPlus.Tests.csproj`
- UI 冒烟测试：设置 `FORKPLUS_AUTOMATION_EXE` 环境变量指向已编译的 `ForkPlus.exe`，然后运行 `dotnet test src/ForkPlus.AutomationTests/ForkPlus.AutomationTests.csproj`

## 多语言支持

### 内置语言

| 语言代码 | 显示名称 | 状态 |
|---------|---------|------|
| `en` | English | 源语言 |
| `zh-Hans` | 简体中文 | 完整 |
| `zh-Hant` | 繁體中文 | 完整 |
| `ja-JP` | 日本語 | 完整 |
| `ko-KR` | 한국어 | 完整 |
| `fr-FR` | Français | 完整 |
| `de-DE` | Deutsch | 完整 |
| `es-ES` | Español | 完整 |

### 添加新语言

在 `src/ForkPlus/Languages/` 目录下新建 `<语言代码>.json` 文件即可，无需修改代码。文件格式：

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

详见 [src/ForkPlus/Languages/README.md](src/ForkPlus/Languages/README.md)。

### 国际化 API

代码中通过以下 API 实现国际化：

- `PreferencesLocalization.Current("English text")` — 简单字符串翻译
- `PreferencesLocalization.FormatCurrent("...{0}...", args)` — 带参数的字符串翻译
- `PreferencesLocalization.Translate(text, language)` — 指定语言的翻译

## 下载

最新版本请前往 [Releases 页面](https://github.com/hebin123456/ForkPlus/releases) 下载。

各版本变更详情请查阅 [Release Notes](RELEASE_NOTE.md)。

## 开发约定

修改应用程序本身时，保持在 `src/ForkPlus` 目录内。`third_party/` 下的运行时二进制（如 `biturbo.dll`）由构建期自动拉取，不要手动提交二进制文件；如需升级 biturbo 版本，请在 [Biturbo 仓库](https://github.com/hebin123456/Biturbo) 发布新 Release，本仓库下次构建会自动拉取。

## 许可证

本项目基于 [MIT License](LICENSE) 开源。

Copyright (c) 2026 hebin123456
