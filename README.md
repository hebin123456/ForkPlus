# ForkPlus

一个基于 Fork 的 Git 图形化工具增强版，使用 Rust 重写了底层能力，新增多语言支持、git mm 和 git repo 工作流，优化了大数据量仓库性能，并修复了大量缺陷。

[English](README.en.md) | [简体中文](README.md) | [繁體中文](README.zh-Hant.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Español](README.es.md)

## 主要特性

- **多语言支持**：内置英语、简体中文、繁體中文、日本語，并支持通过 JSON 文件扩展更多语言
- **git mm 工作流**：内置 `git mm` 子命令，提供精益分支（Lean Branching）工作流
- **AI 辅助开发**：集成 AI 代码审查、自动生成提交信息、AI 辅助修改代码
- **性能优化**：针对大型仓库的刷新、diff 渲染、子模块管理做了专项优化
- **缺陷修复**：修复了原版 Fork 的多个问题（空修改、AI 排队生成失败等）

## 项目结构

```
ForkPlus/
├── src/
│   ├── ForkPlus/              # 主 WPF 应用程序源码、XAML、资源
│   │   ├── Languages/         # 多语言翻译文件（JSON）
│   │   │   ├── zh-Hans.json   # 简体中文
│   │   │   ├── zh-Hant.json   # 繁體中文
│   │   │   ├── ja-JP.json     # 日本語
│   │   │   └── README.md      # 语言文件格式说明
│   │   └── ...
│   ├── ForkPlus.AskPass/      # Git/SSH 密码输入辅助程序
│   ├── ForkPlus.RI/           # 交互式 rebase 编辑器辅助程序
│   ├── ForkPlus.Tests/        # xUnit 单元测试
│   └── ForkPlus.AutomationTests/  # FlaUI UI 冒烟测试
├── third_party/               # 随应用分发的运行时工具和原生二进制
├── gitmm/                     # git mm 工作流参考文档
└── .github/workflows/         # GitHub Actions CI 配置
```

## 编译

### 环境要求

- Windows 10 或更高版本
- Visual Studio 2019/2022，或 .NET SDK + MSBuild
- .NET Framework 4.7.2 目标包

### 编译步骤

- 用 Visual Studio 打开 `ForkPlus.sln`，选择 Release 配置编译
- 或命令行执行：`msbuild ForkPlus.sln /p:Configuration=Release`

### 持续集成

项目配置了 GitHub Actions（[`.github/workflows/build-windows.yml`](.github/workflows/build-windows.yml)），打 `v*` 开头的 tag 会自动在 Windows 环境编译，并发布完整运行时 zip 包到 GitHub Release。

```bash
git tag v1.2.3
git push origin v1.2.3
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

## 开发约定

修改应用程序本身时，保持在 `src/ForkPlus` 目录内，除非有意更新 `third_party` 下的运行时文件。
