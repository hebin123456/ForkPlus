# ForkPlus

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Build](https://github.com/hebin123456/ForkPlus/actions/workflows/build.yml/badge.svg)](https://github.com/hebin123456/ForkPlus/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/hebin123456/ForkPlus)](https://github.com/hebin123456/ForkPlus/releases)

跨平台 Git 客户端：UI 层基于 .NET 10 + Avalonia 12 构建，一套代码运行于 Windows / Linux / macOS。底层 Rust 引擎（biturbo native）、AI 辅助开发、8 种语言、12 套主题皮肤、git mm 工作流，以及贡献热力图、仓库树图等可视化能力开箱即用。

> 构建环境与编译步骤详见 [BUILDING.md](BUILDING.md)。

[English](README.en.md) | [简体中文](README.md) | [繁體中文](README.zh-Hant.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Español](README.es.md)

## 主要特性

- **跨平台**：基于 Avalonia 12 的跨平台 UI 层，CI 同时产出 Windows x64 / Linux x64 / macOS arm64 三平台构建，产物为自包含（self-contained）发布，无需安装 .NET 运行时
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
│   ├── ForkPlus/              # 主应用程序源码（Avalonia 12 跨平台 UI）、XAML、资源
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
│   ├── ForkPlus.Tests/        # xUnit 单元测试（含 Avalonia.Headless UI 冒烟测试）
│   ├── ForkPlus.AskPass.Tests/# AskPass 辅助程序单元测试
│   └── ForkPlus.RI.Tests/     # RI 辅助程序单元测试
├── third_party/               # 构建期拉取的原生二进制（见下文「biturbo native 库来源」）
├── gitmm/                     # git mm 工作流参考文档
└── .github/workflows/         # GitHub Actions CI 配置
```

## 编译

### 环境要求

- Windows 10 或更高版本 / Linux / macOS（跨平台支持）
- .NET 10 SDK
- IDE（可选）：Visual Studio 2026（Windows，可用仓库根目录的 `OpenForkPlusInVS2026.cmd` 一键打开解决方案）、Rider 或 VS Code
- Git 2.40 或更高版本（推荐；低于推荐版本启动时会警告，部分功能可能异常。应用优先使用的内置 git 实例版本为 2.50.1，缺失时回退系统 git）
- git-mm 3.0 或更高版本（使用 git mm 工作流时必需，低于此版本启动时会警告；未安装时 git mm 工作区功能不可用，可在偏好设置中配置 git-mm 路径）

### 编译步骤

```bash
# 编译（仓库根目录）。图表库 OxyPlot.Avalonia 与 biturbo / tokei 三方产物均无需手动准备，
# 构建期自动从对应仓库的 latest Release 拉取（见下方"三方件来源"各节），仅需网络可达 GitHub：
dotnet build ForkPlus.sln -c Release
```

也可用 Visual Studio 2026 直接打开仓库根目录的 `ForkPlus.sln` 编译。

### biturbo native 库来源

biturbo native 三方件（Rust）提供仓库树图布局、提交图缓存、revision header 解析等能力。**该文件不以二进制形式提交到本仓库**，而是在构建期自动从 [Biturbo 仓库](https://github.com/hebin123456/Biturbo) 的最新 Release 拉取，按平台选择文件：

| 平台 | 文件 |
|------|------|
| Windows x64 | `third_party/biturbo.dll` |
| Linux x64 | `third_party/libbiturbo.so` |
| macOS arm64 | `third_party/libbiturbo.dylib` |

具体机制（见 [ForkPlus.csproj](src/ForkPlus/ForkPlus.csproj)）：

- `RestoreBiturbo` target（`BeforeTargets=Build`）：检测到当前平台的 native 库缺失时自动下载（Windows 走 PowerShell，Linux/macOS 走 bash + curl 并按 `uname -s` 选择 `.so` / `.dylib`，均带重试）
- `CopyHelperExecutables` / `PublishHelperExecutables` target（`AfterTargets=Build` / `Publish`）：将 native 库与 AskPass/RI 子进程产物拷贝到 Build / Publish 输出目录
- `.gitignore` 已忽略 `third_party/` 下这些文件

因此首次编译需要网络访问 GitHub；CI 上由 workflow 显式下载并校验（非空且 >1MB），csproj 的 `RestoreBiturbo` 作为兜底。

### tokei 来源

[tokei](https://github.com/XAMPPRocky/tokei)（MIT 协议）用于统计面板的"代码行数"功能。构建期从 [hebin123456/tokei](https://github.com/hebin123456/tokei) 仓库的最新 Release 拉取**预编译二进制**，不再需要本地 Rust 工具链：

- Windows x64 → 裸 exe，存为 `third_party/tokei.exe`
- Linux x64 / macOS → tar.gz（内含裸 `tokei` 二进制），解压存为 `third_party/tokei`
- macOS 资产为 x86_64，Apple Silicon 经 Rosetta 2 运行

机制与 biturbo 相同：`RestoreTokei` target（`BeforeTargets=Build`）自动拉取，CI 显式下载并校验，`.gitignore` 忽略产物。

### OxyPlot.Avalonia 来源

图表库 [OxyPlot.Avalonia](https://github.com/oxyplot/oxyplot-avalonia)（MIT 协议）用于统计面板的绘图控件。**官方仓库停留在 Avalonia 11 且不发布二进制**，本仓库消费 [hebin123456/oxyplot-avalonia](https://github.com/hebin123456/oxyplot-avalonia) fork 发行的**预编译 NuGet 包**（按 Avalonia 12.1.1 原生编译，net8.0 / net10.0 双目标）：

- 包版本带 `-avalonia12.x` 预发布后缀（如 `2.1.2-avalonia12.1`），与 nuget.org 官方 `2.1.2`（面向 Avalonia 11）永不混淆，恢复只命中 fork 包
- 构建期从 fork 仓库 latest Release 下载 `OxyPlot.Avalonia.<版本>.nupkg` 到 `third_party/nuget/`（仓库根 `nuget.config` 注册的本地目录源），由 `PackageReference` 恢复
- 拉取机制：ForkPlus.csproj 的 `RestoreOxyPlotAvalonia` target（`BeforeTargets=Restore;Build`，本地开发）+ CI 显式下载步骤（build.yml，规避 macOS runner 上 MSBuild Exec 差异），`.gitignore` 忽略产物
- 升级 = fork 仓库改 `AvaloniaVersion` 后打 `v*` tag 发新 Release，本仓库改 `ForkPlus.csproj` 的 `OxyPlotAvaloniaPackageVersion` 单点即可

### 持续集成

项目配置了 GitHub Actions（[`.github/workflows/build.yml`](.github/workflows/build.yml)）：推送 `v*` 标签或手动触发时，在三平台并行构建并上传产物，同时在 Linux runner 上运行全量单元与 E2E 测试，全部通过后自动发布 Release（三平台 zip 附件）：

| 矩阵 | Runner | RID |
|------|--------|-----|
| windows-x64 | windows-latest | win-x64 |
| linux-x64 | ubuntu-latest | linux-x64 |
| macos-arm64 | macos-latest | osx-arm64 |

产物为 **self-contained publish**（自带 .NET 10 运行时，目标机无需安装任何框架），包含主程序、AskPass/RI 子进程（同样自包含，git 凭证输入与交互式变基链路在无运行时环境可用）、对应平台的 biturbo native 库、tokei 与语言文件（linux-x64 约 125MB）。发布版本的 zip 附件见 [Releases 页面](https://github.com/hebin123456/ForkPlus/releases)；未打 tag 的手动构建产物可在仓库 [Actions](https://github.com/hebin123456/ForkPlus/actions) 页面的对应运行中下载（Artifacts，保留 14 天）。

## 测试

- 单元测试：`dotnet test src/ForkPlus.Tests/ForkPlus.Tests.csproj`（含 Avalonia.Headless UI 冒烟与端到端测试，跨平台，随单测一起运行）
- 全量 4400+ 用例；关键修复均配有回归防线（失败即测试红灯）
- CI 在 tag 构建与手动触发时于 ubuntu runner 上运行全量测试（含 AskPass / RI 辅助程序测试），环境依赖 gitflow-avh 与 git-lfs（见 workflow 注释）

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

- 正式发布版本：[Releases 页面](https://github.com/hebin123456/ForkPlus/releases)（自包含式，自带 .NET 10 运行时，无需安装任何框架，三平台 zip）
- 各版本变更详情请查阅 [Release Notes](RELEASE_NOTE.md)（含 WPF 版历史）

## 开发约定

- 修改应用程序本身时，保持在 `src/ForkPlus` 目录内；`third_party/` 下的运行时二进制（biturbo native 库、tokei）由构建期自动拉取，不要手动提交二进制文件
- 如需升级 biturbo / tokei 版本，在对应仓库发布新 Release 即可，本仓库下次构建会自动拉取
- 图表库 OxyPlot.Avalonia 消费 [hebin123456/oxyplot-avalonia](https://github.com/hebin123456/oxyplot-avalonia) fork 的预编译 nupkg（按 Avalonia 12.1.1 编译；官方包停留在 Avalonia 11，其 XAML IL 在 12 运行时会 MissingMethodException），构建期从该 fork 的 latest Release 拉取

## 许可证

本项目基于 [MIT License](LICENSE) 开源。

Copyright (c) 2026 hebin123456
