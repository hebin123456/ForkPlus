# 阶段 6：移除 WPF 框架依赖

> 状态：**待开始**
> 性质：最终切换，需全量测试
> 前置：阶段 5 完成（所有 Win32 已跨平台化）

## 目标

移除 WPF 框架依赖，切换 TFM 到 `net10.0`（跨平台目标），开放 Linux/macOS 构建。

## 待办清单

### 工程文件切换

- [ ] `src/ForkPlus/ForkPlus.csproj`
  - `<UseWPF>true</UseWPF>` → `<UseAvalonia>true</UseAvalonia>`
  - `<TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>` → `<TargetFramework>net10.0</TargetFramework>`
  - 更新 `NoWarn` 中的 `CA1416`（平台兼容性分析，跨平台后需重新启用）

### NuGet 包清理

- [x] 移除 `Microsoft.Web.WebView2`（阶段 4.7-c-5 已移除）
- [ ] 移除 `CommunityToolkit.WinUI.Notifications`（阶段 5 已替换）
- [x] 移除 `Microsoft-WindowsAPICodePack-Shell`（阶段 4.7-d 已移除包引用）
- [ ] 确认 `AvalonEdit` → `Avalonia.AvaloniaEdit`（阶段 4 已替换）
- [ ] 确认 `OxyPlot.Wpf` → `OxyPlot.Avalonia` 或 `ScottPlot.Avalonia`（阶段 4 已替换）

### CI 配置开放跨平台构建

- [ ] `.github/workflows/build.yml`
  - Windows runner：保持完整 build + test + artifact
  - Linux/macOS runner：从"仅 biturbo 冒烟测试"升级为"完整 build + test"
  - 更新 artifact 命名（`ForkPlus-windows` / `ForkPlus-linux` / `ForkPlus-macos`）

### 构建后处理清理

- [ ] `ForkPlus.csproj` 的 `RestoreBiturbo` target
  - 已支持多平台（Windows/Linux/macOS），无需改
- [ ] `RestoreTokei` target
  - 当前仅 Windows（`Condition="'$(OS)' == 'Windows_NT'"`），需补 Linux/macOS tokei 拉取
  - tokei 是跨平台工具，有 Linux/macOS 预编译二进制
- [ ] `CopyHelperExecutables` target
  - 子进程 exe（AskPass / RI）的跨平台拷贝逻辑
  - Linux/macOS 不需要 apphost，但需拷贝 .dll 和 .runtimeconfig.json
- [x] `CopyWebView2LoaderToRoot` target（阶段 4.7-c-5 已删除）

### 全局验证

- [ ] 全局 grep `using System.Windows` 应为 0 命中（除 `Services/Wpf/` 实现层，若保留为兼容）
- [ ] 全局 grep `pack://application` 应为 0 命中（已换 `avares://`）
- [ ] 全局 grep `DllImport.*"shell32|user32|kernel32|Shlwapi|advapi32"` 应为 0 命中（除 `Services/Wpf/` 平台实现层）
- [ ] Windows build 通过
- [ ] Linux build 通过
- [ ] macOS build 通过
- [ ] 全量单元测试通过（Windows）
- [ ] 冒烟测试通过（Linux/macOS）

## 风险点

- **tokei 跨平台拉取**：tokei 有 Linux/macOS 预编译二进制，但 asset 命名规则需确认
- **子进程跨平台**：`ForkPlus.AskPass` / `ForkPlus.RI` 需确认在 Linux/macOS 的行为
- **Avalonia Linux/macOS 运行时依赖**：需确认目标用户环境有 GUI 库（X11 / Wayland / macOS Cocoa）
- **测试覆盖率**：现有自动化测试（`ForkPlus.AutomationTests`）使用 FlaUI（Windows UIA），跨平台需换 Avalonia 测试框架

## 完成标志

- `dotnet build` 在 Windows / Linux / macOS 三平台均通过
- 三平台 artifact 正常产出
- 跨平台冒烟测试通过
- `master-refactor` 分支可合并到 `master`，重构完成
