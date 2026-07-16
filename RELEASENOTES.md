# Release Notes

本文件记录 ForkPlus 各版本的变更。从 v1.3.0 开始，每次发布都会在此更新。

## v1.6.4

### 修复：仓库树图点击崩溃

- **现象**：在 Repository Overview 窗口（`Repository Treemap`）点击某文件夹后，应用可能整体崩溃；或在打开窗口"正在加载"完成后过一会儿崩溃。
- **根因 1（点击阶段）**：`RepositoryOverviewWindow` 的 `Treemap.SelectionChanged` 委托无 try/catch；点击后调用 native `bt_get_revision_headers` 返回的 header 数量与传入的 SHA 数量不一致时（悬挂对象 / shallow clone / biturbo 缓存与仓库状态不同步），`gitCommandResult.Result[i]` 抛 `IndexOutOfRangeException`，未捕获异常冒到 WPF Dispatcher 导致应用级崩溃—— [RepositoryOverviewWindow.xaml.cs](file:///workspace/src/ForkPlus/UI/Dialogs/RepositoryOverviewWindow.xaml.cs)
- **根因 2（渲染阶段）**：`Treemap.CalculateLayout` 调用 biturbo 三方件 native `Bt.bt_layout_treemap` 计算布局，失败时直接 `throw new Exception`。该 throw 发生在 `OnRender` 渲染期间（`RefreshData` 设置 `DataSource` → 触发 `InvalidateVisual` → `OnRender` → `CalculateLayout`），异常冒到 WPF 渲染线程导致应用崩溃。这是"加载完成后过一会儿崩溃"的真正原因—— [Treemap.cs](file:///workspace/src/ForkPlus/UI/Controls/Treemap.cs)
- **修复**：
  - **根因 1**：在 `gitCommandResult.Succeeded` 后追加 `Result.Length == shas.Length` 校验，不匹配时跳过提交列表更新并记录日志；整个 `SelectionChanged` 委托包 try/catch 兜底
  - **根因 2**：`CalculateLayout` 里 native `bt_layout_treemap` 失败时不抛异常，改为返回空 `LayoutItem[]` + 记录日志；`RecalculateLayoutIfNeeded` 与 `OnRender` 均包 try/catch 兜底，确保渲染期间任何异常都不会冒到 WPF 渲染线程
  - 新增 i18n key `"Revision header count mismatch"` 用于错误日志
- **三方件说明**：根因 2 的触发条件是 biturbo 三方件（`biturbo.dll`）的 native `bt_layout_treemap` 返回失败，可能因仓库文件数过多、值异常或 native 内部 bug。本次修复让 managed 层在 native 失败时降级为空白显示而非崩溃，但 native 失败的根因仍在 biturbo 三方件侧。

### 增强：贡献热力图加图例与统计摘要

- **需求**：v1.6.3 的贡献热力图只有 5 级色阶格子，缺少色阶说明与汇总指标，信息密度不足。希望像 GitHub 个人主页那样在热力图下方加一行图例 + 统计摘要。
- **实现**：
  - **重构 `ContributionHeatmap` 控件布局**：外层 Grid 改为两行——Row0 是热力图子 Grid（53×7），Row1 是水平 StackPanel（左侧图例 + 右侧摘要）—— [ContributionHeatmap.cs](file:///workspace/src/ForkPlus/UI/Controls/ContributionHeatmap.cs)
  - **图例**：`Less` + 5 个 10×10 色块（与热力图同色阶，主题切换时同步刷新）+ `More`，色块颜色在 `RebuildCells` 末尾随 palette 一起刷新
  - **统计摘要**：单行 TextBlock，三段用 `·` 分隔——
    - `Total: {0}` — 当前数据范围内总提交数
    - `Longest streak: {0} days` — 最长连续提交天数（按日期升序遍历，gap == 1 天且当天有提交则累计）
    - `Most active: {0} ({1})` — 提交数最多的那天（YYYY-MM-DD）及其提交数
    - 任意指标为 0 时省略对应段（如无提交时只显示 Total: 0）
  - **i18n**：5 个新 key 同步到 7 语言——`Less`、`More`、`Total: {0}`、`Longest streak: {0} days`、`Most active: {0} ({1})`

## v1.6.3

### 新增：贡献热力图（GitHub 风格 53 周 × 7 天）

- **需求**：统计面板希望加一张 GitHub 个人主页那种"提交热力图"，一眼看出近一年的提交活跃度分布。
- **实现**：
  - **数据源**：`GetRepositoryStatsGitCommand.Execute` 在原有按月/按周/按小时聚合的循环里，顺手按 `authorDate.Date` 聚合一份 `Dictionary<DateTime, DayContributionInfo> CommitsByDate`，零额外 git 调用—— [GetRepositoryStatsGitCommand.cs](file:///workspace/src/ForkPlus/Git/Commands/GetRepositoryStatsGitCommand.cs)
  - **数据结构**：`RepositoryStats` 新增 `public Dictionary<DateTime, DayContributionInfo> CommitsByDate { get; }` 字段，构造函数同步加参数。`DayContributionInfo` 持有 `Commits` 和 `CommitsByAuthor`（按作者统计当天提交数），`AddCommit` 返回新实例（immutable 风格），`GetTopAuthors(limit)` 按提交数降序、名字升序取前 N 个—— [RepositoryStats.cs](file:///workspace/src/ForkPlus/Git/Commands/RepositoryStats.cs)
  - **自定义 WPF 控件**：新建 `ContributionHeatmap : Grid`——53 列 × 7 行 Grid，每格一个 `Border`（11×11，圆角 2px，间距 3px）。最近一周在右侧，超出今天的格子不绘制。色阶 5 级（按当仓库最大日提交数 quartile 分桶：0 / (0,25%] / (25%,50%] / (50%,75%] / (75%,100%]），light 主题用 GitHub 经典 `#ebedf0/#9be9a8/#40c463/#30a14e/#216e39`，dark 主题用 `#161b22/#033a16/#196f1a/#2ea043/#3fd95e`。订阅 `ApplicationThemeChanged` 事件，主题切换时重建格子刷新色阶—— [ContributionHeatmap.cs](file:///workspace/src/ForkPlus/UI/Controls/ContributionHeatmap.cs)
  - **依赖属性绑定**：`CommitsByDateProperty` DP，setter 触发 `RebuildCells` 重绘
  - **布局接入**：`StatisticsUserControl.xaml` 的 `StatsContainer` Grid 行数从 4 加到 6，在 Row2（LinePlot）后插入 Row3"Contributions"标题 + Row4 热力图，原 Row3 的 Grid 顺移到 Row5。`UpdatePlots` 末尾追加 `Heatmap.CommitsByDate = stat.CommitsByDate`—— [StatisticsUserControl.xaml](file:///workspace/src/ForkPlus/UI/UserControls/StatisticsUserControl.xaml)、[StatisticsUserControl.xaml.cs](file:///workspace/src/ForkPlus/UI/UserControls/StatisticsUserControl.xaml.cs)
  - **tooltip**：每格 tooltip 两行——
    - 第一行：`string.Format(Translate("{0} contributions on {1}"), commits, date.ToString("yyyy-MM-dd ddd", CurrentCulture))`，日期带星期缩写（按系统 culture 显示）
    - 第二行：`string.Format(Translate("Authors: {0}"), string.Join(", ", top3) + ", +N more" if any)`，列出当天按提交数排序的前 3 个作者，超出部分显示"+N more"
    - commits==0 或无作者时只显示第一行
  - **i18n**：3 个新 key 同步到 7 语言——`"{0} contributions on {1}"`、`"Authors: {0}"`、`"+{0} more"`

## v1.6.2

### 优化：跟踪右键改为二级菜单 + 分支级搜索框（跟踪和检查远端同步状态通用）

- **需求**：① "跟踪"右键原来把所有远端分支平铺在一个菜单里（仅 < 150 个时），分支多了不好找；② 希望改成和"检查远端同步状态"一样的二级菜单（按远端分组）；③ 分支那一级最上面加搜索框，搜索框置顶不受上下滚动影响，跟踪和检查远端同步状态都要。
- **实现**：
  - **跟踪改为二级菜单**：`CreateLocalBranchContextMenuItems` 中"Tracking"菜单项重构为二级结构——顶层放"Remove tracking reference"+ Separator，然后按远端名分组列出远端分支（`repositoryData.References.RemoteBranches` 按 Remote 分组），与"检查远端同步状态"风格一致。移除原来的 `< 150` 分支限制（搜索框解决了查找问题）—— [SidebarUserControl.xaml.cs](file:///workspace/src/ForkPlus/UI/UserControls/SidebarUserControl.xaml.cs)
  - **可搜索子菜单模板**：`Menu.xaml` 新增 `SearchableSubmenuHeaderTemplateKey` ControlTemplate 和 `SearchableSubmenuMenuItem` Style——Popup 内容改为 `Grid(Rows: Auto, *)`，第 0 行是置顶搜索框（`PlaceholderTextBox` + `SearchPanelPlaceholderTextBox` 样式 + `SearchOnIcon` 图标，不随列表滚动），第 1 行是原有 `ScrollViewer`+`ItemsPresenter`（可滚动）。搜索框样式复用侧边栏过滤框同款 `SearchPanelPlaceholderTextBox`，风格一致—— [Menu.xaml](file:///workspace/src/ForkPlus/Theme/Styles/Menu.xaml)
  - **分支级搜索框**：新增 `CreateSearchableRemoteGroupMenuItem` 辅助方法，创建带搜索框的远端分组子菜单；子菜单打开时通过 `SubmenuOpened` 事件在视觉树里找到 `PART_SearchBox`，订阅 `TextChanged` 按分支名（`Header`）不区分大小写过滤，隐藏不匹配项（`Visibility=Collapsed`），并自动聚焦搜索框。跟踪和检查远端同步状态的远端分组都复用此方法—— [SidebarUserControl.xaml.cs](file:///workspace/src/ForkPlus/UI/UserControls/SidebarUserControl.xaml.cs)

## v1.6.1

### 修复：远端同步状态弹窗布局拥挤

- **问题**：远端同步冲突预检结果弹窗（ForkSyncCheckWindow）的图标和文字挤在一起，左侧 80px 空列占位但图标被塞进右侧 DockPanel，视觉拥挤。
- **修复**：Grid 改为 `Auto + *` 双列布局，图标放第一列（36×36，右上对齐，右间距 14px），文字放第二列 StackPanel（标题 + 详情两行，行间距 8px）—— [ForkSyncCheckWindow.xaml](file:///workspace/src/ForkPlus/UI/Dialogs/ForkSyncCheckWindow.xaml)

### 修复：检查更新"已是最新版本"未显示版本号

- **问题**：手动检查更新，当已是最新版本时只显示"您使用的是最新版本。"，不显示当前版本号，用户无法确认当前版本。
- **修复**：文案改为"您使用的是最新版本 (v{0})。"，`FormatCurrent` 填入 `info.CurrentVersion`。i18n key 从 `"You are using the latest version."` 变更为 `"You are using the latest version (v{0})."`，7 种语言同步更新—— [UpdateCheckWindow.xaml.cs](file:///workspace/src/ForkPlus/UI/Dialogs/UpdateCheckWindow.xaml.cs)

### 修复：git mm 子仓变更数量"从有到无"（彻底修复）

- **问题**：git mm 视图下子仓变更数字（如"本地变更(100)"）短暂显示后变成 0，且不恢复。右键"作为独立仓库打开"后独立标签页有数据，证明 `git status` 本身没问题，问题在 git mm 视图内部的刷新/实例管理流程。
- **根因 A（实例替换清零）**：`RefreshSubrepos`（首次扫描）和 `RunGitMm` sync 分支（每次 sync 后重扫）会把 `_workspace.Subrepos` 整体替换为全新 `GitMmSubrepoItem` 实例——新实例所有计数字段默认 0、`RuntimeStateUpdatedAtUtc=null`。紧接着 `RebuildSubrepoTabs` 把 tab 重建到新实例，UI 立刻显示 0。TTL 缓存按实例缓存，新实例 `HasValue=false` 导致缓存失效。
- **根因 B（补救刷新被丢弃）**：替换实例后调用的 `RefreshSubrepoRuntimeState` 是唯一恢复数据的机会，但 `SubreposTabControl_SelectionChanged` 在切 tab 时调用 `CancelStatusRefresh()` → 自增 `_runtimeStateRequestId` → 回调命中守卫 `requestId != _runtimeStateRequestId` 整体 return → 新实例永久停在 0。`RefreshSubrepoRuntimeState` 刷新的是所有子仓的全局状态，切 tab 不应取消它。
- **修复 A（迁移运行态）**：新增 `MigrateRuntimeState` 方法，在 `RefreshSubrepos` 和 `RunGitMm` sync 分支替换 `Subrepos` 后，按 Path 把旧实例的运行态数据（`ChangedFilesCount`/`HasLocalChanges`/`RuntimeStateUpdatedAtUtc` 等）迁移到新实例——即使补救刷新被丢弃，新实例也带着旧数据，UI 不显示 0。
- **修复 B（切 tab 不取消全局刷新）**：`SelectionChanged` handler 移除 `CancelStatusRefresh()` 调用。`RefreshSubrepoRuntimeState` 入口仍调 `CancelStatusRefresh` 取消旧刷新，新旧请求去重由 `_runtimeStateRequestId` 守卫保证，切 tab 不再干扰在途的全局刷新。
- **修复 C（失败不覆盖）**：`GetSubrepoRuntimeState` 在 `git status` 失败时返回 `null`，回调对 `states[i] == null` 的项跳过——失败不用 0 覆盖已有正确值。
- **附**：移除前轮的 `_isRebuildingTabs` 标志和 `RebuildSubrepoTabs` try/finally 包裹（已被修复 B 取代，不再需要）—— [GitMmUserControl.xaml.cs](file:///workspace/src/ForkPlus/UI/UserControls/GitMmUserControl.xaml.cs)

### 修复：git mm 子仓视图左侧树/未暂存区为空（子仓自己的变更被误过滤）

- **问题**：git mm 视图下点击子仓 tab，左侧"本地变更"/"所有提交"为空，下方未暂存区也无任何文件；但右键"作为独立仓库打开"后独立标签页一切正常。
- **根因**：`RepositoryUserControl.NormalizeChangedFilesForDisplay` 无脑取全局 `ActiveGitMmUserControl` 做子仓入口过滤，没判断"当前 RepositoryUserControl 自身是不是该工作区的子仓"。`ChangedFilesDisplayNormalizer.NormalizeForDisplay` 在 `gitMmUserControl` 非空时对每个变更文件调 `IsGitMmManagedSubrepoChange`，其 fallback 分支用 `ContainsSubrepoPath`（前缀匹配）判断文件绝对路径——子仓自己的文件路径必然以子仓路径为前缀，于是被全部误判为"git mm 管理的子仓变更"而过滤掉。`CommitUserControl.FilterGitMmManagedSubmoduleChanges` 也调同一方法，导致未暂存区同样为空。"作为独立仓库打开"路径 `ActiveGitMmUserControl` 为 null，命中 `NormalizeForDisplay` 的 early-return 不过滤，所以有数据——这正是两条路径表现不同的核心。
- **修复**：`NormalizeChangedFilesForDisplay` 增加短路——仅当当前 `RepositoryUserControl.GitModule.Path` 等于 `gitMmUserControl.WorkspacePath`（即当前是工作区根/主仓视图）时才走过滤；子仓自身的视图直接返回原始 `changedFiles`，不过滤。这样主仓视图仍隐藏子仓入口变更（原有行为），子仓视图正常显示自身变更—— [RepositoryUserControl.xaml.cs](file:///workspace/src/ForkPlus/UI/UserControls/RepositoryUserControl.xaml.cs)

### 修复：远端同步状态弹窗显示 `[Dialog Description]` 占位符

- **问题**：远端同步状态弹窗（ForkSyncCheckWindow）布局修复后，标题栏下方多出一行 `[Dialog Description]` 文字。
- **根因**：`ForkPlusDialogWindow` 基类在 `InitializeDialogChrome` 中会自动渲染 `DialogDescription` 文本块；子类只设置了 `DialogTitle` 未设置 `DialogDescription`，TextBlock 未被赋值，XAML 模板里的占位符文本 `[Dialog Description]` 暴露出来。并非 `ToString` 未实现，而是基类占位符未被覆盖。
- **修复**：构造函数中显式设置 `DialogDescription = string.Empty;`，让基类占位符被空字符串覆盖—— [ForkSyncCheckWindow.xaml.cs](file:///workspace/src/ForkPlus/UI/Dialogs/ForkSyncCheckWindow.xaml.cs)

### 优化：将"检查 Fork 同步状态"统一改为"检查远端同步状态"

- **需求**：该功能检测的是本地分支与 upstream 远端分支的同步状态，不限于 fork 工作流，"Fork 同步"表述有误导性，改为"远端同步"更准确。
- **实现**：4 个 i18n key 重命名（`Check Fork Sync Status...` → `Check Remote Sync Status...`、`Check Fork Sync...` → `Check Remote Sync...`、`Checking fork sync: {0}/{1}` → `Checking remote sync: {0}/{1}`、`Fork Sync Status` → `Remote Sync Status`），7 种语言同步更新值。代码中 7 处引用（`CheckForkSyncCommand.cs` 5 处、`SidebarUserControl.xaml.cs` 1 处、`ForkSyncCheckWindow.xaml.cs` 1 处）同步改 key 名。类名/枚举名（`CheckForkSyncCommand`、`ForkSyncStatus` 等）保持不变—— [CheckForkSyncCommand.cs](file:///workspace/src/ForkPlus/UI/Commands/CheckForkSyncCommand.cs) / [SidebarUserControl.xaml.cs](file:///workspace/src/ForkPlus/UI/UserControls/SidebarUserControl.xaml.cs) / [ForkSyncCheckWindow.xaml.cs](file:///workspace/src/ForkPlus/UI/Dialogs/ForkSyncCheckWindow.xaml.cs)

### 优化：远端同步状态改为二级菜单选择远端分支 + 立即弹框显示检测中

- **需求**：① 原实现默认用本地分支名去 upstream 远端找分支，但远端不一定有同名分支，经常找不到；② 整个检测在后台跑完才弹窗，用户点击后以为没反应；③ 提示文案"你可以直接推送到 fork 仓"措辞有误导性。
- **实现**：
  - **二级菜单**：分支右键菜单的"检查远端同步状态"改为二级菜单，按远端名分组列出所有远端分支（`repositoryData.References.RemoteBranches`，与"Tracking"菜单风格一致），用户显式选择目标远端分支后触发检测，不再默认用本地分支名—— [SidebarUserControl.xaml.cs](file:///workspace/src/ForkPlus/UI/UserControls/SidebarUserControl.xaml.cs)
  - **立即弹框**：`CheckForkSyncCommand.Execute` 新增接受 `RemoteBranch` 参数的重载；选定后立即弹出 `ForkSyncCheckWindow`（status 传 null 进入"检测中"状态），后台 JobQueue 跑 fetch + merge-tree 检测，完成后通过 `Dispatcher.Async` 回调调 `UpdateResult` 刷新三态结果。检测中提交按钮由 `IsSubmitAllowed` 守卫自动禁用—— [CheckForkSyncCommand.cs](file:///workspace/src/ForkPlus/UI/Commands/CheckForkSyncCommand.cs) / [ForkSyncCheckWindow.xaml.cs](file:///workspace/src/ForkPlus/UI/Dialogs/ForkSyncCheckWindow.xaml.cs)
  - **文案修正**：去掉提示中"push to your fork"的 "fork" 措辞，改为通用的"push"（`You can push without syncing.` / `...before pushing.`），2 个 i18n key 重命名，7 种语言同步更新；新增 `Checking... Please wait.` i18n key（7 种语言）

### 新功能：git mm 子仓右键"作为独立仓库打开"

- **需求**：git mm 视图下，子仓 tab 上的右键菜单加一个"作为独立仓库打开"选项，点击后用单仓方式新开一个 tab，方便单独操作某个子仓。
- **实现**：`CreateSubrepoTabContextMenu` 新增 `Open as Standalone Repository` 菜单项，点击调用 `MainWindow.Instance?.TabManager?.OpenRepository(subrepo.Path)` 在主窗口新开一个独立仓库 tab—— [GitMmUserControl.xaml.cs](file:///workspace/src/ForkPlus/UI/UserControls/GitMmUserControl.xaml.cs)
- **新增 i18n key**（7 种语言）：`Open as Standalone Repository`。

## v1.6.0

### 新功能：AI 解决合并冲突

- **需求**：合并冲突解决窗口（Side-by-Side Merge）逐个冲突块手动解决比较繁琐，希望让 AI 一键解决全部冲突。
- **实现**：在文本合并模式的工具栏新增「🤖 AI Resolve」按钮。点击后读取磁盘上带冲突标记的原始文件内容，构造 prompt（要求 AI 合并两侧变更、保留非冲突上下文、不输出解释/代码围栏），通过 `OpenAiService.CreateFromAiReviewSettings().OpenAiRequestStreamingWithRetry` 流式请求；收到完整响应后剥离可能的 markdown 代码围栏，检测是否残留冲突标记（若仍含 `<<<<<<<`/`=======`/`>>>>>>>` 则提示用户检查），最后经用户确认后通过 `ResolveMergeConflictGitCommand`（`File.WriteAllText` + `git add`）写回并关闭窗口。处理中按钮禁用并显示「AI is resolving conflicts...」状态。仅在配置了 AI 检视且当前为文本合并模式时显示该按钮—— [SideBySideMergeWindow.xaml](file:///workspace/src/ForkPlus/UI/Dialogs/SideBySideMergeWindow.xaml) / [SideBySideMergeWindow.xaml.cs](file:///workspace/src/ForkPlus/UI/Dialogs/SideBySideMergeWindow.xaml.cs)

### 新功能：Fork 工作流同步冲突预检

- **需求**：fork 工作流下（upstream 主仓 + origin fork 仓），push 前想先知道本地分支与 upstream 目标分支是否会冲突——没冲突就懒得多 pull 一次，有冲突则必须先拉取解决才能继续。
- **实现**：分支右键菜单新增「Check Fork Sync Status...」（仅当存在 upstream/非 origin 远端时启用）。后台三步检测：①`FetchGitCommand` 拉 upstream（`noPrompt`）；②`git rev-parse --verify` 确认 upstream 远端分支存在；③`git merge-base` 求共同祖先，若祖先 == upstream HEAD 则「SafeToPush」；否则用 legacy 3-arg `git merge-tree`（`merge-base upstream local`）预演合并，扫描 `+>>>>>>>`/`+<<<<<<<` 标记区分「ShouldSyncNoConflict」和「MustSyncWithConflict」。结果用三态对话框展示（绿勾/黄叹/红叉），需要同步时主按钮一键打开 Pull 窗口拉取并解决—— [CheckForkSyncStatusGitCommand.cs](file:///workspace/src/ForkPlus/Git/Commands/CheckForkSyncStatusGitCommand.cs) / [ForkSyncCheckWindow.xaml.cs](file:///workspace/src/ForkPlus/UI/Dialogs/ForkSyncCheckWindow.xaml.cs) / [CheckForkSyncCommand.cs](file:///workspace/src/ForkPlus/UI/Commands/CheckForkSyncCommand.cs) / [SidebarUserControl.xaml.cs](file:///workspace/src/ForkPlus/UI/UserControls/SidebarUserControl.xaml.cs)

### 新功能：Commit 面板 Gitmoji

- **需求**：commit subject 输入 `:` 时希望弹出 gitmoji emoji 选择器（如 `:bug:` → 🐛），方便给提交加上语义化 emoji 前缀。
- **实现**：复用已有的 `AutoCompleteTextBox` 补全体系（`CommitSubjectTextBox` 已内置 Popup+ListBox 浮层）。新增 `GitmojiAutocompleteProvider`（`IAutoCompleteProvider`）：从光标向前找 `:`（中途遇空白则不触发），取 `:` 后的 prefix 与 ~70 项标准 Gitmoji 短名匹配，返回 `GitmojiAutoCompleteSuggestion`（选中后插入「emoji + 空格」）。配套 `GitmojiAutoCompleteSuggestion` 子类携带 `GitmojiEntry`，在 [Listview.xaml](file:///workspace/src/ForkPlus/Theme/Styles/Listview.xaml) 中新增隐式 DataTemplate（emoji + 短名 + 描述三列，emoji 用 `Segoe UI Emoji` 彩色字体）。在 `CommitUserControl` 构造函数中注册 provider；`FullCommitMessage` setter 和 `SetRecentCommitMessage` 加 `DisableUpdates` 保护，避免 AI 生成/最近消息回填等程序化写入 subject 时误触发选择器—— [GitmojiData.cs](file:///workspace/src/ForkPlus/UI/Controls/GitmojiData.cs) / [GitmojiAutocompleteProvider.cs](file:///workspace/src/ForkPlus/UI/Controls/GitmojiAutocompleteProvider.cs) / [GitmojiAutoCompleteSuggestion.cs](file:///workspace/src/ForkPlus/UI/Controls/GitmojiAutoCompleteSuggestion.cs) / [CommitUserControl.xaml.cs](file:///workspace/src/ForkPlus/UI/UserControls/CommitUserControl.xaml.cs)

### 优化：AI 辅助开发对话 Markdown 渲染 + Emoji 彩色显示

- **问题**：AI 辅助开发对话窗口的 AI 回复原先用纯 TextBox 显示，Markdown 格式（代码块/列表/表格等）以原始文本呈现，可读性差；且 emoji 显示为方框/黑白线条，观感不佳。
- **实现**：AI 回复改用 WebView2 渲染。通过 native Biturbo 库 `Bt.bt_md_to_html` 把 Markdown 转 HTML 后 `NavigateToString` 写入 WebView2（与 AiCodeReviewWindow 共用底层转换）。流式响应边收边追加到 Markdown 缓冲，节流后实时渲染，用户能逐段看到生成内容。WebView2 导航完成后用 JS 测量 `document.documentElement.scrollHeight` 自动调整控件高度以完整显示。WebView2 原生支持彩色 emoji，配合 `Segoe UI, Segoe UI Emoji` 字体回退，emoji 显示为彩色。同时按主题（亮/暗）设置 `PreferredColorScheme`，禁用右键菜单—— [AiDevelopmentWindow.xaml.cs](file:///workspace/src/ForkPlus/UI/Dialogs/AiDevelopmentWindow.xaml.cs)
- **新增 i18n key**（7 种语言）：AI 解决冲突相关（`AI Resolve`、`AI is not configured...`、`Failed to read conflict file: {0}`、`No conflict markers found in the file.`、`AI is resolving conflicts...`、`Use AI to resolve all conflicts`、`AI resolve failed: {0}`、`AI returned empty content. Aborting.`、`AI output still contains conflict markers...`、`AI resolved all conflicts. Apply the resolved content and close?`、`Failed to apply resolved content: {0}`）；Fork 同步预检相关（`Check Fork Sync...`、`Check Fork Sync Status...`、`Fork Sync Status`、`No remotes configured...`、`No 'upstream' remote found...`、`No active branch to check.`、`Checking fork sync: {0}/{1}`、`Safe to push`、`'{0}' is up-to-date with {1}...`、`Recommended to sync`、`{0} has new commits that are not in '{1}'...`、`Pull from upstream`、`Skip and push later`、`Conflicts detected`、`{0} has new commits that would conflict with '{1}'...`、`Pull and resolve`、`Upstream branch not found`、`No remote branch '{0}' found on the upstream remote...`、`Unable to determine sync status`、`Could not determine whether '{0}' would conflict with {1}...`）。

## v1.5.8

### 修复：变更数量大时暂存区/未暂存区被强制平铺（改回树状）

- **问题**：当暂存区或未暂存区的变更文件数达到 5000 时，列表会从用户选择的「树状」自动降级为「平铺」，即使设置了 Tree 模式也不按目录层级显示。这是一个古早的性能优化，但用户认为这是回归，希望变更多时也保持树状。
- **根因**：`FileListUserControl.GetEffectiveMode` 在 `mode == Tree && source.Length >= 5000` 时把模式替换为 `List`。
- **修复**：移除该降级逻辑——三个调用点（`SetItemSource`/`SetItemSourceAsync`/`RebuildItems`）直接用用户选择的 `Mode`，变更多时仍按树状构建。保留大列表（≥5000）的后台线程构建（`Task.Run`）和跳过选中项恢复，避免 UI 冻结——这部分是纯性能优化，不影响显示形态。常量 `LargeFileListUiDegradeThreshold` 更名为 `LargeFileListBackgroundBuildThreshold` 以反映其当前职责—— [FileListUserControl.xaml.cs](file:///workspace/src/ForkPlus/UI/UserControls/FileListUserControl.xaml.cs)

## v1.5.7

### 修复：git mm 子仓变更仍不显示（v1.5.6 修复无效）

- **问题**：v1.5.6 把子仓状态命令对齐到了单仓**脏检查** `IsRepositoryDirtyGitCommand`（`--untracked-files=no`，排除 untracked）。当子仓的变更全部是 untracked 文件时，计数为 0，于是“啥都没有”。
- **根因**：单仓**变更列表** `GetChangedFilesGitCommand` 实际用的是 `--untracked-files=all`（含 untracked），与脏检查的 `--untracked-files=no` 是两回事。v1.5.6 误把脏检查参数当成了变更列表参数。
- **修复**：子仓本质上是普通单仓，“单仓有啥就显示啥，别区分”——子仓状态检测命令完全对齐单仓 `GetChangedFilesGitCommand`：`-c core.fsmonitor=false -c core.untrackedCache=false -c core.checkStat=default --no-optional-locks status -b --porcelain -z --untracked-files=all`，含 untracked 文件；递归子模块的状态命令同步对齐。同时把 `ParseBranchHeader`/`CountPorcelainChangedFiles`/`CountConflicts` 的解析从按 `\n` 分隔改为按 NUL 分隔，匹配 `-z` 输出—— [GitMmUserControl.xaml.cs](file:///workspace/src/ForkPlus/UI/UserControls/GitMmUserControl.xaml.cs)

### 易用性：子仓页签右键“打开 git mm 仓”快捷入口

- **需求**：单仓方式打开的某个仓，如果它其实是某个 git mm 工作区的子仓，希望在它的页签右键菜单加一个“打开 git mm 仓”按钮，可快捷跳转到对应的 git mm 页签。
- **实现**：
  - `ClosableTabItem.GetContextMenu` 对 `TabItemMode.Repository` 页签，先在已打开的 git mm 页签中查找所属工作区（`TabManager.FindGitMmWorkspacePathForSubrepo` → `GitMmUserControl.ContainsSubrepoPath`），找不到再向上查找 `.repo`/`.mm` 工作区根（`GitMmUserControl.FindAncestorGitMmWorkspace`，即便 git mm 页签未打开也能识别）；命中则追加“打开 git mm 仓”菜单项，点击调 `TabManager.OpenRepository(workspacePath)` 激活/创建对应 git mm 页签—— [ClosableTabItem.cs](file:///workspace/src/ForkPlus/UI/Controls/ClosableTabItem.cs) / [TabManager.cs](file:///workspace/src/ForkPlus/TabManager.cs)
- **新增 i18n key**（7 种语言）：`Open git mm Repository`。

## v1.5.6

### 修复：git mm 视图子仓变更不显示

- **问题**：git mm 视图下子仓明明有变更（`git status` 能看到、其他 git 工具也能看到），但 ForkPlus 一直显示不出变更。
- **根因**：`GitMmUserControl.GetSubrepoRuntimeState` 用裸 `git status -b --porcelain` 检测子仓状态，缺少 `--no-optional-locks`、未关闭 `core.fsmonitor`/`core.untrackedCache`，与主仓脏检查 `IsRepositoryDirtyGitCommand` 的命令参数不一致，存在锁竞争和 fsmonitor 误判；且失败时静默吞掉错误，无法定位。
- **修复**：子仓本质上是普通单仓，按单仓对待——子仓状态检测命令对齐 `IsRepositoryDirtyGitCommand` 的参数（`-c core.fsmonitor=false -c core.untrackedCache=false -c core.checkStat=default --no-optional-locks status -b --porcelain --untracked-files=no`），规避锁竞争和 fsmonitor 误判；失败时打印日志（path/exitCode/stderr），便于定位 `safe.directory`/dubious ownership 等问题—— [GitMmUserControl.xaml.cs](file:///workspace/src/ForkPlus/UI/UserControls/GitMmUserControl.xaml.cs)

### 重构：AI 代码检视页面（模型选择 / 进度状态 / 流式实时输出 / Stop）

- **问题**：AI 代码检视页面一直只有一个转圈圈，不知道当前进度（排队？请求中？生成中？）；页面缺少模型切换入口；返回信息/请求信息/排队信息没有地方承载。
- **模型下拉选择**：标题栏新增模型下拉框，复用 AI 辅助开发的模型加载逻辑（后台异步从 `/v1/models` 拉取，加载前先显示当前选中模型避免下拉为空），切换模型即时保存到 `AiReviewSelectedModel`—— [AiCodeReviewWindow.xaml](file:///workspace/src/ForkPlus/UI/Dialogs/AiCodeReviewWindow.xaml) / [AiCodeReviewWindow.xaml.cs](file:///workspace/src/ForkPlus/UI/Dialogs/AiCodeReviewWindow.xaml.cs)
- **状态栏（进度承载）**：标题栏下方新增状态栏（进度条 + 状态文字），承载排队/请求/生成等阶段信息。通过订阅 `JobMonitor.SetProgressAction`，把 `monitor.Update` 的阶段文字（"排队中..."/"正在收集差异..."/"使用 {model} 检视中..."/"排队中。{0} 后再次检查..."/"{0} 秒后重试（{1}/{2}）..."/"生成中...（已接收 N 字）"）实时同步到状态栏，替代之前一直转圈圈无任何反馈的体验。
- **流式实时输出**：`OpenAiService.CodeReview`/`CodeReviewFiles` 新增 `onChunk` 参数并透传给 `OpenAiRequestStreamingWithRetry`；AiCodeReviewWindow 传入 `OnStreamingChunk` 回调，把 SSE 流式 chunk 边收边追加到缓冲，节流（400ms）后转 Markdown→HTML 实时写入 WebView，用户能逐段看到 AI 生成内容，而不是等几十秒后一次性出现。markdown→html 转换放到线程池，避免阻塞 UI。
- **Stop 按钮**：标题栏新增 Stop 按钮，处理中可点击取消当前 AI 检视任务（`JobMonitor.Cancel()` 中断流式 SSE 请求），完成/取消/出错时自动隐藏。
- **排队/重试状态外显**：`OpenAiRequestStreamingWithRetry` 的排队等待和重试等待新增 `monitor.Update` 调用（之前只 `AppendOutputLine` 写到 job 输出，不触发进度回调），使排队/重试信息能显示在检视窗口状态栏。
- **新增 i18n key**（7 种语言）：`Stopped`、`Queued...`、`Collecting diff...`、`Generating... ({0} chars)`、`Queued. Waiting {0} before checking again...`、`Retrying in {0}s ({1}/{2})...`。

## v1.5.5

### 修复：git 命令预览过长挤掉确认按钮

- **问题**：对话框（Push/Pull/Fetch/Merge 等）的 git 命令预览区使用了 `TextWrapping.Wrap` 但没有 `MaxHeight`/`ScrollViewer`/`TextTrimming`。长命令换行多行后，预览行 Auto 高度无限增长，`SizeToContent=Height` 让窗口长高，但 WPF 把窗口钳制到屏幕高度，底部 Footer（确认/取消按钮）被裁出可视区且无法滚动回去，用户点不到确认按钮。
- **修复**：给命令预览 TextBlock 外层包 `ScrollViewer`（`MaxHeight=120`、`VerticalScrollBarVisibility=Auto`），限制预览区最大高度，超出部分在滚动条内查看；同时设置 `ToolTip` 为完整命令文本，鼠标悬停即可查看完整命令。
- **覆盖范围**：
  - 基类 `ForkPlusDialogWindow`（覆盖所有继承它的对话框：Push/Pull/Fetch/Merge/Checkout/Stash/Revert 等）—— [ForkPlusDialogWindow.cs](file:///workspace/src/ForkPlus/UI/Dialogs/ForkPlusDialogWindow.cs)
  - GitMm 三个窗口的内联预览（Start/Upload/Sync）+ InitGitMmRepositoryWindow—— 同步包 `ScrollViewer` + 设置 `ToolTip`

## v1.5.4

### 修复：AI 排队场景返回错误码（影响 AI 辅助开发 + AI 代码检视 + commit 消息生成）

v1.5.3 对排队场景的修复只对非流式路径生效，流式路径（AI 辅助开发、代码检视、commit 消息生成均走此路径）仍会直接返回错误码。本次彻底修复。

- **根因**：流式路径 `OpenAiRequestStreaming` 在请求失败时直接透传 `Connection.RequestStream` 返回的 `RemoteServiceJsonError`，绕过了 `RestClientBase.Decode` → `OpenAiService.DecodeJsonError` 解码链。`RemoteServiceJsonError.FriendlyMessage` 是通用文案"远程服务返回了错误响应。"，不含排队关键字，导致 `ShouldRetry` 无法识别排队/限流场景（429/503 + JSON 错误体），一次请求就失败。
- **修复 1 - 流式路径错误解码**（[OpenAiService.cs](file:///workspace/src/ForkPlus/Accounts/AiServices/OpenAiService.cs)）：新增 `DecodeStreamError` 方法，在两个流式重载（单轮 + 多轮对话）中将 `RemoteServiceJsonError` 经 `DecodeJsonError` 解码为 `RemoteServiceError`（`FriendlyMessage` 为真实错误文本），使 `ShouldRetry`/`IsQueuedWaitError`/`IsTransientServiceMessage` 能从真实文本识别排队关键字。
- **修复 2 - 注入 HTTP 状态码**（[Connection.cs](file:///workspace/src/ForkPlus/Utils/Http/Connection.cs)）：`DeserializeJsonError` 新增 `statusCode` 参数，将 HTTP 状态码注入错误 JSON（字段 `__http_status_code__`）。`DecodeServiceError` 提取该状态码并以前缀 `[HTTP 429]` 形式附加到错误文本，确保即使消息本身不含排队关键字（如 "internal error"），`ShouldRetry` 仍能通过状态码数字识别排队/限流并触发重试。同时修复了 `RemoteServiceJsonError` 丢失 HTTP 状态码信息的固有问题（此前仅保留 JSON 体）。
- **影响范围**：修复同时覆盖 AI 辅助开发对话、AI 代码检视（分支评审/SHA 区间评审/文件评审）、AI 生成 commit 消息——三者均走 `OpenAiRequestStreamingWithRetry` 流式路径，共享同一套排队/重试逻辑。

## v1.5.3

### 优化：AI 辅助开发体验（模型选择 / 需求队列 / 排队处理 / 上下文压缩 / commit 即时写入 / 停止任务）

- **模型下拉选择**：AI 辅助开发弹窗右上角新增模型下拉框，自动从 `/v1/models` 拉取可用模型列表（后台异步加载，不阻塞 UI）。用户可随时切换模型，选中项即时保存到 `AiReviewSelectedModel` 设置。加载前先显示当前模型避免下拉为空。
- **需求队列不阻塞输入**：之前 AI 处理期间输入框和发送按钮被禁用，用户必须等 AI 回复完才能继续说话。现在处理期间输入框始终可用，新输入的需求自动入队，发送按钮显示当前队列数量（如"发送 (队列: 2)"），当前请求完成后自动按顺序处理队列中的下一个。
- **停止任务**：AI 处理期间进度条旁新增"Stop"按钮，点击可停止当前 AI 任务及其后台 HTTP 请求（通过 `JobMonitor.Cancel()` 触发已注册的取消回调，中断流式 SSE 请求），同时清空待处理队列。取消路径改为切回 UI 线程执行收尾，避免跨线程操作 UI 元素。
- **正确处理排队场景**：修复 AI 服务排队时显示"远程服务返回了错误响应"的问题。根因是 `ServiceError.RemoteServiceJsonError.FriendlyMessage` 返回通用文本，不含排队关键字，导致 `ShouldRetry` 无法识别排队场景而直接放弃。修复：`DecodeJsonError` 中 `DecodeServiceError` 无法解析时用原始 JSON 文本作为错误消息；`MaxQueuedWaitSeconds` 从 `TimeoutSeconds`(默认 300s) 增大到 `max(TimeoutSeconds, 1800)`(30 分钟)，给排队足够等待时间。
- **上下文超长自动压缩**：之前历史消息超过 20 条时简单丢弃最早的，丢失上下文。现在发送前估算 token 数（约每 4 字符 1 token），超过 6000 时自动将早期对话通过 AI 生成摘要（保留关键文件路径、变更、需求、决策），替换为单条 system 摘要消息 + 最近 6 条原始消息。摘要失败时退回到简单截断。压缩过程复用流式+重试机制，享受排队/重试处理。
- **AI commit 消息即时写入**：修复 AI 生成 commit 消息时不即时写入、需等很久的问题。`GenerateCommitMessage` 新增 `onChunk` 参数，流式 chunk 实时追加到 commit 框（`FullCommitMessage`），用户无需等待整个请求完成。请求完成后用最终完整消息覆盖（可能经过 regex retry 修正）。同时受益于排队场景修复，commit 生成遇到排队也会继续等待而非报错。
- **新增 i18n key**（7 种语言）：`Select model...`、`Model switched to: {0}`、`Send (queued: {0})`、`Stop`、`Stop the current AI task and abort its request`、停止任务相关的状态消息。

## v1.5.2

### 优化：AI 辅助开发界面 + 按钮位置 + 对话记忆

- **AI 按钮迁移到顶部工具栏**：从仓库侧边栏设置按钮左侧移除，改放到主窗口顶部工具栏——位于"控制台"和"外观"按钮中间，更符合常用功能入口的位置习惯，且随仓库切换自动启用/禁用。
- **AI 对话增加记忆**：之前每次请求都是独立的，AI 不记得上下文。现在维护对话历史（user/assistant 消息列表），每次请求携带历史上下文发送，支持连续追问和迭代修改。`OpenAiService` 新增多轮流式请求方法（接收 messages 数组），最多保留最近 20 条历史防止 token 超限。System prompt 重构为固定指令（不含用户需求），用户需求作为独立 user 消息发送。
- **优化 AI 辅助开发界面**：
  - 顶部显示当前 AI 模型名
  - 空对话状态显示欢迎信息卡片（说明功能用法）
  - 底部增加操作提示文本（发送快捷键 + 记忆说明）
  - 增加"清空"按钮，可清空对话历史和界面重新开始
  - 按钮间距和布局调整，视觉更协调
- **新增 i18n key**（7 种语言）：`Clear`、`Clear conversation history`、`Conversation cleared.`、`Model: {0}`、欢迎信息、两种发送模式的提示文本。

### 修复：检查更新按钮无反应 + 504 网关超时

- **改为"先弹窗后检测"交互**：点击帮助菜单"Check for Updates..."后立即弹出检查窗口（显示进度条+"正在检查更新..."），在窗口内执行检测，避免点击后无反馈。检测完成后窗内直接显示结果——有更新（版本号+Release Notes+下载/不再提醒）、已是最新、或失败原因。关闭窗口通过 `CancellationToken` 立即中止 HTTP 请求。
- **修复 504 网关超时**：`UpdateChecker` 不再复用项目共享的 `Connection`（其 `HttpClientHandler` 默认走系统代理），改用独立的 `HttpClient` 并设置 `UseProxy=false` 直连 GitHub API，避免用户系统代理（clash/v2ray 等）对 `api.github.com` 不通时返回 504 Gateway Timeout。用户能正常访问 GitHub 但检查更新报 504 的问题得到解决。
- **修复窗口标题**：检查更新窗口标题从"发现更新"改为"检查更新"（新 i18n key `Check for Updates`），与功能语义一致。
- **新增 UpdateCheckWindow**：独立的检查更新对话框，`Loaded` 时启动后台 `Task.Run` 检测，`OnCancel`/`OnClosed` 触发 `CancellationTokenSource.Cancel()` 停止检测。
- **UpdateChecker 支持取消**：`CheckLatestRelease` 新增 `CancellationToken` 参数，关窗时能中止进行中的 HTTP 请求。
- **失败原因可见**：`UpdateInfo` 新增 `ErrorMessage` 字段，限流/网络失败/异常响应时记录具体原因，窗内显示"检测更新失败：{具体原因}"。
- **自动检测保持静默**：后台自动检测（30s 首检 + 24h 节流）仅在有更新且未被跳过时弹 `UpdateAvailableWindow`，失败静默不打扰用户。
- **新增 i18n key**：`Check for Updates`、`Checking for updates...`、`Update check failed: {0}`（7 种语言补齐）。

## v1.5.0

### 自动检测更新

- **启动后自动检测新版本**：启动后延迟 30 秒首次自检，之后每小时 tick 一次，仅当距上次检测达到设定间隔（默认 24 小时，下限 12 小时）时才实际请求 GitHub Releases API（`repos/hebin123456/ForkPlus/releases/latest`）。自动检测失败静默，不打扰用户。
- **帮助菜单主动检测**：帮助菜单新增"Check for Updates..."菜单项，不受节流限制，随时可点。无更新提示"已是最新版本"，检测失败提示检查网络。
- **新版本提示窗口**：发现新版本时弹出提示窗口，显示新版本号、当前版本号、Release Notes（更新内容），提供"Download"（跳转下载地址）、"Later"（稍后）两个按钮，以及"Don't remind me for this version"勾选框（按版本号跳过，新版本会重新提醒）。
- **新增设置项**：`CheckForUpdatesAutomatically`（自动检测开关，默认开）、`UpdateCheckIntervalHours`（检测间隔小时数，默认 24）、`SkippedUpdateVersion`（已跳过的版本号）；复用已存在的 `LastUpdateCheck` 做节流时间戳。
- **国际化**：新增 8 个 i18n key（Check for Updates.../Update Available/Release Notes/Download/Don't remind me for this version/You are using the latest version./Update check failed...），7 种语言补齐翻译。

## v1.4.7

### AI 辅助开发增强

- **AI 开发窗口改用流式输出**：`AiDevelopmentWindow` 此前用非流式 `OpenAiRequest`，用户需等整段响应才看到内容。改用 `OpenAiRequestStreamingWithRetry`（v1.4.6 为检视路径引入的 SSE 流式 API），新增 `onChunk` 回调参数，AI 生成的文本逐 chunk 实时追加到聊天气泡。与检视路径对齐，解决"卡一段时间无输出"的体验问题。
- **AI 开发新增"撤销 AI 修改"按钮**：AI 修改文件后，diff 结果头部显示"Undo AI Changes"按钮。点击后用 `beforeContents` / `_fileChanges` 回写文件原内容（恢复被删文件、删除新建文件、还原修改内容），复用已有的路径安全检查（`IsPathInAllowedDirectories`）。此前 AI 改坏文件需手动 `git checkout`。

### 国际化补全

- **AiDevelopmentWindow 中文字面量国际化**：约 24 个中文状态消息/标签（发送模式、排队中、正在请求 AI、AI 请求失败/出错、文件变更、新建/删除/修改、AI 响应、撤销相关等）补齐 7 种语言翻译 key。此前这些中文字符串作为 key 传给 `Current()` 但语言文件无对应条目，非中文用户看到的仍是中文。

### CI 质量治理

- **修复 `PathHelper.GetParent` 空路径崩溃**：`Path.GetDirectoryName("")` 在 .NET Framework 上抛 `ArgumentException`，导致 `PathHelperTests.GetParent_ReturnsNullForNullOrEmptyPath` 一直失败（被 continue-on-error 掩盖）。改为对 null/空/非法路径返回 null，与 null 输入行为一致。此问题自 v1.4.5 起存在。
- **单元测试超时从 300s 缩短到 120s**：vstest 会话因测试宿主进程不退出（预先存在的后台线程泄漏，与 v1.4.7 改动无关）一直触发 `TestSessionTimeout` 超时。386 个测试本身 1 秒内全部通过，但会话卡满超时才被中止。缩短到 120s 减少等待；单元测试与系统测试步骤暂保留 `continue-on-error: true` 直至定位泄漏线程的测试和 ST 测试的 UI 自动化环境适配问题。

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
