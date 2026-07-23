# 阶段 3：ViewModel 抽取

> 状态：**进行中**（阶段 2→3 过渡里程碑已完成）
> 性质：最大工作量，重构分水岭
> 前置：阶段 0/1/2 完成（抽象层就位、领域层干净、Commands 去 WPF）

## 目标

为每个 Dialog / UserControl 抽取独立 ViewModel，把 View 字段里的业务状态、业务方法搬到 VM，View 只剩"绑定 + 事件转命令"。这是 Avalonia 能否切得动的**关键判断点**。

## 验收标准

随便挑一个 ViewModel，把 `using System.Windows.*` 全删掉能编译通过。

---

## 里程碑 3.0：清除 Commands 层残留的 `Application.Current.ActiveRepositoryUserControl()`（阶段 2→3 过渡）

> 状态：**已完成**（待 CI 验证）

阶段 2 验收时遗留 14 处 `Application.Current.ActiveRepositoryUserControl()` 直访，标记为"留待阶段 3"。本里程碑在不抽取 VM 的前提下，先把这些直访收口到服务抽象，让 Commands 层彻底不依赖 `Application.Current`。

### 方案：扩展 `IWindowManagerService`（+5 活动仓库视图操作）

关键约束：新接口方法的参数/返回类型只能引用**领域/根层类型**（`SubDomain` / `RevisionDiffTarget` / `GitModule` / `TempFileManager`），不能引用 UI 层类型（`RevisionSelector` / `RepositoryViewMode` 等），否则会形成 Services → UI 的逆向依赖。

新增 5 方法（[IWindowManagerService.cs](file:///workspace/src/ForkPlus/Services/IWindowManagerService.cs)）：

| 方法 | 替换的直访 | 用的类型 |
|---|---|---|
| `InvalidateAndRefreshActiveRepositoryView(SubDomain)` | `ActiveRepositoryUserControl().InvalidateAndRefresh(domain)` | `SubDomain`（Git） |
| `ActivateRevisionViewOnActiveRepository()` | `ActiveRepositoryUserControl().ActivateRevisionView()` | 无 |
| `ShowRevisionDetailsOnActiveRepository(RevisionDiffTarget)` | `ActiveRepositoryUserControl().ShowRevisionDetails(target)` | `RevisionDiffTarget`（Git） |
| `GetActiveRepositoryTempFileManager()` | `ActiveRepositoryUserControl().TempFileManager` | `TempFileManager`（根） |
| `GetActiveRepositoryGitModule()` | `ActiveRepositoryUserControl().GitModule` | `GitModule`（Git） |

WPF 实现（[WpfWindowManagerService.cs](file:///workspace/src/ForkPlus/Services/Wpf/WpfWindowManagerService.cs)）全部转发到 `MainWindow.Instance?.TabManager?.ActiveRepositoryUserControl?.X()`，与 `ApplicationExtensions.ActiveRepositoryUserControl()` 同源，逐方法 null-safe。

### 已迁移的 9 个 Command（14 → 5）

1. [UnpinReferenceCommand](file:///workspace/src/ForkPlus/UI/Commands/UnpinReferenceCommand.cs) — `InvalidateAndRefresh(SubDomain.ReferenceSettings)`
2. [PinReferenceCommand](file:///workspace/src/ForkPlus/UI/Commands/PinReferenceCommand.cs) — `InvalidateAndRefresh(SubDomain.ReferenceSettings)`
3. [ShowPreferencesWindowCommand](file:///workspace/src/ForkPlus/UI/Commands/ShowPreferencesWindowCommand.cs) — `InvalidateAndRefresh(SubDomain.Revisions)`（注：同文件 `MainWindow.Toolbar.RefreshUndoRedoVisibility()` 是另一处 View 耦合，留待 MainWindowViewModel）
4. [ShowRepositorySettingsWindowCommand](file:///workspace/src/ForkPlus/UI/Commands/ShowRepositorySettingsWindowCommand.cs) — `InvalidateAndRefresh(SubDomain.All)`
5. [RefreshRepositoryDataCommand](file:///workspace/src/ForkPlus/UI/Commands/RefreshRepositoryDataCommand.cs) — `InvalidateAndRefresh(SubDomain.All)`
6. [SwitchRevisionListOrientationCommand](file:///workspace/src/ForkPlus/UI/Commands/SwitchRevisionListOrientationCommand.cs) — `ActivateRevisionView()`
7. [CompareRevisionToWorkingDirectoryCommand](file:///workspace/src/ForkPlus/UI/Commands/CompareRevisionToWorkingDirectoryCommand.cs) — `ShowRevisionDetails(RevisionDiffTarget)`
8. [OpenFileInDefaultEditorCommand](file:///workspace/src/ForkPlus/UI/Commands/OpenFileInDefaultEditorCommand.cs) — `TempFileManager`
9. [ToggleHideTagsCommand](file:///workspace/src/ForkPlus/UI/Commands/ToggleHideTagsCommand.cs) — `GitModule` + `InvalidateAndRefresh`（合并迁移）

### 剩余 5 处（需具体 `RepositoryUserControl` 或 UI 层类型，留待 VM 抽取）

这些命令把 RUC 当作**参数**传给别的构造器/方法，或访问 UI 层类型（`RevisionSelector` / `RepositoryViewMode`）/深层 View 成员，无法用领域类型抽象干净，必须等对应 VM 抽出后用 VM 注入替换：

| 文件 | 耦合点 | 为什么不能现在抽象 |
|---|---|---|
| [ShowCreateTagWindowCommand](file:///workspace/src/ForkPlus/UI/Commands/ShowCreateTagWindowCommand.cs) | `InvalidateAndRefresh(SubDomain, new RevisionSelector.Sha(...))` | `RevisionSelector` 是 UI 层类型；且方法签名本就接收 `RepositoryUserControl` 参数 |
| [ShowRevisionInSeparateWindowCommand](file:///workspace/src/ForkPlus/UI/Commands/ShowRevisionInSeparateWindowCommand.cs) | `new RevisionDetailsWindow(RUC, ...)` | 把 RUC 作为 owner 传给 Window 构造器 |
| [ToggleReferenceFilterCommand](file:///workspace/src/ForkPlus/UI/Commands/ToggleReferenceFilterCommand.cs) | `RepositoryUserControl.Commands.UpdateReferenceFilter.ToggleActiveBranchFilter(RUC)` | 把 RUC 作为方法参数 |
| [ToggleShowReflogInRevisionListCommand](file:///workspace/src/ForkPlus/UI/Commands/ToggleShowReflogInRevisionListCommand.cs) | `ViewMode` / `Content.CommitUserControl.ToggleShowIgnoredFiles()` / `ShowReflogInRevisionList` setter | 深层 View 成员访问，`RepositoryViewMode` 是 UI 层类型 |
| [QuickLaunchWindow](file:///workspace/src/ForkPlus/UI/QuickLaunch/QuickLaunchWindow.xaml.cs) | `RepositoryData` / `GitModule` / `Command.Converter(args, RUC)` | 把 RUC 作为命令执行上下文，深度耦合 |

> 这 5 处的本质：RUC 同时承担"数据源 + 命令执行上下文 + Window owner"三重角色。只有把 RUC 拆出 `RepositoryUserControlViewModel`（本阶段核心任务），用 VM 替换这些传参，才能彻底消除。

### 验收

Commands 目录对 `Application.Current.ActiveRepositoryUserControl()` 的直访：**14 → 5**（剩余 5 处均标注了 VM 抽取后的替换路径）。

---

## 里程碑 3.1：抽取首个 ViewModel（CloneWindowViewModel）建立模式

> 状态：**已完成**（待 CI 验证）

阶段 3 的核心是 VM 抽取。本里程碑选最小且独立的 `CloneWindow` 作为首个目标，建立"VM 持纯状态/逻辑、View 仅绑定+转发"的模式，为后续大规模 VM 抽取定基调。

### VM 范围（[CloneWindowViewModel.cs](file:///workspace/src/ForkPlus/UI/Dialogs/CloneWindowViewModel.cs)）

**关键设计**：VM **零 WPF using**（无 `System.Windows.*`），满足阶段 3 验收标准——"把 `using System.Windows.*` 全删掉能编译通过"。

承接的纯业务逻辑：

| VM 成员 | 替换的 View 逻辑 | 类型来源 |
|---|---|---|
| `RepositoryUrl` / `RepositoryName` / `ParentDirectory` | 3 个 TextBox.Text 直访 | `string` |
| `IsSubmitAllowed`（计算属性） | `ForkPlusDialogWindow.IsSubmitAllowed` override | `bool` |
| `CommandPreview`（计算属性） | `ForkPlusDialogWindow.GetCommandPreview()` override | `string` |
| `DeriveRepositoryName(url)` 静态 | `RefreshRepositoryNameTextBox` 核心 | `GitUrl`（Git） |
| `GetNetworkProtocol(url)` 静态 | `RefreshNetworkProtocolButton` 核心 | `GitUrl.NetworkProtocol?`（Git） |
| `TryGetUrlFromClipboard()` 静态 | `TryParseUrlFromClipboard` | `ServiceLocator.Clipboard`（Services） |
| `RemoveGitClonePrefix(text)` 静态 | `RemoveGitClonePrefix` | `string` |

`INotifyPropertyChanged` 实现参考既有 [SshKeyViewModel](file:///workspace/src/ForkPlus/UI/Dialogs/SshKeyViewModel.cs) 模式（同目录，简单 INPC，无基类）。

### View 接入（[CloneWindow.xaml.cs](file:///workspace/src/ForkPlus/UI/Dialogs/CloneWindow.xaml.cs)）

1. 新增 `_viewModel` 字段（构造时实例化）。
2. `IsSubmitAllowed` / `GetCommandPreview()` 两个 override 改为：先 `SyncViewModelFromControls()`（把 3 个 TextBox.Text 推到 VM），再返回 VM 的计算属性。
3. `RefreshRepositoryNameTextBox` / `RefreshNetworkProtocolButton` / `TryParseUrlFromClipboard` 改为调用 VM 的对应静态方法，View 仅保留控件可见性/赋值。
4. 删除 View 中已迁走的 `RemoveGitClonePrefix`（逻辑唯一入口现是 VM）。

### 暂留 View 的逻辑（显式标注，留待后续迭代）

| 逻辑 | 为什么暂留 |
|---|---|
| `OnSubmit` | 调 `JobQueue.Add` / `MainWindow.ActiveRepositoryUserControl` / `Application.Current.TabManager().OpenRepository` / `Dispatcher.Invoke`，WPF 强耦合 |
| `TestButton_Click` | `BitmapImage` / `Dispatcher.Invoke` / 控件 Show/Hide |
| `AccountItem`（含 `ImageSource Icon`） | `ImageSource` 是 WPF 类型，放 VM 会破层；后续需改为图标 key 字符串 |
| `RefreshAccountsComboBox` / `RefreshUrlUserName` | 涉及 `AccountsComboBox` 控件操作 + `AccountItem` 构造，与上同 |
| `BrowseButton_Click` | `OpenDialog.SelectDirectory`（WPF 对话框封装） |

### 建立的模式（供后续 VM 抽取复用）

1. **VM 零 WPF using**：VM 只引用领域/根/Services 层类型，View 负责 WPF 类型适配。
2. **View 主动推送**：现阶段 View 在事件里把控件值 push 到 VM 属性（非双向绑定），降低改动面；后续整体切 Avalonia 时改双向绑定。
3. **override 委托**：基类虚方法（`IsSubmitAllowed`/`GetCommandPreview`）的 override 改为转发 VM，保持基类调用契约不变。
4. **静态纯函数**：无状态的推导逻辑（仓库名/协议/剪贴板解析）做成 VM 静态方法，View 直接调，避免为一次性计算创建实例状态。
5. **显式标注暂留项**：每个暂留 View 的 WPF 耦合点都在文档/注释里写明原因和后续路径，避免遗漏。

---

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
