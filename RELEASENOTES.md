# Release Notes

本文件记录 ForkPlus 各版本的变更。从 v1.3.0 开始，每次发布都会在此更新。

## v1.4.6

### 国际化补全

- **工具栏下拉菜单国际化**：`ToolbarUserControl` 中 Appearance（主题/语言/提交列表布局）、Stash（Recent Stashes / Save Snapshot...）、Workspaces 三个下拉菜单的硬编码英文文案改为 `PreferencesLocalization.Translate`。复用语言文件中已有的 10 个翻译 key（Theme / Light / Dark / Language / Commit List Layout / Horizontal / Vertical / Recent Stashes / Save Snapshot... / Workspaces），无需新增语言条目；foreach 循环变量重命名避免遮蔽外层 `language`。

### AI 检视：流式输出 + 超时处理

- **OpenAI HTTP 路径改用 SSE 流式输出**：`stream:true` + `HttpCompletionOption.ResponseHeadersRead` 立即返回响应头，逐行读取 `data:` 事件，每个 chunk 实时 `monitor.Append` 追加到检视窗口。解决此前整读模式下长输出"卡一段时间无任何输出"的体验问题。
  - 新增 `Connection.RequestStream`：流式 HTTP 请求，每收到一行重置空闲计时器（idle timeout），只有真正卡住才超时，避免长输出被误判超时。
  - 新增 `OpenAiService.OpenAiRequestStreamingWithRetry` / `OpenAiRequestStreaming` / `ParseSseLine` / `CreateChatStreamRequest`，与原非流式路径结构对称（queued wait / retry / cancel 检查）。SSE 行解析跳过空行、`:` 注释/keepalive、`[DONE]` 终止标记。
  - `GenerateCommitMessage` / `CodeReview` / `CodeReviewFiles` 改用流式版本，移除成功后整段 `AppendOutputLine`（流式已逐 chunk 追加）。
- **Claude CLI 路径新增超时处理**：`GenerateCommitMessageShellCommand` / `MakeCodeReviewShellCommand` 此前无超时，`claude.exe` 卡住时无限等待。新增 `System.Threading.Timer`，超时后调 `monitor.Cancel()` 杀进程并 `monitor.Fail` 提示"AI request timed out or was canceled."。复用 `AiReviewTimeoutSeconds` 设置（默认 300s，最小 10s），`finally` 中释放 Timer。

## v1.4.4

### 命令预览收尾——补全剩余弹窗

补全 6 个执行 git 命令但缺命令预览的弹窗（与 v1.4.0 引入的命令预览机制对齐，均为 `GetCommandPreview` 重写 + 构造函数末尾 `RefreshCommandPreview` 补刷）：

- **LeanBranchingStartWindow**：`git checkout -b <branch> <mainBranch>`（+可选 `git stash`）
- **LeanBranchingFinishWindow**：`git fetch` → `git checkout main` → `git merge <feature>`
- **InteractiveRebaseWindow**：`git rebase -i <destination>`
- **SaveSnapshotWindow**：`git stash push [--include-untracked] [-m "<msg>"]`（补 XAML 事件绑定）
- **GitLfsTrackWindow**：`git lfs track <patterns>`
- **AddGitIgnorePatternWindow**：`# .gitignore` + `git rm --cached -r .`

### Bug 修复

- **LeanBranchingStartWindow**：`FriendlyName` 改为 `Name`，规避 `Branch` 显式接口实现导致取到 `IFriendlyNamed.Name` 而非显示名的问题。
- **LeanBranchingFinishWindow**：修正构造函数括号结构（CS1513 编译错误）。
- `SideBySideMergeWindow`（全屏冲突解决器）与 `WelcomeWindow`（首次启动向导）不补——前者无合适 UI 位置且命令依赖运行时合并状态，后者非常规 git 操作弹窗。

## v1.4.3

### Bug 修复

- **新建分支/新建标签弹窗显示 git 命令预览**：`CreateBranchWindow` 与 `CreateTagWindow` 已实现 `GetCommandPreview` 重写及控件事件刷新，但构造函数末尾缺少 `RefreshCommandPreview()` 补刷（与重置/变基/删除分支弹窗同款 bug），`InitializeComponent` 期间控件未赋值导致首次预览被折叠。两处构造函数末尾各补刷一次。
- **删除分支弹窗显示 git 命令预览**：
  - `RemoveLocalBranchWindow`：构造函数末尾补刷 `RefreshCommandPreview()`；`GetCommandPreview` 的 `-d` 改为 `-D` 与实际 `RemoveLocalBranchGitCommand` 的 `--delete --force` 一致。
  - `RemoveRemoteBranchWindow`：新增 `GetCommandPreview()` 重写（`git push <remote> --delete refs/heads/<branch>`），构造函数末尾补刷。
- **"Cannot parse revision" 国际化 + AI 生成提交信息取消后仍写入**：
  - `ParseError.FriendlyDescription` 走 `PreferencesLocalization.Translate`，`"Cannot parse revision"` / `"Cannot parse revision details"` 在 7 个语言文件补齐翻译。
  - AI 生成提交信息（AiAgent/Claude 路径）Dispatcher 回调补 `monitor.IsCanceled` 检查：Claude 输出缓冲到进程结束才返回，期间用户点取消后，已返回内容仍被写入 commit 信息文本框。与 OpenAI 路径对齐。
  - `prepare-commit-msg` hook 回调同样补 `monitor.IsCanceled` 检查（同类缺陷）。

## v1.4.2

### Bug 修复

- **git mm 下拉框两行 / 交互式变基闪退 / "在文件树中显示"闪退**：
  - git mm 下拉框两行：`GetGitMmVersionShellCommand` 取版本输出首行，去除内嵌换行（版本号 + build info 多行输出污染下拉框 label）。
  - 交互式变基弹窗闪退：`PrepareTodoListForRebase` 中 `Close()` 后补 `return`，避免 `todoListResult.Result` 为 null 时 `.Reverse()` 抛 NRE，经 `Dispatcher.Invoke` 传播到 IPC 后台线程导致进程崩溃。
  - 右键"在文件树中显示"闪退：`RevisionFileTreeUserControl.Refresh` 异步设置 `RootItem`，`ShowRevisionDetails` 同步访问 null。新增 `_pendingFilePath` 延迟展开模式，`RootItem` 就绪后再展开。
- **追溯/历史弹窗显示 "Cannot parse revision"（Windows `\r\n` 问题）**：Windows 上 git 输出使用 `\r\n` 行尾，而 `GetFileHistoryGitCommand` 用 `Split('\n')` 分割后每行末尾残留 `\r`。`Sha.TryParse` 要求恰好 40 字符，`"sha\r"` 变成 41 字符导致解析失败。修复：在 split/搜索前将 `\r\n` 统一替换为 `\n`。同时移除冗余的 `--oneline` 选项。
- **变基弹窗默认不显示 git 命令预览**：与 `ResetBranchWindow` 同样的时序问题——`InitializeComponent` 期间 `AddCommandPreview` 已执行，但此时 `_destination` 及复选框状态尚未赋值，导致 `GetCommandPreview` 返回 null 折叠了预览区。修复：构造函数末尾补刷一次 `RefreshCommandPreview`。
- **重置分支弹窗默认不显示 git 命令预览**：同款时序问题。修复：在构造函数末尾（`_destination` 赋值后）补刷一次 `RefreshCommandPreview`，使默认 Mixed 重置命令正常显示。
- **追溯/历史弹窗显示类型名而非错误描述**：`BlameWindow` 和 `FileHistoryWindow` 的 `ShowErrorFallback` 调用 `error.ToString()`，而 `ParseError` 等子类未重写 `ToString`，默认返回类型全名。修复：在基类 `GitCommandError` 重写 `ToString` 返回 `FriendlyDescription`，所有未自行重写 `ToString` 的子类都受益。

## v1.4.1

### 新功能

- **git 命令预览复制按钮**：git 命令预览右侧新增复制图标按钮（矢量 Path 绘制），ToolTip 复用 "Copy to clipboard" 国际化文案，点击复制预览命令到剪贴板。

### 国际化

- **git-mm Instance 标签国际化**：7 个语言文件补齐 `"git-mm Instance:"` 翻译 key。
- **远端右键菜单 Edit/Delete 'xxx' 国际化**：`SidebarUserControl` 中远端仓库右键菜单的 `"Edit 'xxx'..."` 和 `"Delete 'xxx'..."` 此前是硬编码英文字符串拼接，改为 `PreferencesLocalization.FormatCurrent`，并补齐 7 个语言文件的 `"Edit '{0}'..."` 翻译 key（`"Delete '{0}'..."` 已存在）。

### Bug 修复

- **偏好设置打开卡顿（误判与 revert）**：曾尝试取消偏好设置中 git mm 版本判断（`GitMmVersionText` 短路返回 null）以消除同步启动 `git-mm.exe --version` 子进程阻塞 UI 线程导致的卡顿；经确认卡顿非版本判断导致，已 revert 恢复 `GitMmVersionText` 原始实现。同时修复了版本输出含内嵌换行导致下拉框每项显示两行的问题。

## v1.4.0

### 新功能：Git 命令预览

- 在所有对话框窗口（push/pull/fetch/stash/branch/tag/rebase/merge/cherry-pick/reset/clone/remote/submodule/worktree/gitflow/lfs 等 45 个窗口）底部添加 git 命令预览区域
- 用户在窗口中修改任何选项时，命令预览实时更新
- 预览区域显示完整的 git 命令（含参数），用 Consolas 等宽字体显示
- 关键参数未选择时预览区域自动隐藏
- 实现方式：在 `ForkPlusDialogWindow` 基类中添加公共命令预览基础设施（`GetCommandPreview` 虚方法 + `RefreshCommandPreview` 方法），各子窗口重写 `GetCommandPreview` 返回命令字符串
- 7 种语言 JSON 文件新增 "Git Command Preview" key

### Bug 修复：CI 构建失败

- **DeleteWorktreeWindow**：`GetCommandPreview` 中 `Worktree`（struct）与 null 比较导致 CS0019 编译错误。移除多余的 null 检查。
- **CheckoutRevisionWindow**：`GetCommandPreview` 中 `Sha`（struct）与 null 比较导致 CS8073 警告。改为与 `Sha.Zero` 比较。

### Bug 修复：打开偏好设置异常（git mm 相关）

- **根因**：`RefreshGitMmInstanceComboBox` 未找到 git-mm 时将 `SelectedItem` fallback 到 AddCustom 项，触发 `SelectionChanged` 在 `PreferencesWindow` 构造期间弹出文件选择对话框。
- **修复**：未找到 git-mm 时 `SelectedItem` 设为 null（不选中任何项），不 fallback 到 AddCustom。
- **修复**：`GitMmInstanceComboBox_SelectionChanged` 添加 `_isRefreshingGitMm` 守卫标志，刷新期间跳过副作用逻辑（弹文件对话框/写磁盘）。
- **优化**：`SelectionChanged` 中选择 System/Local/Custom 时移除 `Save()` 调用（仅赋值不立即写磁盘，避免每次打开偏好设置都触发磁盘写入）。
- **优化**：`RefreshGitMmInstanceComboBox` 使用 `App.GitMmPathFromPath`（带缓存的 PATH 查找），避免直接调 `FindExecutableInPath` 绕过缓存导致重复遍历 PATH。

## v1.3.4

### Bug 修复：所有 push 操作报 "src refspec xxx does not match any"

- **根因**：`PushGitCommand` 走 `ExecuteWithCallbackBt`（argv 数组传参，每个参数独立，不做 shell 解析），但代码仍用 `Quotify()` 给 `remote` 和 refspec 包裹双引号。导致 git.exe 收到字面量 `"origin"`（含双引号）作为 remote 名，找不到该 remote，refspec 解析失败，报 `src refspec "refs/heads/xxx" does not match any`。
- **修复**：移除 `PushGitCommand` 中 5 处 `Quotify()` 调用（主重载 4 处 + LeanBranching 重载 2 处），与同走 `ExecuteWithCallbackBt` 的 `PushTagGitCommand`/`PushMultipleBranchesGitCommand`/`PushMultipleTagsGitCommand` 保持一致（它们本来就不用 Quotify）。
- **影响范围**：所有走 `PushGitCommand` 的 push 操作（PushWindow 推送分支、QuickPush、CreatePullRequest、LeanBranching Step3）。
- **说明**：`Quotify` 仅适用于 `ExecuteWithCallback`（无 Bt 后缀，走 `ProcessStartInfo.Arguments` 拼接，CreateProcess 解析需要双引号）路径。`ExecuteWithCallbackBt`（Bt 后缀，走原生 argv 数组）路径不应使用 Quotify。

## v1.3.3

### 性能优化：启动速度

- **合并重复的 git version 子进程**：`IsGitInstanceAvailable()` 移除子进程调用，仅 `File.Exists` 检查；版本检测统一由 `WarnIfGitVersionUnsupported` 完成。原实现启动时执行 2 次 `git version` 子进程，现仅 1 次。
- **缓存 `App.GitMmPath` 的 PATH 遍历结果**：`FindExecutableInPath("git-mm.exe")` 结果缓存到静态字段，避免每次访问 `App.GitMmPath` 都遍历整个 PATH。
- **git-mm 检测改为后台线程 + 异步弹窗**：`WarnIfGitMmUnavailable` 整体放到 `Task.Run`，`ErrorWindow.ShowDialog` 用 `Dispatcher.BeginInvoke` 延迟到 UI 线程异步弹出。原实现同步弹模态对话框会阻塞 `RestoreSession`。

### Bug 修复：窗口状态恢复

- **修复窗口位置/大小/状态不按上次保存恢复**：
  - `OnSourceInitialized`：先设置 WPF 依赖属性 `Left/Top/Width/Height`，再调 `SetWindowPlacement`。原实现只调 Win32，WPF 在 `Show()` 流程中用 XAML 默认值（`Width=1000/Height=600`）覆盖了恢复的位置/尺寸。
  - 统一 `GetWindowLocationState`：删除最小化时的特殊分支（用 WPF `window.Left/Top`，最小化时是系统幽灵值 -32000），改为始终用 `placement.normalPosition`（还原矩形）。
  - 新增 `OnStateChanged`：纯状态切换（最大化↔正常不伴随尺寸变化）现在也会保存状态。

### 国际化补全

- 补全 18 个未本地化的命令 Title（菜单/右键菜单显示英文）：
  - Remote：`Edit Remote...`、`Add New Remote...`
  - Branch：`Start Branch...`、`Finish Branch...`、`Rebase Branch`、`Interactive Rebase Branch`、`Checkout Branch as Worktree...`
  - Tag：`Push Tag...`、`Push Tags...`、`Show Annotated Tag Details...`（同时修正拼写：`Annoted` → `Annotated`）
  - Worktree：`Open Worktree In New Tab`
  - 其他：`Switch orientation`（修正大小写与已有 key 对齐）、`Fast-Forward Pull`、`Activate Search Navigator`、`Stage/Unstage File`、`Ai Result...`、`Send Crash Report`、`Merge...`（修正尾随空格与已有 key 对齐）
- 7 种语言（zh-Hans/zh-Hant/ja-JP/ko-KR/fr-FR/de-DE/es-ES）各新增 16 个 key

## v1.3.2

### Bug 修复

- **修复新文件详情页显示原始 diff 头部的 bug**：`git diff` 退出码 1（有差异）被误判为失败，导致 diff 头部文本（`diff --git ... new file mode ... index ...`）被当作错误信息显示。改为 `ExitCode >= 2` 才判定为真实错误。
  - `GetWorkingDirectoryFileChangesGitCommand`：3 处 diff 命令（ExecuteInternal / GetStagedPatch / GetChangesAsBinaryPatchInternal）
  - `GetRevisionFileChangesGitCommand`：1 处 diff 命令（ExecuteInternal）
- **修复 `PatchParser.Parse` 返回 null 导致 NRE**：当 Biturbo 原生 tokenizer 失败时，之前返回 null 导致所有调用方访问 `.Succeeded` 抛 `NullReferenceException`。改为返回 `Failure`。

## v1.3.1

### git mm 版本检测（按需提示）

- 新增 `GitMmVersionChecker` + `GetGitMmVersionShellCommand`，执行 `git-mm.exe --version` 解析版本号，最低要求 3.0.0
- 新增 `App.GitMmPath` 属性：用户设置 → PATH 查找 → git.exe 同目录三级回退；`FindExecutableInPath` 辅助方法
- `ForkPlusSettings` 新增 `GitMmInstancePath` 字段持久化用户选择的 git-mm 路径
- 偏好设置 Git 选项卡新增 git-mm 实例选择下拉框：自动发现 PATH 与 git 同目录的 `git-mm.exe`、支持手动添加自定义路径
- **检测时机**：仅当用户打开 git mm 仓库（`GitMmUserControl` 构造）时才检测 git-mm 是否存在及版本是否满足 3.0，缺失或版本过低才弹 `ErrorWindow`；启动和偏好设置中不再打扰不使用该功能的用户
- 新增 7 个本地化 key 补全 7 种语言，8 个 README 环境要求补充 git-mm 3.0

## v1.3.0

### Git 命令健壮性

- 修复 `Quotify()` 未转义参数内嵌引号的问题，杜绝参数注入与命令拼接错误
- 修复 `GetChangedFilesGitCommand` 解析 Copied/Renamed 状态时越界访问 `array[i+1]` 导致崩溃
- `GetWorkingDirectoryFileChangesGitCommand` 改用 `gitRequestResult.Success` 判断失败，不再依赖 stderr 字符串匹配
- `CommitGitCommand` 写入提交信息时显式使用 UTF-8 无 BOM 编码，避免非 ASCII 提交信息乱码
- `GetFileHistoryGitCommand` 的 `-L` 参数路径加引号转义
- `PushGitCommand`/`PullGitCommand`/`CheckoutBranchGitCommand`/`FetchGitCommand`/`CreateNewBranchGitCommand` 的分支名、远程名、refspec 统一通过 `Quotify()` 包裹
- `GetRecentRevisionsGitCommand` 区分空仓库与真实错误，空仓库返回空数组，其他错误记录日志

### Bug 修复

- `Connection.cs`：修复 `HttpClientHandler.UseCookies` 配置顺序；`IsJsonError` 增加空安全；`HttpRequestMessage`/`HttpResponseMessage` 用 `using` 包裹，避免 socket 与内存泄漏
- 12 处 `async void` 事件处理器补充 try/catch，防止未捕获异常终止进程（涉及 Clone、Rescan、Welcome、FileHistory、SaveAsPatch、GenerateSshKey、GitUserControl、CommitUserControl、RepositoryDetailsUserControl 等窗口）
- `FileHelper.OpenInWindowsExplorer` 改用 `Process.Start(ProcessStartInfo)`，避免未 Dispose 的 Process 对象泄漏

### 性能优化

- `GitMmUserControl.RefreshSubrepoRuntimeState`：subrepo 状态查询从串行改为最多 4 路并发并行；单 subrepo 的 `status` + `branch --show-current` + `rev-list --left-right --count` 三次 git 调用合并为一次 `git status -b --porcelain`，50+ 仓库刷新耗时显著下降
- `RefreshSubrepoSummary` 从 6 次 O(N) 遍历改为 1 次遍历累加
- `RevisionFileTreeUserControl.Refresh` 异步化 `git ls-tree` 调用，不再阻塞 UI 线程
- `RevisionChangesUserControl.UpdateDiff` 异步化 diff 计算，加入请求序号守卫丢弃过期结果，大文件 diff 不再卡顿

### 国际化

- 修复 9 处 `ErrorWindow` 字符串拼接，改为 `FormatCurrent` 模板化翻译
- 修复 12 处 `monitor.Fail` 原始英文字符串，改为 `Current` 本地化
- 新增 11 个翻译 key，同步补全简体中文、繁體中文、日本語、한국어、Français、Deutsch、Español 七种语言

### git mm 版本检测

- 启动时检测 `git-mm.exe` 版本，低于 3.0 弹警告（未找到也提示）
- 偏好设置 Git 选项卡新增 git-mm 实例选择下拉框，支持自动发现 PATH 与 git 同目录的 `git-mm.exe`、手动添加自定义路径、选择后即时版本校验
- 新增 `GitMmInstancePath` 设置项持久化用户选择的 git-mm 路径
- 新增 7 个本地化 key 补全 7 种语言
