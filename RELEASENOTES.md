# Release Notes

本文件记录 ForkPlus 各版本的变更。从 v1.3.0 开始，每次发布都会在此更新。

## v1.4.0

### 新功能：Git 命令预览

- 在所有对话框窗口（push/pull/fetch/stash/branch/tag/rebase/merge/cherry-pick/reset/clone/remote/submodule/worktree/gitflow/lfs 等 45 个窗口）底部添加 git 命令预览区域
- 用户在窗口中修改任何选项时，命令预览实时更新
- 预览区域显示完整的 git 命令（含参数），用 Consolas 等宽字体显示
- 关键参数未选择时预览区域自动隐藏
- 实现方式：在 `ForkPlusDialogWindow` 基类中添加公共命令预览基础设施（`GetCommandPreview` 虚方法 + `RefreshCommandPreview` 方法），各子窗口重写 `GetCommandPreview` 返回命令字符串
- 7 种语言 JSON 文件新增 "Git Command Preview" key

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
