# ForkPlus

一款使用 Rust 重寫底層引擎的高效能 Git 圖形化用戶端，內建 AI 輔助開發、8 種語言、12 套主題皮膚、git mm 工作流，以及貢獻熱力圖、倉庫樹圖等視覺化能力。

[English](README.en.md) | [简体中文](README.md) | [繁體中文](README.zh-Hant.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Español](README.es.md)

## 主要特性

- **多語言支援**：內建英語、簡體中文、繁體中文、日本語，並支援透過 JSON 檔案擴充更多語言
- **多主題面板**：內建 12 套預設面板（Light/Dark、Solarized、GitHub、Dracula、Monokai、紫色/綠色淺色深色），並支援使用者自訂顏色覆蓋，即時生效
- **git mm 工作流**：內建 `git mm` 子命令，提供精精益分支（Lean Branching）工作流
- **AI 輔助開發**：整合 AI 程式碼審查、自動生成提交訊息、AI 輔助修改程式碼
- **效能優化**：針對大型倉庫的重新整理、diff 渲染、子模組管理做了專項優化
- **缺陷修復**：修復了原版 Fork 的多個問題（空修改、AI 排隊生成失敗等）

## 專案結構

```
ForkPlus/
├── src/
│   ├── ForkPlus/              # 主 WPF 應用程式原始碼、XAML、資源
│   │   ├── Languages/         # 多語言翻譯檔案（JSON）
│   │   │   ├── zh-Hans.json   # 簡體中文
│   │   │   ├── zh-Hant.json   # 繁體中文
│   │   │   ├── ja-JP.json     # 日本語
│   │   │   ├── ko-KR.json     # 한국어
│   │   │   ├── fr-FR.json     # Français
│   │   │   ├── de-DE.json     # Deutsch
│   │   │   ├── es-ES.json     # Español
│   │   │   └── README.md      # 語言檔案格式說明
│   │   └── ...
│   ├── ForkPlus.AskPass/      # Git/SSH 密碼輸入輔助程式
│   ├── ForkPlus.RI/           # 互動式 rebase 編輯器輔助程式
│   ├── ForkPlus.Tests/        # xUnit 單元測試
│   └── ForkPlus.AutomationTests/  # FlaUI UI 冒煙測試
├── third_party/               # 隨應用分發的執行時工具和原生二進位檔案
├── gitmm/                     # git mm 工作流參考文件
└── .github/workflows/         # GitHub Actions CI 配置
```

## 編譯

### 環境要求

- Windows 10 或更高版本
- Visual Studio 2019/2022，或 .NET SDK + MSBuild
- .NET Framework 4.7.2 目標包
- Git 2.31 或更高版本（推薦 2.40+，低於此版本啟動時會警告，部分功能可能異常）
- git-mm 3.0 或更新版本（使用 git mm 工作流時必需，低於此版本啟動時會警告；未安裝時 git mm 工作區功能不可用，可在偏好設定中設定 git-mm.exe 路徑）

### 編譯步驟

- 用 Visual Studio 開啟 `ForkPlus.sln`，選擇 Release 配置編譯
- 或命令列執行：`msbuild ForkPlus.sln /p:Configuration=Release`

### 持續整合

專案配置了 GitHub Actions（[`.github/workflows/build-windows.yml`](.github/workflows/build-windows.yml)），打 `v*` 開頭的 tag 會自動在 Windows 環境編譯，並發布完整執行時 zip 包到 GitHub Release。

```bash
git tag v1.3.0
git push origin v1.3.0
```

編譯產物包含 `ForkPlus.exe`、所有依賴 dll、`biturbo.dll`、語言檔案等，解壓即可執行。

## 測試

- 單元測試：`dotnet test src/ForkPlus.Tests/ForkPlus.Tests.csproj`
- UI 冒煙測試：設定 `FORKPLUS_AUTOMATION_EXE` 環境變數指向已編譯的 `ForkPlus.exe`，然後執行 `dotnet test src/ForkPlus.AutomationTests/ForkPlus.AutomationTests.csproj`

## 多語言支援

### 內建語言

| 語言代碼 | 顯示名稱 | 狀態 |
|---------|---------|------|
| `en` | English | 來源語言 |
| `zh-Hans` | 简体中文 | 完整 |
| `zh-Hant` | 繁體中文 | 完整 |
| `ja-JP` | 日本語 | 完整 |
| `ko-KR` | 한국어 | 完整 |
| `fr-FR` | Français | 完整 |
| `de-DE` | Deutsch | 完整 |
| `es-ES` | Español | 完整 |

### 新增新語言

在 `src/ForkPlus/Languages/` 目錄下新建 `<語言代碼>.json` 檔案即可，無需修改程式碼。檔案格式：

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

詳見 [src/ForkPlus/Languages/README.md](src/ForkPlus/Languages/README.md)。

### 國際化 API

程式碼中透過以下 API 實現國際化：

- `PreferencesLocalization.Current("English text")` — 簡單字串翻譯
- `PreferencesLocalization.FormatCurrent("...{0}...", args)` — 帶參數的字串翻譯
- `PreferencesLocalization.Translate(text, language)` — 指定語言的翻譯

## 下載

最新版本請前往 [Releases 頁面](https://github.com/hebin123456/ForkPlus/releases) 下載。

各版本變更詳情請查閱 [Release Notes](RELEASE_NOTE.md)。

## 開發約定

修改應用程式本身時，保持在 `src/ForkPlus` 目錄內，除非有意更新 `third_party` 下的執行時檔案。
