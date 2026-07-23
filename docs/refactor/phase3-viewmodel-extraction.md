# 阶段 3：ViewModel 抽取

> 状态：**待开始**
> 性质：最大工作量，重构分水岭
> 前置：阶段 0/1/2 完成（抽象层就位、领域层干净、Commands 去 WPF）

## 目标

为每个 Dialog / UserControl 抽取独立 ViewModel，把 View 字段里的业务状态、业务方法搬到 VM，View 只剩"绑定 + 事件转命令"。这是 Avalonia 能否切得动的**关键判断点**。

## 验收标准

随便挑一个 ViewModel，把 `using System.Windows.*` 全删掉能编译通过。

## 待办清单（按耦合严重程度排序）

### 核心 UserControl

- [ ] `MainWindowViewModel`
  - 从 `MainWindow.xaml.cs` 抽出：TabManager / Toolbar / Manager 字段（`_automaticBackgroundFetchManager` / `_updateCheckManager` / `_repositoryStatusManager`）
  - 快捷键映射（`InitializeKeyBindings` 第 323-493 行 20+ 个 KeyBinding）
  - `OnDrop` / `OnKeyDown` 业务逻辑（QuickFetch 触发、剪贴板 patch 检测）
  - 设置持久化（`Window_Closing` 写 `ForkPlusSettings.Default.MainWindowLocationState`）

- [ ] `RepositoryUserControlViewModel`
  - 从 `RepositoryUserControl.xaml.cs` 抽出字段：`TempFileManager` / `JobQueue` / `UndoRedoStack` / `RefreshRepositoryCommand` / `RepositoryData` / `RepositoryStatus` / `GitModule` / `CommitGraphCache`
  - `OnDrop` 第 122-134 行 `.patch` 扩展名判断 → VM 命令
  - `FindParentRepositoryName` 第 137-163 行（submodule/worktree 判断）→ VM 方法

- [ ] `CommitUserControlViewModel`
  - 从 `CommitUserControl.xaml.cs` 抽出：`CommitMessageAutocompleteProvider` / `GitmojiAutocompleteProvider` / `DelayedAction<ChangedFileArgs>`
  - `AmendMode` 属性（第 64-85 行）→ VM 属性，移除 `Dispatcher.CheckAccess()` / `Dispatcher.Invoke`
  - `FullCommitMessage` 属性 → VM 属性，移除 `CommitSubjectTextBox.Text` 直访
  - `CommitAndPush` getter（第 89-103 行）→ VM 属性，`KeyboardHelper.IsShiftDown` 改为 VM 接收键盘状态

### AI 系列窗口（最复杂）

- [ ] `AiDevelopmentWindowViewModel` — **重构难度最高**
  - 抽出状态字段：`_fileChanges` / `_lastBeforeContents`（撤销快照）/ `_skillEntries` / `_pendingRequests` / `_conversationHistory`（多轮对话）/ `_streamingMarkdown`（流式缓冲）
  - 流式渲染节流逻辑（`StreamingRenderIntervalMs = 400` / `_lastStreamingRenderUtc`）→ VM 用 `ITimerService`
  - `LoadSkillList()` / `InitializeModelComboBox()` / `ShowWelcomeMessage()` → VM 方法
  - WebView2 交互通过 `IWebViewFactory` / `IMarkdownWebView` 抽象（需先补接口，见下文）

- [ ] `AiCodeReviewWindowViewModel` / `AiCommitComposerWindowViewModel` / `AiTextResultWindowViewModel`
  - 同上模式，抽取对话状态、流式状态、WebView2 交互

### Dialog 窗口

- [ ] `CloneWindowViewModel`
  - 从 `CloneWindow.xaml.cs` 抽出：`IsSubmitAllowed`（第 85-103 行）、`GetCommandPreview`（第 105-126 行 git clone 命令拼接）
  - 移除 `RepositoryUrlTextBox.Text` / `RepositoryNameTextBox.Text` / `ParentDirectoryTextBox.Text` 直访 → VM 属性双向绑定

- [ ] `StatisticsUserControlViewModel`
  - 从 `StatisticsUserControl.xaml.cs` 抽出 `AuthorStatViewModel` / `CodeLineLanguageViewModel`（当前是 nested class 第 28-71 行）→ 独立 VM
  - `PlotHelper.CreateLinePlotModel()` → VM 暴露 `PlotModel` 属性（OxyPlot 平台无关部分）

- [ ] `CustomColorsDialogViewModel`
  - 12 处 `MessageBox.Show` → `ServiceLocator.MessageBox`
  - 颜色行动态生成 → `ObservableCollection<ColorItemVm>` + `DataTemplate`

- [ ] `MergeConflictUserControlViewModel` / `SideBySideMergeWindowViewModel`
  - 各 8 处 `MessageBox.Show` → `ServiceLocator.MessageBox`
  - diff 行动态生成 → `ObservableCollection<DiffLineVm>` + `DataTemplateSelector`（按 `LineType` 选模板）

### 阶段 0 推迟的接口（本阶段补）

- [ ] `IWebViewFactory` / `IMarkdownWebView`
  - WebView2 事件面大（`ContextMenuRequested` / `WebMessageReceived` / `NavigationCompleted`），需先设计 AI 窗口 VM 再定义接口
  - 接口草案：
    ```csharp
    public interface IMarkdownWebView
    {
        string Markdown { set; }
        bool IsDarkTheme { set; }
        event Action<string> ScrollPositionChanged;
        event Action<WebViewContextMenuRequest> ContextMenuRequested;
    }
    ```
  - VM 只管"输出 markdown 字符串"，View 负责创建 WebView 并绑定

## 风险点

- **WebView2 是最大单点风险**：Avalonia 无官方 WebView。阶段 3 先抽 VM，WebView2 暂留 WPF View，等 VM 稳定后阶段 4 再决定替代方案（CefGlue / AvaloniaEdit 渲染 Markdown）
- **静态全局访问**：`MainWindow.Instance` / `Application.Current.MainWindow` / `MainWindow.ActiveRepositoryUserControl` 共 74 处，需在 VM 化过程中逐步注入
- **x:Name 直访**：176 处 `x:Name` + 事件，需逐个评估改 Binding+Command 还是保留事件（配合 VM）
- **Dispatcher 直调**：29 个文件直接用 WPF Dispatcher，需替换为 `ServiceLocator.Dispatcher`

## 后续阶段衔接

阶段 3 完成后，VM 层纯 C# 不依赖任何 UI 框架，切 Avalonia 只是"换基类 + 换 XAML 命名空间 + 换第三方库"。
