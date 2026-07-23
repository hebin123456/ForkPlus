# 阶段 4：Avalonia UI 重写

> 状态：**待开始**
> 性质：体力活（VM 已干净时）/ 脑力活（VM 未抽干净时）
> 前置：阶段 3 完成（ViewModel 层干净，不依赖 WPF）

## 目标

把 View 层从 WPF 切换到 Avalonia。此时 VM 层已是纯 C#，本阶段主要是"换基类 + 换 XAML 命名空间 + 换第三方库"。

## 待办清单

### 第三方库替换

- [ ] `AvalonEdit` → `Avalonia.AvaloniaEdit`（社区移植，API 接近，需逐个核对）
  - 涉及：`CommitUserControl`、Diff 编辑器、`CodeEditorSearchPanelUserControl`
- [ ] `OxyPlot.Wpf` → `OxyPlot.Avalonia`（社区维护，落后于 Wpf 版）/ 或 `ScottPlot.Avalonia`
  - 涉及：`StatisticsUserControl`
- [ ] `Microsoft.Web.WebView2` → 调研替代方案（**最大风险**）
  - 选项 A：`CefGlue` / `Avalonia.WebView`（包大、跨平台一致，保留富 HTML 渲染）
  - 选项 B：`AvaloniaEdit` + 自写 Markdown→文档模型（中等工作量，跨平台干净）
  - 涉及：`AiDevelopmentWindow` / `AiCodeReviewWindow` / `AiTextResultWindow` / `GitMmReferenceWindow`
- [ ] `Microsoft-WindowsAPICodePack-Shell` → Avalonia `StorageProvider.OpenFilePickerAsync` / `SaveFilePickerAsync`
  - 涉及：`OpenDialog` 静态类（阶段 2 已迁移到 `IFileSystemDialogService`）

### 基类重写

- [ ] `CustomWindow : Window`（`src/ForkPlus/UI/CustomWindow.cs`）→ Avalonia 等价物
  - WPF 特性：`[ContentProperty]` / `[TemplatePart]` / `DependencyProperty` / `WindowChrome`（`System.Windows.Shell`）/ `HwndSource`
  - Avalonia 对应：`ContentProperty` / `TemplatePart` / `AvaloniaProperty` / `Window` 原生 chrome
- [ ] `ForkPlusDialogWindow : CustomWindow`（所有 Dialog 基类）→ Avalonia 等价物
  - WPF 特性：`pack://application:,,,/ForkPlus;component/...` URI → `avares://ForkPlus/Assets/...`
  - `WindowChrome` / `HwndSource` → Avalonia chrome
- [ ] 涉及文件：`UI/Dialogs/` 下约 90 个 `.xaml.cs`、`UI/MainWindow.xaml.cs`、`UI/QuickLaunch/QuickLaunchWindow.xaml.cs`

### 主题系统重写

- [ ] 22 套 `Theme/Generic.*.xaml` → Avalonia `Styles` 机制重写
  - WPF `MergedDictionaries.Add/Remove` 强制 `DynamicResource` 失效 → Avalonia `Styles` / `Resources` 机制不同
- [ ] `App.ReloadThemeDictionary`（第 728-761 行）→ `IThemeService` 实现（阶段 0 推迟，本阶段补）
- [ ] `App.ApplyCustomColors`（第 671-721 行）→ `IThemeService` 实现
- [ ] `Theme.Refresh` / `Theme.FindBrush` / `Theme.FindImage` → Avalonia 资源查找

### XAML 命名空间 + 绑定迁移

- [ ] WPF XAML 命名空间 → Avalonia XAML 命名空间（`http://schemas.microsoft.com/winfx/2006/xaml/presentation` → `https://github.com/avaloniaui`)
- [ ] 176 处 `x:Name` + 事件 → Binding + ICommand（配合阶段 3 的 VM）
- [ ] `pack://application:,,,/ForkPlus;component/...` URI → `avares://ForkPlus/Assets/...`
- [ ] `DynamicResource` → Avalonia `DynamicResource`（语义基本一致，但主题切换机制不同）

### 按页面重要度逐个迁移

1. `MainWindow` → `RepositoryUserControl` → `CommitUserControl`（核心交互链）
2. `RevisionListViewUserControl` / `RevisionDetailsUserControl` / `RevisionFileTreeUserControl`
3. `SidebarUserControl` / `ToolbarUserControl`
4. 各 Dialog（`CloneWindow` / `AiDevelopmentWindow` / `CustomColorsDialog` / `SideBySideMergeWindow` 等）
5. `StatisticsUserControl`（依赖 OxyPlot 替换）
6. `QuickLaunchWindow`

### 阶段 0 推迟的接口（本阶段补）

- [ ] `IThemeService`
  - `ReloadThemeDictionary` / `ApplyCustomColors` 属 View 层职责
  - 需改 `App` 私有方法可见性或搬到 `WpfThemeService` / `AvaloniaThemeService`
- [ ] `IWindowPlacementService`
  - `SetWindowPlacement` / `GetWindowPlacement` 仅 `MainWindow` 使用
  - Avalonia 有原生窗口位置 API，可直接用

## 风险点

- **WebView2 替代是最大不确定性**：若选 CefGlue，包体积大；若选 AvaloniaEdit 渲染 Markdown，需重写 Markdown 渲染逻辑
- **主题系统重写工作量大**：22 套主题字典 + 自定义颜色覆盖机制，需重新设计
- **CustomWindow 基类重写**：所有 Dialog 都继承它，基类改了所有 Dialog 都受影响
- **x:Name → Binding 迁移**：176 处，需逐个评估，部分可能保留 code-behind 事件（Avalonia 也支持）

## 后续阶段衔接

阶段 4 完成后，UI 已切到 Avalonia，但 TFM 仍是 `net10.0-windows`（部分 Win32 调用还在）。阶段 5 处理平台 API 跨平台化，阶段 6 切换 TFM。
