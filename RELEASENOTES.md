# Release Notes

本文件记录 ForkPlus 各版本的变更。从 v1.3.0 开始，每次发布都会在此更新。

## v1.3.1

### git mm 版本检测

- 启动时检测 `git-mm.exe` 版本，低于 3.0 弹警告（未找到也提示）
- 偏好设置 Git 选项卡新增 git-mm 实例选择下拉框，支持自动发现 PATH 与 git 同目录的 `git-mm.exe`、手动添加自定义路径、选择后即时版本校验
- 新增 `GitMmInstancePath` 设置项持久化用户选择的 git-mm 路径
- 新增 7 个本地化 key 补全 7 种语言
- 8 个 README 环境要求补充 git-mm 3.0

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
