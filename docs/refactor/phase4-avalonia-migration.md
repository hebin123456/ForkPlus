# 阶段 4：Avalonia UI 重写

> 状态：**进行中**（4.1-4.4 完成，4.5 进行中）
> 性质：体力活（VM 已干净时）/ 脑力活（VM 未抽干净时）
> 前置：阶段 3 完成（ViewModel 层干净，不依赖 WPF）
> 分支：`master-refactor2`

## 目标

把 View 层从 WPF 切换到 Avalonia。此时 VM 层已是纯 C#，本阶段主要是"换基类 + 换 XAML 命名空间 + 换第三方库"。

## 待办清单

### 第三方库替换

- [x] `AvalonEdit` → `Avalonia.AvaloniaEdit`（4.7-a 完成）
  - 涉及：`CodeEditor`、`DiffCodeEditor`、`MergeCodeEditor`、`HexEditor`、`HexContentControl`、`HexDiffUserControl`、`CodeEditorSearchPanelUserControl`、`SlidingPanelHelper` 等
  - 已完成：命名空间替换、`StreamGeometryContext` API 适配、`WeakEventManager` → 直接订阅、`SetResourceReference` → 移除、`OnRenderSizeChanged` → `OnSizeChanged`、模板部件查找适配、`Brush` → `IBrush`、`Freeze()` 移除、`Pointer*` 事件替换
  - 剩余：`MenuExtensions` + 依赖的命令文件（`HunkHistoryCommand`/`CopyCommand`/`OpenFileInExternalEditorCommand`）属于菜单系统迁移，单独处理
- [x] `OxyPlot.Wpf` → `OxyPlot.Avalonia`（4.7-b 完成）
  - 涉及：`StatisticsUserControl`、`StatisticsUserControlViewModel`
  - 已完成：`OxyPlot.Wpf` → `OxyPlot.Avalonia`、`WeakEventManager` → 直接订阅、`ListCollectionView`/`CollectionViewSource` → 过滤 `ObservableCollection`、`Dispatcher.BeginInvoke` → `Dispatcher.Post`、`ToOxyColor()` 扩展方法（`OxyPlotExtensions.cs`）、XAML xmlns 替换、`{x:Type}` → Selector 语法
- [ ] `Microsoft.Web.WebView2` → 调研替代方案（4.7-c 进行中，**最大工作量**）
  - **评估结论：推荐方案 B（Markdig + Avalonia 原生 Markdown 渲染）**——采用现成包 `OneWare.Markdown.Avalonia.Tight` 11.3.17.1（Avalonia 11.3.17 + net10.0，MIT，fork 自 whistyun/Markdown.Avalonia），无需自建 `MarkdownAvaloniaRenderer`
  - 与 csproj 声明一致："阶段 4.7 由 AvaloniaEdit + Markdown 渲染替代后移除"
  - 涉及：`AiDevelopmentWindow` / `AiCodeReviewWindow` / `AiTextResultWindow` / `GitMmReferenceWindow` / `WebView2EnvironmentHelper` / `AiStreamingMarkdownViewModel`
  - 拒绝方案 A（CefNet/Avalonia.WebView）：CefNet 未维护，~150MB Chromium 依赖，不推进跨平台目标
  - 拒绝方案 C（CefNet 直接使用）：同上风险更高
  - 过渡方案 D（Windows 保留 WebView2 + 跨平台 fallback）：可作增量迁移过渡
  - JS 互操作：`AiTextResultWindow`/`AiCodeReviewWindow` 用 `window.chrome.webview.postMessage` 做滚动追踪和建议卡按钮回调，需改为原生 Avalonia 事件
  - **进度**：
    - [x] 4.7-c-1：`GitMmReferenceWindow` 迁移完成（最简单，无 JS 互操作）
      - `WebView2.NavigateToString(HTML)` → `MarkdownScrollViewer.Markdown` 属性（原生 Avalonia 控件渲染）
      - 移除全部 HTML 生成代码（`MarkdownToHtml`/`AppendHtmlTable`/`ConvertInlineMarkdownToHtml`/`CreateHtmlDocument`/`Bt.bt_md_to_html` 调用）
      - GFM 表格改由 Markdown.Avalonia 内置支持（替代自研 `IsTableRow`/`SplitTableRow` 解析）
      - 移除 `Microsoft.Web.WebView2.Core` using；删除 `GitMmReferenceWindowTests`（测试已移除的 `MarkdownToHtml` 方法）
      - 保留 `FallbackUserControl`（仍 WPF，44 文件共用，单独迁移）
    - [ ] 4.7-c-2：`AiDevelopmentWindow`（无 postMessage，但有多气泡 + 动态 WebView 创建 + `ExecuteScriptAsync` 测高）
      - **进度**：
        - [x] 移除 `Microsoft.Web.WebView2.Core`/`Wpf` using，添加 `using Markdown.Avalonia;`
        - [x] `_streamingWebView` 类型 `WebView2` → `MarkdownScrollViewer`
        - [x] `RenderMarkdownToWebView(WebView2, md)` → `RenderMarkdownToViewer(MarkdownScrollViewer, md)`：`NavigateToString(HTML)` → `Markdown = markdown` 直接绑定（移除 `AiStreamingMarkdownViewModel.RenderMarkdownToHtmlDocument` 调用）
        - [x] `TryRenderStreamingPreview`：`.CoreWebView2 == null` 判空 → `== null` 判空
        - [x] `InitializeAiMessageWebViewAsync(WebView2)` → `ConfigureMarkdownViewer(MarkdownScrollViewer)`：删除 `EnsureCoreWebView2Async`/`PreferredColorScheme`/`ContextMenuRequested`/`NavigationCompleted` + `ExecuteScriptAsync("scrollHeight")` JS 自动高度测量（Avalonia 原生布局自动处理高度）；仅保留 `MaxHeight = 480` 防溢出
        - [x] `CreateStreamingResponseBubble`：`new WebView2 { DefaultBackgroundColor = Transparent }` → `new MarkdownScrollViewer()`；删除 `_ = InitializeAiMessageWebViewAsync(webView)` 异步初始化调用
        - [x] `AddAiResponseMessage` 非流式气泡：同上替换；`await InitializeAiMessageWebViewAsync` + `RenderMarkdownToWebView` → `RenderMarkdownToViewer`（同步调用）
        - [x] `RemoveStreamingResponseBubble`/`FinalizeStreamingResponseBubble` 参数类型 `WebView2` → `MarkdownScrollViewer`；`.CoreWebView2 != null` → `!= null`
        - [x] 顺手修复 WPF 残留：`PreviewKeyDown` → `KeyDown`（Avalonia 无隧道事件）；`System.Windows.Input.KeyEventArgs`/`Key`/`Keyboard.IsKeyDown` → `Avalonia.Input.KeyEventArgs`/`Key`/`e.KeyModifiers.HasFlag`；`Dispatcher.BeginInvoke(Delegate, DispatcherPriority.Background)` → `Dispatcher.Post(Action)`；`(Brush)FindResource(...)` → `Theme.FindBrush(...)`
    - [ ] 4.7-c-3：`AiTextResultWindow`（scroll-at-bottom JS 互操作 → Avalonia ScrollViewer 事件）
      - **进度**：
        - [x] XAML：`wv2:WebView2` → `md:MarkdownScrollViewer`，移除 `xmlns:wv2` 命名空间
        - [x] 移除 `using Microsoft.Web.WebView2.Core`，添加 `using Markdown.Avalonia` + `using Avalonia.VisualTree` + `using ForkPlus.Services`
        - [x] `InitializeWebView()` → `AttachScrollTracker()`：删除 `EnsureCoreWebView2Async`/`ContextMenuRequested`/`WebMessageReceived`/`NavigationCompleted` + `ExecuteScriptAsync("scrollTo")`；改为 `GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault()` 查找内部 ScrollViewer 并订阅 `ScrollChanged`
        - [x] `CoreWebView2_WebMessageReceived` → `InnerScrollViewer_ScrollChanged`：JS postMessage('scroll-at-bottom:1/0') → `Offset.Y + Viewport.Height >= Extent.Height - 80` 原生判定
        - [x] `RenderMarkdown`：`RenderMarkdownToHtmlDocumentWithScrollScript` + `NavigateToString(HTML)` → `Markdown = markdown` + `Dispatcher.Post(ScrollInnerViewerToEnd)`（延迟一轮布局再滚到底）
        - [x] `ShowError`：`BuildErrorHtmlDocument` → Markdown 引用块 `"> ⚠️ ..."`
        - [x] `StartStreaming`：`.CoreWebView2 != null` → `_innerScrollViewer != null`（Loaded 已执行的标志）
        - [x] `TryRenderStreamingPreview`：`.CoreWebView2 == null` → `== null`
        - [x] `CopyButton_Click`：`Clipboard.SetText` → `ServiceLocator.Clipboard.SetText`（跨平台剪贴板服务）
    - [ ] 4.7-c-4：`AiCodeReviewWindow`（scroll 追踪 + 建议卡按钮回调 preview/apply suggestion → 原生 Avalonia 事件/Command，最复杂）
    - [ ] 4.7-c-5：`WebView2EnvironmentHelper` 删除 + csproj 移除 `Microsoft.Web.WebView2` 包 + `CopyWebView2LoaderToRoot` MSBuild target 删除
- [x] `Microsoft-WindowsAPICodePack-Shell` → Avalonia `StorageProvider.OpenFilePickerAsync` / `SaveFilePickerAsync`（4.7-d 完成）
  - 涉及：`OpenDialog` 静态类（阶段 2 已迁移到 `IFileSystemDialogService`）、`WpfFileSystemDialogService`、`IFileSystemDialogService`
  - 已完成：`CommonOpenFileDialog` → `OpenFolderPickerAsync`/`OpenFilePickerAsync`、`CommonSaveFileDialog` → `SaveFilePickerAsync`、`GetAwaiter().GetResult()` 同步阻塞异步调用、`PreventRefreshAfterChildDialogClose` 保留、`FilePickerFileType.Patterns` glob 规范化（WPF `.txt` → Avalonia `*.txt`）

### 基类重写（里程碑 4.2，已完成）

- [x] `CustomWindow : Window`（`src/ForkPlus/UI/CustomWindow.cs`）→ Avalonia 等价物（4.2-a 完成）
  - WPF `WindowChrome`/`HwndSource`/Win32 消息钩子 → Avalonia `ExtendClientAreaToDecorationsHint`
  - `DependencyProperty` → `StyledProperty<T>`
  - `OnSourceInitialized`（Win32 专属）→ 移除
  - `CommandBindings`/`SystemCommands` → 直接 `Button.Click` 处理
  - `GetTemplateChild` → `e.NameScope.Get<T>` in `OnApplyTemplate`
  - `OnPropertyChanged` override 处理 `ShowHeader` 属性变更
- [x] `ForkPlusDialogWindow : CustomWindow`（所有 Dialog 基类）→ Avalonia 等价物（4.2-b 完成）
  - `BitmapImage(uri)` → `new Bitmap(uri)`（Avalonia.Media.Imaging）
  - `pack://application:,,,/ForkPlus;component/...` → `avares://ForkPlus/Assets/...`
  - `VisualTreeHelper.GetChildrenCount/GetChild` → `GetVisualDescendants().OfType<T>()`
  - `ComponentDispatcher.IsThreadModal` → 移除
  - `WeakEventManager` → 直接事件订阅
  - `Application.Current.TryFindResource` → `Theme.FindBrush` / `Resources.TryGetResource`
  - `OverridesDefaultStyle`/`RenderOptions.SetClearTypeHint` → 移除
  - `ResizeMode.NoResize` → `CanResize=false`
  - `Initialized` 事件/`OnContentChanged`/`IsInitialized` → `Loaded` + `_dialogChromeInitialized` 标志
  - `MoveFocus(TraversalRequest)` → `Grid.Focus()`
  - `DrawingImage`+`GeometryDrawing` → `TextBlock` emoji（复制按钮图标）
  - `ToolTip` 实例属性 → `ToolTip.SetTip(target, value)`
  - 所有 `Visibility`/`Alignment`/`GridLength`/`FontWeights` → Avalonia 等价类型

### 主题系统重写（里程碑 4.3，已完成）

- [x] 69 个 `Theme/*.xaml` 命名空间迁移（4.3-a 完成）
  - `xmlns` WPF → Avalonia（sed 批量替换）
  - `pack://application:,,,/ForkPlus;component/` → `avares://ForkPlus/`
- [x] `IThemeService` 接口 + `AvaloniaThemeService` 实现（4.3-b 完成）
  - 接口：`Refresh()` + `GetSystemBrush(SystemColorType, IBrush fallback)`
  - 实现：`PlatformSettings.GetColorValues().AccentColor1` 获取系统强调色
  - `SystemColorType` 枚举从 `Theme.cs` 提取到 `Services` 命名空间
  - `ServiceLocator` 添加 `ThemeService` 属性和 `RegisterPlatformServices(themeService:)` 参数
- [x] `Theme.cs` 门面迁移（4.3-b 完成）
  - `Brush` → `IBrush`，`ImageSource` → `IImage`，`Style` → `Avalonia.Styling.Style`
  - `Application.Current.TryFindResource` → `Resources.TryGetResource`
  - `SystemThemeHelper.GetSystemBrush` → `ServiceLocator.ThemeService.GetSystemBrush`
  - `Refresh` 转发到 `ServiceLocator.ThemeService`
- [x] `App.axaml.cs` 主题方法迁移（4.3-c 完成）
  - `RefreshWindowBorderBrush`：`SystemParameters.WindowGlassBrush` → `ThemeService.GetSystemBrush`，`ResourceDictionary`/`MergedDictionaries` → 直接 `Resources[key]=value`，移除 `Freeze()`
  - `InitializeTheme`：`MergedDictionaries` Add/Remove → `RequestedThemeVariant`
  - `ApplyCustomColors`：`ColorConverter.ConvertFromString` → `Color.Parse`，`MergedDictionaries` → 直接 `Resources[key]=value`
  - `ReloadThemeDictionary`：`MergedDictionaries` 遍历 → `Theme.Refresh()`
  - 注册 `AvaloniaThemeService` 到 `ServiceLocator`

### XAML 命名空间 + 绑定迁移（里程碑 4.4，已完成）

- [x] WPF XAML 命名空间 → Avalonia XAML 命名空间（`http://schemas.microsoft.com/winfx/2006/xaml/presentation` → `https://github.com/avaloniaui`）
  - 110 个 Dialog `.xaml`（sed 批量替换）
  - 69 个 Theme `.xaml`（4.3-a 完成）
- [x] `pack://application:,,,/ForkPlus;component/...` URI → `avares://ForkPlus/Assets/...`
- [x] `ResizeMode` → `CanResize`，`<ColumnDefinition />` → `<ColumnDefinition Width="*" />`
- [x] Dialog code-behind WPF using 批量移除（100 个 `.xaml.cs`）
- [x] Dialog code-behind Avalonia using 智能添加（98 个文件，Python 脚本按类型用法推断）
- [ ] 176 处 `x:Name` + 事件 → Binding + ICommand（配合阶段 3 的 VM）（推迟到 4.5）
- [ ] `DynamicResource` → Avalonia `DynamicResource`（语义基本一致，但主题切换机制不同）（推迟到 4.6）

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
