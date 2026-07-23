# 阶段 3：ViewModel 抽取

> 状态：**Dialog 窗口 VM 抽取已完成**（48 个 ForkPlusDialogWindow 子类全部抽取）+ **AI 系列窗口 VM 抽取已完成**（3 个 WebView2 窗口），CI 三平台全绿
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

## 里程碑 3.2–3.13：批量抽取全部 48 个 Dialog Window ViewModel

> 状态：**已完成**（CI 三平台全绿）

在 3.1 建立的模式基础上，对全部 48 个含 `IsSubmitAllowed` override 的 `ForkPlusDialogWindow` 子类（40 个在 `Dialogs/` + 8 个在 `Dialogs/Accounts/`）批量抽取 ViewModel。每个 VM **零 WPF using**，View 仅负责绑定+转发+UI 副作用。

### 9 个 VM 抽取模式点（横向复用）

| # | 模式点 | 适用场景 | 代表 VM |
|---|---|---|---|
| 1 | VM 零 WPF using | 所有 VM | CloneWindowViewModel |
| 2 | View 主动推送（PushSelectionToViewModel） | 控件值→VM 属性 | CreateBranchWindowViewModel |
| 3 | override 委托（IsSubmitAllowed/GetCommandPreview 转发 VM） | 所有 Dialog | PullWindowViewModel |
| 4 | SetStatus 拆分（Status+Message 由 VM 返回，View 调 SetStatus） | 有校验状态的窗口 | EditRemoteWindowViewModel |
| 5 | base.IsSubmitAllowed 保留（!IsOperationInProgress） | 简单提交判定 | PushWindowViewModel |
| 6 | 复选框/选项投影为 bool | 多选项窗口 | CherryPickWindowViewModel |
| 7 | Validate() 三元组 (IsAllowed, Status, Message) | 有校验逻辑的窗口 | CreateBranchWindowViewModel |
| 8 | 命令预览纯函数（CommandPreview 计算属性） | 所有有 GetCommandPreview 的窗口 | ConfigureGitInstanceWindowViewModel |
| 9 | Validate() 4 元组 with RequiresTranslation | 校验消息需国际化的窗口 | LeanBranchingFinishWindowViewModel |

### 里程碑总览

| 里程碑 | VM 数量 | 代表 VM | 模式点 |
|---|---|---|---|
| 3.1 | 1 | CloneWindowViewModel | 1-5（建立模式） |
| 3.2–3.5 | 9 | Fetch/RenameLocalBranch/GitLfs*/SshPassphrase/PushTag/PushMultipleTags/RenameStash/Welcome | 1-8 |
| 3.6–3.8 | 12 | GitFlowStart*/ForkSyncCheck/GenerateNewSshKey/AddGitIgnorePattern/GitLfsTrack/ChangeRemoteTracking/CheckoutBranchAsWorktree + Accounts/ 8 个登录窗口 | 1-8（含 Validate 三元组） |
| 3.9 | 5 | RevertRevision/GitFlowInit/CreateTag/ApplyPatch/AddSubmodule | 9（Validate() 4 元组 with RequiresTranslation） |
| 3.10 | 5 | InitGitMmRepository/CreateWorktree/CherryPick/CreatePartialStash/LeanBranchingFinish | 9（LeanBranchingFinish 依赖运行时 git 命令校验） |
| 3.11 | 5 | Pull/GitMmStart/ConfigureGitInstance/TrackRemoteBranch/AddGitignoreTemplate | 9（TrackRemoteBranch 重名消息含 {0} 占位符） |
| 3.12 | 4 | LeanBranchingStart/CreateBranch/EditRemote/Push | 7/9/5（Push 保留 base.IsSubmitAllowed；嵌套类留 View） |
| 3.13 | 2 | InteractiveRebase/SideBySideMerge | 3/5（重度 WPF 逻辑全留 View，VM 仅持判定+预览） |
| **合计** | **48** | | |

### 关键技术决策

1. **WPF 嵌套类留 View**：`RemoteItem`/`RemoteBranchItem`（PushWindow）、`AccountItem`（EditRemoteWindow）、`GitMmSubrepoItem`（GitMmStartWindow）等嵌套类含 WPF `ImageSource`/`Visibility`，整体留 View 作列表项；VM 仅持选中状态纯数据投影（View 用 `.Select(s => s.Name).ToArray()` 投影为纯字符串列表后推入 VM）。

2. **RequiresTranslation 双用法**：`PreferencesLocalization`（WPF 类型）不可被 VM 引用，第 9 模式点用 `RequiresTranslation` 标志让 View 决定是否翻译。
   - LeanBranchingFinish/TrackRemoteBranch：VM 端已 `string.Format` 填充占位符，View 用 `Translate(message)` 直接翻译。
   - LeanBranchingStart：VM 端未填充占位符（保留 `"Branch '{0}' already exists"` 原文），View 用 `string.Format(Translate(statusMessage), BranchNameTextBox.Text)` 双重处理。
   - CreateBranch：走三元组模式，重名消息原文不翻译（保留原始行为差异）。

3. **扩展方法命名空间陷阱**：`LocalMain`/`Upstream`（RepositoryReferences 扩展方法）和 `AreInSync`（BehindAheadCount 扩展方法）定义在 `ForkPlus.Git.Commands.LeanBranching` 命名空间，VM 需显式 `using`。

4. **重度 WPF 窗口最小抽取**：InteractiveRebaseWindow（IPC/ObservableCollection/Adorner/Semaphore）和 SideBySideMergeWindow（AvalonEdit/MergeCodeEditor/MergeConflictView）仅抽取 `IsSubmitAllowed` 判定 + `CommandPreview`，重度 WPF 逻辑全留 View。

### 验收

- `grep -r "System.Windows" *WindowViewModel.cs` → 零匹配（所有 VM 零 WPF using）
- 全部 48 个 `IsSubmitAllowed` override 均委托 VM
- CI 三平台（Windows/Linux/macOS）全绿

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

### Dialog 窗口（VM 已抽取，剩余 View 侧清理）

- [x] `CloneWindowViewModel` — 里程碑 3.1 完成
- [x] `SideBySideMergeWindowViewModel` — 里程碑 3.13 完成（IsSubmitAllowed 判定已抽 VM；MessageBox/diff 行动态生成等 View 侧清理留后续迭代）
- [x] 全部 48 个 `ForkPlusDialogWindow` 子类的 `IsSubmitAllowed` / `GetCommandPreview` 已委托 VM

- [ ] `StatisticsUserControlViewModel`
  - 从 `StatisticsUserControl.xaml.cs` 抽出 `AuthorStatViewModel` / `CodeLineLanguageViewModel`（当前是 nested class 第 28-71 行）→ 独立 VM
  - `PlotHelper.CreateLinePlotModel()` → VM 暴露 `PlotModel` 属性（OxyPlot 平台无关部分）

- [ ] `CustomColorsDialogViewModel`
  - 12 处 `MessageBox.Show` → `ServiceLocator.MessageBox`
  - 颜色行动态生成 → `ObservableCollection<ColorItemVm>` + `DataTemplate`

- [ ] `MergeConflictUserControlViewModel`
  - 8 处 `MessageBox.Show` → `ServiceLocator.MessageBox`
  - diff 行动态生成 → `ObservableCollection<DiffLineVm>` + `DataTemplateSelector`（按 `LineType` 选模板）

### 阶段 0 推迟的接口（本阶段补）

- [x] `IWebViewFactory` / `IMarkdownWebView` → **改为 VM 基类方案（里程碑 3.14）**
  - 原计划设计 `IMarkdownWebView` 接口抽象 WebView2，但 AI 窗口流式渲染逻辑高度重复且紧耦合 `NavigateToString`/`CoreWebView2`，接口抽象收益低于复杂度成本
  - 实际方案：抽取 `AiStreamingMarkdownViewModel` 基类承载流式状态+协议+Markdown转换+CSS（零 WPF），View 保留 WebView2 实例操作。VM 只管"输出 HTML 字符串"，View 负责 `NavigateToString`
  - 详见里程碑 3.14

## 风险点

- **WebView2 是最大单点风险**：Avalonia 无官方 WebView。阶段 3 先抽 VM（里程碑 3.14 已完成），WebView2 暂留 WPF View，等 VM 稳定后阶段 4 再决定替代方案（CefGlue / AvaloniaEdit 渲染 Markdown）
- **静态全局访问**：`MainWindow.Instance` / `Application.Current.MainWindow` / `MainWindow.ActiveRepositoryUserControl` 共 74 处，需在 VM 化过程中逐步注入
- **x:Name 直访**：176 处 `x:Name` + 事件，需逐个评估改 Binding+Command 还是保留事件（配合 VM）
- **Dispatcher 直调**：29 个文件直接用 WPF Dispatcher，需替换为 `ServiceLocator.Dispatcher`

---

## 里程碑 3.14：AI 系列窗口 VM 抽取（第 10 个模式点：流式渲染 VM 基类）

> 状态：**已完成**（commit `193f707` → `d048933` → `41234dd`，CI 三平台全绿）

AI 系列窗口是阶段 3 中难度最高的部分（WebView2 + 流式渲染 + 多对话状态）。5 个 AI 窗口中 3 个使用 WebView2 流式渲染，其流式逻辑高度重复（`_streamingMarkdown` + `_streamingLock` + 节流 400ms + 滚动跟踪 + Markdown→HTML + CSS），是明显的抽取目标。

### 关键决策：用 VM 基类替代 IWebViewFactory 接口

原计划（阶段 0 推迟项）设计 `IMarkdownWebView` 接口抽象 WebView2。但调研发现 AI 窗口流式渲染紧耦合 `NavigateToString`/`CoreWebView2`，接口抽象收益低于复杂度成本。改为抽取纯 VM 基类承载流式状态+协议+Markdown转换+CSS，View 保留 WebView2 实例操作。

### 新建 4 个 VM 文件（零 WPF）

| 文件 | 职责 |
|---|---|
| [AiStreamingMarkdownViewModel.cs](file:///workspace/src/ForkPlus/UI/Dialogs/AiStreamingMarkdownViewModel.cs) | 基类：流式缓冲+节流协议+滚动跟踪+Markdown→HTML(native Bt)+CSS加载+HTML文档构建+scrollScript注入 |
| [AiModelListLoader.cs](file:///workspace/src/ForkPlus/UI/Dialogs/AiModelListLoader.cs) | 静态助手：三窗口共有的模型下拉后台拉取逻辑（OpenAiService.ListModels）+ CurrentModel 持久化 |
| [AiTextResultWindowViewModel.cs](file:///workspace/src/ForkPlus/UI/Dialogs/AiTextResultWindowViewModel.cs) | 继承基类，+ModelListLoaded |
| [AiCodeReviewWindowViewModel.cs](file:///workspace/src/ForkPlus/UI/Dialogs/AiCodeReviewWindowViewModel.cs) | 继承基类，+ModelListLoaded |
| [AiDevelopmentWindowViewModel.cs](file:///workspace/src/ForkPlus/UI/Dialogs/AiDevelopmentWindowViewModel.cs) | 继承基类，+ModelListLoaded |

### 接线 3 个 WebView2 窗口

| 窗口 | 行数 | 特点 | 接线方式 |
|---|---|---|---|
| [AiTextResultWindow](file:///workspace/src/ForkPlus/UI/Dialogs/AiTextResultWindow.xaml.cs) | 484 | 最简单，单 WebView，完整 scroll-at-bottom 跟踪 | 全套 VM 协议 |
| [AiCodeReviewWindow](file:///workspace/src/ForkPlus/UI/Dialogs/AiCodeReviewWindow.xaml.cs) | 1758 | suggestion 列表 + 文件 review + diff 缓存 | 流式部分用 VM，业务逻辑留 View |
| [AiDevelopmentWindow](file:///workspace/src/ForkPlus/UI/Dialogs/AiDevelopmentWindow.xaml.cs) | 2141 | 最复杂，多对话气泡 + 工具调用循环 + 文件变更/撤销 | 流式缓冲用 VM，无 scroll-at-bottom（气泡自动高度），业务逻辑留 View |

### 第 10 个模式点：流式渲染 VM 基类

VM 承载（零 WPF）：
- 流式状态：`_streamingMarkdown`(StringBuilder) + `_streamingLock` + `_lastStreamingRenderUtc` + `StreamingRenderIntervalMs`(400) + `_streamingActive` + `_streamingUserAtBottom` + `_pendingStreamingScrollToEnd`
- 流式协议：`OnChunk(chunk)`→(shouldRender,lengthSoFar) / `ShouldRenderNow()` 节流判定 / `GetMarkdownSnapshot()` / `StopStreaming()` / `ResetForNewRequest()` / `SetUserAtBottom()` / `RequestScrollToEndIfNeeded()` / `ConsumeScrollToEndRequest()` / `ClearStreamingBuffer()` / `GetFinalMarkdown()`
- Markdown→HTML：`ConvertMarkdownToHtml(markdown)`（native Bt.bt_md_to_html）
- CSS：`GetCss()`（嵌入资源 md-ai-output.css，静态缓存）
- HTML 文档：`BuildHtmlDocument(body)` / `RenderMarkdownToHtmlDocument(md)` / `RenderMarkdownToHtmlDocumentWithScrollScript(md)` / `BuildErrorHtmlDocument(msg)` / `BuildScrollScript()`
- 消息解析：`TryParseScrollMessage(message)`→bool?

View 保留（WPF 职责）：
- WebView2 实例创建/初始化/事件订阅（`EnsureCoreWebView2Async` / `ContextMenuRequested` / `WebMessageReceived` / `NavigationCompleted`）
- `NavigateToString(html)`
- `Dispatcher.Async` 调度（VM.OnChunk 被 UI 线程外调用，View 负责 marshal 回 UI 线程渲染）
- Visibility / ProgressBar / StatusText / BusyIndicator 切换
- ComboBox.Items 填充
- Clipboard.SetText
- PreferencesLocalization（WPF 类型）
- 业务逻辑（suggestion 交互 / 文件 review / 工具调用 / 文件变更 / 撤销）

### CI 修复记录

- `193f707` 推送后 Windows build 报 `CS0050`：`AiCodeReviewWindowViewModel.CreateAiService()` 返回 internal `OpenAiService`，public 方法返回 internal 类型。修复（`8af8db4`）：删除该封装方法，View 直接用 OpenAiService
- `8af8db4` 推送后 Windows build 报 `CS0246`：`AiModelListLoader.cs` 缺 `ServiceResult<>` 的 using。修复（`d048933`）：补 `using ForkPlus.Utils.Http`

### 未抽取的 2 个 AI 辅助窗口（评估后决定不抽）

- [AiCommitComposerWindow](file:///workspace/src/ForkPlus/UI/Dialogs/AiCommitComposerWindow.xaml.cs)（542行）：继承 CustomWindow，无 WebView2/无流式，WIP 提交分组编辑，逻辑紧密围绕 ListBox/TextBox UI，抽取价值低
- [AiSuggestionPreviewWindow](file:///workspace/src/ForkPlus/UI/Dialogs/AiSuggestionPreviewWindow.xaml.cs)（134行）：继承 ForkPlusDialogWindow 但无 IsSubmitAllowed override，纯 diff 预览，不在抽取范围

## 后续阶段衔接

阶段 3 完成后，VM 层纯 C# 不依赖任何 UI 框架，切 Avalonia 只是"换基类 + 换 XAML 命名空间 + 换第三方库"。
