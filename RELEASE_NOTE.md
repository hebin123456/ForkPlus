# Release Notes

本文件记录 ForkPlus 各版本的变更。从 v1.3.0 开始，每次发布都会在此更新。

## v3.12.3

### 修复

- **AI 辅助开发窗口：AI 回复气泡未靠左的问题**：AI 对话气泡设置了 `MaxWidth = 700` 但未显式指定水平对齐，WPF 中 `Stretch` 对齐被 `MaxWidth` 截断时元素会居中放置，导致窗口较宽时 AI 回复气泡悬在消息区中间、左右留白不对称。现在流式回复气泡（`CreateStreamingResponseBubble`）与完整回复气泡（`AddAiResponseMessage`）均显式 `HorizontalAlignment = Left`，AI 气泡贴左、用户气泡靠右，形成标准对话布局。已排查确认欢迎横幅、状态文本、diff 结果容器等其他消息元素不受影响。
- **AI 辅助开发窗口：修复靠左后气泡宽度塌陷的问题**：`WebView2` 为 HwndHost 系控件，期望宽度极小，气泡改为靠左对齐后失去 `Stretch` 撑满机制，宽度收缩到仅剩内边距的窄条（约几十像素），内容无法查看。现在气泡宽度按消息面板实际宽度显式指定（上限 `MaxWidth = 700`），窗口缩放时通过 `MessagePanel.SizeChanged` 同步更新所有 AI 气泡；面板尚未布局时（首次显示前）跳过，待首次布局触发 `SizeChanged` 补齐。
- **补齐 v3.12.2 MessageBox 替换引入的 8 个缺失国际化键**：v3.12.2 将原生 MessageBox 替换为 `MessageBoxWindow` 时，部分新引入的文案键未写入语言包，非英文界面回退显示英文。现已补齐 7 个语言包（zh-Hans / zh-Hant / ja-JP / ko-KR / fr-FR / de-DE / es-ES）：`Stash changes`（贮藏更改）、`Unsupported Platform`、`Failed to load file`、`Confirm Import`、`Import Complete`、`Import` 覆盖确认与导入完成两条多行提示、`Force push`（强制推送）。术语与各语言包既有译法对齐（stash → 贮藏/貯藏/スタッシュ/스태시/Remisage/Stash/stash）。已全量校验 9 个改动文件中 81 处 `MessageBoxWindow` 字面量全部命中语言包。

## v3.12.2

### 修复

- **清理原生 MessageBox 残留，统一使用自定义 MessageBoxWindow**：全仓库 29 处 `System.Windows.MessageBox.Show` 残留全部替换为 ForkPlus 自定义弹窗 `MessageBoxWindow`，弹窗样式与主题（明暗皮肤、自定义配色）保持一致，不再出现系统原生灰框：
  - 覆盖 9 个文件：CustomColorsDialog（12 处，配色导入/导出提示与校验错误）、CheckForkSyncCommand（4 处，远端同步预检警告）、CreatePartialStashWindow（3 处）、App.xaml.cs（3 处，32 位系统警告与 git 版本检测）、RepositoryUserControl（2 处，Undo/Redo 前确认）、SaveStashWindow（2 处）、ImportExportUserControl（2 处，配置导入确认与完成提示）、RevisionListViewUserControl（1 处，AI 解释 commit 未配置提示）、AiReviewPreferencesUserControl（1 处，技能文件加载失败）。
  - 图标语义映射：原 `Warning`/`Error` → 警告图标；`Information` → 无图标；纯通知单按钮 OK。
  - 原 `YesNo` 场景（stash 前确认）：提交键 = 先 stash 并继续，取消键 = 中止，语义不变。
  - 原 `YesNoCancel` 场景（已推送提交的 Undo 确认）：拆为两步弹窗——第一步确认是否继续（取消即中止），第二步选择"强制推送远端"或"仅本地撤销"，三条路径全部保留。
  - 文案继续走 `PreferencesLocalization` 国际化查表，语言包中已有键的翻译不丢失，无键文案回退英文（与替换前行为一致）。
  - 清理后全仓库不再残留 `MessageBox.Show` / `MessageBoxButton` / `MessageBoxImage` / `MessageBoxResult` 引用。

## v3.12.1

### 新功能

- **mm 子仓 Push 防呆（与 v3.11.2 Pull 防呆同构）**：在 git mm 子仓中触发推送时，与拉取一样弹出告警引导：
  - 检测逻辑与 Pull 防呆完全一致（`FindGitMmWorkspacePathForSubrepo`），同时覆盖"mm 页签内选中子仓"与"单仓页签打开但路径位于 mm 工作区内"两种场景。
  - 推荐路径：切换到所属 git mm 工作区并自动打上传窗口执行 `git mm upload`，多个子仓变更一起推送，保持子仓间一致性。
  - 逃生口：用户明确选择"仅推送该仓库"时按普通单仓 push 继续；取消则中止。
  - 覆盖全部三个推送入口：Push 窗口（菜单/快捷键 Ctrl+Shift+P）、Quick Push、分支右键 Push。
  - 新增公共 `GitMmUserControl.OpenUploadWindow()`（与 `OpenSyncWindow` 同构），供上传按钮与引导流程复用。
  - 新增文案已国际化（zh-Hans/zh-Hant/ja-JP/ko-KR/fr-FR/de-DE/es-ES）。

## v3.12.0

### 新功能

- **拉取默认使用 Rebase（全局开关）**：偏好设置 → 通用新增"默认使用变基方式拉取"复选框。开启后拉取代码走 `--rebase` 而非 merge，从源头保持提交历史线性，本地不再产生 "Merge branch 'xxx'" 这类合并提交。该开关与 Pull 窗口 / Quick Pull 的既有 rebase 选项同源（同一设置项），一处开启处处生效。
- **推送前 Squash（合并多条未推送提交）**：Push 窗口新增"Squash 未推送的提交"选项，解决"多次 commit → 拉取 → 解冲突 → 再 commit → 推送"后远端出现多条零碎提交的问题：
  - 勾选后，推送前自动把本地所有未推送提交合并为一条（`reset --soft` 到上游 + 重新提交），内容零丢失。
  - 仅当分支有上游且未推送提交数 ≥ 2 时可用，复选框自动显示待合并条数（如"压缩 3 个未推送的提交"）。
  - 合并后的提交信息默认取最早一条提交的标题 + 全部标题清单，可在窗口内直接编辑后再推送。
  - 命令预览会同步展示实际执行的完整命令链（reset --soft → commit → push），所见即所得。
  - 推送前会重新校验未推送提交数，若期间已被推送（少于 2 条）则自动跳过 Squash 直接推送。
  - 两项功能的新增文案均已国际化（en/zh-Hans/zh-Hant/ja-JP/ko-KR/fr-FR/de-DE/es-ES）。

## v3.11.2

### 新功能

- **mm 子仓单仓 Pull 防呆（检测 + 引导 + 逃生口）**：git mm 工作区由多个子仓组成，单独对某个子仓执行 `git pull` 会破坏子仓间版本一致性。现在在 mm 子仓内触发 Quick Pull 或 Pull 窗口时：
  - **检测**：自动判定当前仓库是否隶属某个 git mm 工作区（覆盖"mm 页签内选中子仓"与"单仓页签打开但路径位于 mm 工作区内"两种场景，向上递归查找 `.repo/.mm` 工作区根）。
  - **引导**：弹出引导窗口说明风险，默认推荐切换到所属 git mm 工作区并打开同步窗口执行 `git mm sync`，保持所有子仓版本一致。
  - **逃生口**：用户明确选择"仅拉取当前仓库"时，按普通单仓 pull 继续，不阻断有意的单仓操作。
  - 引导窗口文案已国际化（en/zh-Hans/zh-Hant/ja-JP/ko-KR/fr-FR/de-DE/es-ES 8 种语言）。

### 修复

- **补漏引导窗口描述文案的国际化**：mm 子仓 pull 引导窗口中"仅拉取当前仓库"单选框下方的描述文本（带句号版本 "Skip git mm sync and pull this repository as a standalone repository."）此前只添加了 ToolTip 用的无句号 key，导致该描述在非英文界面一直显示英文。现已在 7 个非英语语言文件中补齐该 key。
- **移除误提交的 libbiturbo.so**：`third_party/libbiturbo.so`（Linux 平台原生库）被误提交进了仓库。biturbo 原生库本就由构建期 `RestoreBiturbo` target 按编译平台从 Biturbo 仓库最新 release 直接拉取，不应纳入版本管理；且仓库中残留的 `.so` 会让 macOS 本地构建误判"已存在"而跳过拉取正确的 `.dylib`。现已删除，并在 `.gitignore` 中补齐三个平台产物（biturbo.dll / libbiturbo.so / libbiturbo.dylib）的忽略规则，防止再次误提交。

## v3.11.1

### 修复

- **过滤嵌套子仓与 Windows NUL 文件**：git mm 子仓目录下若嵌套另一个子仓（untracked 的 git worktree 目录条目），或在 mm 标签页/单仓打开时出现 Windows 保留设备名文件（NUL/CON/PRN 等），这些条目完全无法操作，现已在未暂存区过滤：
  - mm 标签页：过滤 untracked 且本身是 git worktree 且路径在 mm 子仓列表中的条目（嵌套子仓入口）。
  - 单仓打开：同样过滤 untracked 的 git worktree 目录条目，与 mm 标签页行为一致。
  - 两类场景均过滤 Windows 保留设备名文件。
  - 修复判定根因：git status 输出中 untracked（`??`）条目在 `ChangedFile.NewChangeType` 中映射为 `ChangeType.Added` 而非 `Untracked`，导致过滤条件从未命中；改用 `StatusType.Untracked` 判定并清理路径尾部斜杠。

## v3.11.0

### 新功能

- **git mm 界面重构**：git mm 页签界面布局大幅优化，与单仓界面风格统一：
  - git mm 自建状态栏融入主活动状态栏（与单仓 NotificationBar 同款样式：中间显示当前选中子仓名，左侧活动管理器按钮，右侧按当前过滤按钮）。
  - 原底部命令输出区域移除，命令输出改为浮层 Popup，点击活动管理器右侧"输出"按钮切换显隐（仅在用户主动点击时弹出，不再自动弹出）。
  - 新增命令历史按钮，与输出按钮并排；按钮 tips 国际化。

## v3.10.2

### 修复

- **git mm 子仓状态刷新与大列表渲染**：修复 git mm 打开子仓时感知不到文件变化、或只显示变化数量看不到 diff 内容的问题（根因是 git fsmonitor daemon 与 diff/status 命令口径不一致，现统一绕过 fsmonitor 并对齐 core.checkStat 配置）；修复文件数量多时未暂存树文件夹图标不绘制的问题；移除 5000 文件变更数加载阈值，有多少加载多少。
- **交付件精简**：移除发布包中冗余的语言卫星程序集文件夹（各语言文件夹内 dll 内容相同）。

## v3.10.1

### 修复

- **右键菜单文案修正**：仓库树图（Repository Overview）、Blame 视图、文件历史视图右键提交时的菜单项"在 Fork 中显示"已改为"在 ForkPlus 中显示"。该项此前残留了上游 Fork 的品牌名，未随 ForkPlus 改名。同步更新 8 种语言的翻译 key 与值。

## v3.10.0

### 新功能

- **自带 .NET 10 运行时，用户无需另装运行时**：发布包改为 **self-contained 自包含**模式，把 .NET 10 运行时（coreclr、基础类库等）直接打进发行包里。用户下载解压即可运行，不再需要先去微软官网下载安装 ".NET 10 Desktop Runtime"。
  - ForkPlus / ForkPlus.AskPass / ForkPlus.RI 三个 exe 共用同一份运行时文件（同目录加载），不重复占用空间。
  - 取舍：发行包体积由原来约 20MB 增至约 70–85MB（zip），这是打包运行时的必然代价，换来零安装依赖的体验。
  - 不做 trimming（WPF 大量使用反射，裁剪后运行时易崩）、不做 single-file（现有原生库加载逻辑依赖目录结构）。
  - 说明：仅打包了 .NET 运行时；WebView2 运行时仍依赖系统随 Edge 预装（Win10/11 默认都有），不在本次打包范围。

### 修复

- **CI 触发分支修正**：`build.yml` 的 push 触发分支由已删除的 `master-update` 改回 `master`。此前推送到 `master` 不会触发 CI 构建，只有打 tag 时才构建；现在推送 `master` 也会正常跑 CI。

## v3.9.2

### 修复

- **AI 解决冲突按钮无反应**：修复按钮点击后完全没反应的问题。此前 `_aiResolving` 标志若卡在 true（如上次请求异常退出未重置），按钮会永远静默 return。现在：
  - 正在处理中再次点击 → 弹窗提示"AI 正在解决冲突，请等待当前任务完成"，不再没反应
  - 文件/仓库未就绪 → 弹窗提示，不再静默
  - 文件路径解析失败 → 弹窗提示错误，不再静默
  - 异常退出时自动重置 `_aiResolving` 和按钮 loading 态
- **AI 解决冲突文案国际化**：修复解决冲突后显示的文字只有英文、未走国际化的问题。此前所有面向用户的文案（"AI Resolve"、"AI is not configured..."、"No conflict markers found..."、"AI returned empty content..."、"AI output still contains conflict markers..."、"AI resolved all conflicts. Apply..."、"Apply"/"Cancel" 等）都是硬编码英文字符串。现已全部改为 `PreferencesLocalization.Current` 调用，并为 8 种语言（en/zh-Hans/zh-Hant/ja-JP/ko-KR/fr-FR/de-DE/es-ES）补充翻译。

## v3.9.1

### 新功能

- **"Open in" 菜单智能推荐 IDE**：工具栏的 "Open in" 下拉菜单现在会根据当前仓库的项目类型，只显示匹配的 JetBrains IDE，不再无差别列出所有已安装的 IDE：
  - Node 仓库（有 `package.json`）→ 仅显示 WebStorm
  - Maven 仓库（有 `pom.xml`）→ 仅显示 IntelliJ IDEA
  - Android 仓库（`build.gradle` + `AndroidManifest.xml`）→ 仅显示 Android Studio（新增检测）
  - Python 仓库（`requirements.txt`/`pyproject.toml`/`setup.py`）→ 仅显示 PyCharm
  - Go 仓库（`go.mod`）→ 仅显示 GoLand
  - PHP 仓库（`composer.json`）→ 仅显示 PhpStorm
  - .NET 仓库（`*.sln`/`*.csproj`）→ 仅显示 Rider / Visual Studio
  - 识别不到项目类型 → 仅保留通用编辑器（VSCode/Cursor/Sublime 等）和终端/文件管理器
- 不匹配项目类型的 IDE（例如 Node 仓库里已装的 PyCharm）不再出现在菜单，保持菜单简洁。

## v3.9.0

### 优化

- 统一所有 AI 辅助界面（AI 解决冲突、AI 辅助开发、AI 解释代码、AI 代码检视、AI Commit Composer、AI PR 描述生成）的设计与实现，消除此前各窗口"各写一套"的重复代码与不一致体验：
  1. **统一 AI 操作按钮**：新增 `AiActionButton` 自定义控件，封装一致的样式（统一 padding、高度、字号、emoji 前缀）、加载状态管理（`SetBusy` 方法）及根据 AI 配置状态自动控制可见性。将 5 处原生 `Button` 替换为 `AiActionButton`。
  2. **抽取结果窗口基类**：新增 `AiResultWindowBase` 抽象基类，封装 ModelComboBox 初始化/切换、CSS 资源读取、Markdown→HTML 转换等共享逻辑。`AiTextResultWindow`、`AiCodeReviewWindow`、`AiCommitComposerWindow` 均继承此类，消除三份几乎相同的模型下拉初始化代码。CSS 读取和 Markdown 转换委托给 `AiStreamingWebView` 静态方法，与 `AiDevelopmentWindow` 共用同一份实现。
  3. **统一流式 Markdown 渲染控件**：新增 `AiStreamingWebView` 控件，封装 WebView2 初始化、节流渲染（避免每个 chunk 都触发 markdown→html→NavigateToString 卡顿）、滚动位置跟随（用户在底部附近时自动跟随新内容，主动上滚时不打断阅读）、错误处理与加载动画。`AiTextResultWindow` 和 `AiCodeReviewWindow` 用此控件替换各自约 250 行重复的流式渲染代码。`AiDevelopmentWindow` 的聊天气泡复用控件的静态 CSS/markdown 转换方法。`AiStreamingWebView` 还支持 `ResumeStreaming()` 用于部分重试时保留已有内容继续渲染。
  4. **统一窗口基类**：所有 AI 窗口统一继承 `CustomWindow`（`AiSuggestionPreviewWindow` 经 `ForkPlusDialogWindow` 间接继承），与程序其余界面视觉风格一致。
- 统一加载状态指示：所有 AI 界面使用一致的进度条 + 状态文字 + 取消按钮组合，替代此前各窗口手动管理 `IsEnabled`/`ToolTip` 的方式。
- `AiDevelopmentWindow` 中文硬编码字符串收口为英文资源键，并为 zh-Hans、zh-Hant、ja-JP、ko-KR、fr-FR、de-DE、es-ES 共 7 个语言文件补充翻译。
- 修复 `OpenAiLoginWindow` 标题错误显示为 "Login to GitLab" 的问题，改为 "Login to OpenAI"。

## v3.8.3

### Bug 修复

- 全面排查并修复多处可能导致应用闪退的 Bug：
  1. **[高] AI 解决冲突按钮闪退**：`MergeConflictUserControl` 和 `SideBySideMergeWindow` 中的 `AiResolveButton_Click` 是 `async void` 事件处理器，`await Task.Run` 之后直接访问 UI 元素（`AiResolveButton`、`_repositoryUserControl` 等），若用户在 AI 请求期间切换 Tab 或关闭窗口，会抛出未捕获异常导致应用闪退。修复方案：将 `async void` 拆分为 try-catch 包装器 + `async Task` 核心方法，确保所有异常被捕获并记录日志；await 后增加 `IsLoaded` 检查，控件/窗口已卸载时安全退出。
  2. **[高] 未变更文件视图空引用闪退**：`FileDiffControl.LoadUnchangedFileContent` 中 `repositoryUserControl` 来自 DependencyProperty（默认值 null），未做 null 检查直接访问 `.GitModule` 导致 NullReferenceException。`ShowUnchangedFileContentView` 同样存在 `RepositoryUserControl` 属性在异步回调期间被清空的空引用风险。均已补充 null 检查。
  3. **[中] 合并冲突面板多处空引用**：`UpdateResolveButton`（CheckBox 在 `SetConflict` 完成前被操作时 `_changedFile` 为 null）、`StageButton_Click`（链式访问 `_repositoryUserControl.Content.CommitUserControl` 未做 null 检查）、`ShaButton_Click`（`_repositoryUserControl` 和 `_changedFile` 未做 null 检查）均已补充防护。
- 顺带将 `SideBySideMergeWindow` 中 AI 解决冲突流程的 7 处原生 `MessageBox` 也替换为 ForkPlus 自定义弹窗 `MessageBoxWindow`，与 v3.8.2 保持一致。

## v3.8.2

### 优化

- 优化「AI 解决冲突」（AI Resolve）功能的弹窗体验：将 `MergeConflictUserControl` 中 AI 冲突解决流程里使用的 7 处原生 `System.Windows.MessageBox` 全部替换为 ForkPlus 自定义弹窗 `MessageBoxWindow`，使其与程序其余界面的视觉风格统一。涵盖的场景包括：AI 未配置提示、读取冲突文件失败、未检测到冲突标记、AI 请求失败、AI 返回空内容、AI 输出仍含冲突标记、应用解决内容前的确认、以及写回失败提示。确认弹窗由 Yes/No 改为「应用/取消」按钮，单按钮提示采用只读「确定」按钮，错误/警告类提示显示警告图标。

## v3.8.1

### Bug 修复

- 修复 v3.8.0 新增的"显示完整工作目录"功能存在的三个问题：
  1. 菜单项 "Show Full Working Directory" 未做国际化，非英文界面下显示英文原文。现已为 zh-Hans、zh-Hant、ja-JP、ko-KR、fr-FR、de-DE、es-ES 共 7 个语言文件补充翻译。
  2. 该视图为只读，但选中未变更文件时 Stage 按钮仍可点击。现已将 Stage 按钮在选中项全部为未变更文件（`ChangeType.Unchanged`）时置灰，避免对只读文件误触发暂存操作。
  3. 点击未变更文件时右侧视图仅显示 "File has no changes"，未展示文件内容。现已在 `FileDiffControl` 中对未变更文件特殊处理：通过 `git rev-parse HEAD` 取当前 HEAD sha，用 `GetFileContentGitCommand` 从仓库读取完整文件内容（文本走 `TextContentControl`、二进制走 `HexContentControl`/`BinaryFileContentControl`，与文件树浏览视图一致），后台线程加载避免阻塞 UI，加载期间可被切换文件取消。

## v3.8.0

### 新功能

- Commit 视图新增"显示完整工作目录文件树"选项。在文件列表的视图设置下拉菜单（View as Tree/List/CombinedList 同一处）追加一项 "Show Full Working Directory"，勾选后未变更的已跟踪文件也会出现在未暂存列表中，按目录树结构展示完整工作目录，未变更文件不显示状态图标（与变更文件的 M/A/D 等图标区分）。该选项与视图模式正交，默认关闭，关闭时行为与旧版完全一致。开启后未变更文件不参与 Stage/Discard/SaveAsPatch/StageAll 操作（菜单项在仅选中未变更文件时自动禁用），避免误操作。数据源通过 `git ls-files --cached -z` 获取全部已跟踪文件，与变更文件做差集后合并，git 调用在后台线程执行不阻塞 UI。新增 `ChangeType.Unchanged` 枚举值表示未变更文件。设置持久化到 `ShowFullWorkingDirectory` 配置项。Amend 模式下不启用此功能（staged 数据源不同，避免混淆）。

## v3.7.2

### Bug 修复

- 修复"检查远端同步状态"和"跟踪"三级菜单中，分支名含多个 `_` 时第一个 `_` 后内容丢失不显示的问题。根因是 WPF 的 `MenuItem.Header` 当为 string 时，`_` 是助记符（mnemonic）前缀：第一个 `_` 会被吞掉，其后的字符带下划线作为 Alt 快捷键。例如分支名 `dev_test_branch` 会显示成 `devtest_branch`（`t` 带下划线、第一个 `_` 消失）。修复方式：对分组 Header（远端名）和分支项 Header（分支短名）中的 `_` 转义为 `__`（WPF 渲染为单个 `_`），并把原始未转义名存入 `MenuItem.Tag` 供搜索框过滤使用，避免转义后的 `__` 干扰用户输入的 `_` 匹配。

## v3.7.1

### Bug 修复

- 修复二进制文件（约 2MB）在变更视图展开时界面卡死的问题。根因是 Hex Diff 视图的 SetContent 在 UI 线程同步执行了字节拷贝、hex 文本格式化、逐字节差异比较、MD5 计算等重活。现已将数据准备阶段移至后台线程，UI 线程只保留 AvalonEdit 文档赋值与高亮重绘（DispatcherObject 必须在 UI 线程访问）。同时加入 CancellationToken：快速切换文件或控件移除时取消未完成的加载，避免旧内容回填。
- 继续修复 1.7MB 量级二进制文件（如 mp4）展开后界面仍卡死的问题。异步化后真正的卡点是 UI 线程上 AvalonEdit 对超长 hex 文本（1.7MB 字节 → 约 8MB 文本 ×2 editor）同步重建 DocumentLine 行树。新增 256KB 单边渲染截断阈值：超过则只格式化并渲染前 256KB，末尾追加截断提示，逐字节 diff 高亮也限定在截断范围内；MD5 仍对完整字节计算（后台线程），hash 完整性不受影响。同时 UI 回调用 Dispatcher.Yield 分帧执行两侧 editor 的赋值与高亮，避免单次回调长时间占用 UI 线程。
- 再次修复 256KB 截断阈值下 1.7MB mp4 仍卡死的问题。根因有二：其一，单边渲染阈值 256KB 偏高，格式化后约 1.2MB 文本 ×2 editor 串行赋值 + WPF 首帧布局仍可达秒级阻塞；其二，HexDiffUserControl 误实现为 FileContentControl.IFileContentControlSubControl，而它实际宿主在 FileDiffControl（DiffControlContainer）下，DiffControlContainer.ShowSubView 切换子控件时只识别 IFileDiffControlSubControl，导致旧控件的取消回调永不触发、_loadCts 不被取消，旧后台 Task.Run 继续往 UI 线程投递大文本赋值，多次切换后重活排队累积成卡死。现已将单边渲染阈值降至 64KB（格式化后约 315KB 文本，AvalonEdit 处理 <80ms，绝不卡顿），并将接口修正为 DiffControlContainer.IFileDiffControlSubControl，使切换文件时正确取消未完成的异步加载。
- 进一步改为增量加载模式，彻底消除 64KB 仍偶发卡顿的问题。首屏只渲染前 16KB（格式化后约 80KB 文本，AvalonEdit 处理 <30ms，绝不卡），编辑器下方显示"加载更多"按钮，点击后后台格式化下一段 16KB 并通过 TextDocument.Insert 增量追加到文档末尾（避免整串 base.Text= 重建行树），同时刷新差异高亮范围。完整字节保留在内存供后续加载，MD5 仍对完整字节计算。按钮文案显示本次追加量与剩余未加载量。

## v3.7.0

### 新特性

- 偏好设置新增「导入/导出」页：支持将 ForkPlus 配置（settings.json、custom-commands.json、accounts.json）打包导出为 zip 文件，或从 zip 导入以在另一台机器上恢复。导出时可选是否包含账号（含 API token 等敏感凭据）。导入会覆盖当前配置并自动重启应用以生效。zip 内只接受白名单文件，防止路径穿越。

## v3.6.5

### 新特性

- 二进制文件 Hex Diff 视图新增 MD5 行：在"每行字节数"工具栏下方、"修改前/修改后"列头上方插入一行，左右两列分别显示修改前、修改后字节流的 MD5（小写十六进制），用等宽字体显示，便于快速对比两侧内容是否一致。

## v3.6.4

### 新特性

- 菜单栏「窗口 → 切换主题」从原来只能在 Light/Dark 之间 toggle 的单项，改造为与工具栏 Appearance 下拉一致的二级菜单：非纯色主题直接列出、"纯色"三级菜单装 14 套纯色主题、"自定义颜色..."单项打开自定义颜色对话框。菜单栏现在也能选到全部 22 套预设皮肤及自定义颜色。

## v3.6.3

### Bug 修复

- 修复添加账号窗口所有服务图标都显示成 Remote.png 的问题：根因是业务层 `IconKeys` 用带 `Remote.` 前缀的键（`Remote.Azure` / `Remote.Bitbucket` / `Remote.Github` / `Remote.Gitlab` / `Remote.Gitea` / `Remote.Generic`），而 XAML 资源字典里只定义了 `XxxIcon` 键（`AzureIcon` / `BitbucketIcon` 等），两套键对不上，`FindImage` 全部返回 null 退化成 `GenericRemoteIcon`。已在两个 Images 主题字典和 Geometries 字典中为每个 `Remote.Xxx` 键追加别名条目指向同一份资源，图标现在能正确区分。

## v3.6.2

### Bug 修复

- 继续修复 .NET 10 迁移后 `UseShellExecute` 默认值变更导致的功能失效：用默认编辑器打开工作区文件、打开历史版本临时文件、打开应用数据文件夹三处 `Process.Start` 均失效，现已补齐 `UseShellExecute = true`。

## v3.6.1

### Bug 修复

- 修复"在文件资源管理器打开/显示"功能失效：根因是 .NET 10 迁移后 `UseShellExecute` 默认值变为 false，工具栏"Open in File Explorer"用文件夹路径直接 `Process.Start` 时因非可执行文件抛异常失效；同时修正 `explorer /select,` 逗号后多余空格导致新版 Windows 不选中目标文件而是打开"文档"库的问题。

## v3.6.0

### 新特性

- 代码行数统计支持临时排除目录/文件：统计界面新增"排除"输入框，每行一个 glob 模式（语义同 .gitignore，如 `tests/`、`**/*.Tests/`、`bin/`），点"按排除重新统计"即可重跑 tokei 并过滤对应路径。配置不持久化，关闭即清空。

## v3.5.3

### Bug 修复

- 修复检查更新点击"下载"按钮无响应、不跳转浏览器的问题：根因是 .NET 10 迁移后 `UseShellExecute` 默认值变为 false，导致 `Process.Start` 不会唤起默认浏览器。

### 优化

- tokei（代码行数统计）改为从 hebin123456/tokei 仓库最新 release 拉取预编译二进制，不再从源码 cargo 编译，CI 构建更快、无需 Rust 工具链。
- CI 触发条件调整：master 分支 push 不再触发构建，仅 tag（`v*`）和 master-update 分支触发。

## v3.5.2

### Bug 修复

- 修复 AI 辅助开发界面 AI 回答内容过长时溢出消息容器、撑爆整页滚动的问题（单条消息 WebView2 限高 + 内部滚动）。
- 修复未配置 AI 时点开"AI 开发"弹出的原生 MessageBox 未国际化的问题，改用 ForkPlus 自带提示框，并提供"打开偏好设置"按钮直达 AI Enhancement 配置页。
- 修复 AI 总是回复"没有目录读取权限"的问题：AI 辅助开发此前没有任何文件访问能力，导致它误报无权限。现已为其提供仓库内只读文件系统访问能力。

### 新特性

- AI 辅助开发新增仓库文件系统只读访问能力：AI 可通过 `<list_dir>`/`<read_file>` 标签请求列出目录、读取文件内容，由 ForkPlus 本地执行后回填给 AI，使其能自主浏览代码、理解上下文，不再误报权限问题。
- AI 未配置提示框新增"打开偏好设置"入口，一键跳转到 AI Enhancement 标签页。

## v3.5.1

### Bug 修复

- 修复运行时缺少 `ForkPlus.AskPass.dll` / `ForkPlus.RI.dll`，导致凭据/SSH 询问弹窗无法弹出、交互式 rebase 助手无法启动的问题。

## v3.5.0

### 框架迁移：.NET Framework 4.7.2 → .NET 10

- 整个解决方案从 .NET Framework 4.7.2 迁移到 .NET 10 LTS（TFM：`net10.0-windows10.0.19041.0`，Windows-only）
- 主工程 `ForkPlus.csproj` 改为 SDK 风格工程：`Microsoft.NET.Sdk.WindowsDesktop` → `Microsoft.NET.Sdk` + `<UseWPF>true</UseWPF>`
- 子进程工程 `ForkPlus.AskPass` / `ForkPlus.RI` 同步迁移到 `net10.0-windows10.0.19041.0`
- 删除 6 个旧 Reference（PresentationCore / WindowsBase / System.Core / System.Net.Http / System.IO.Compression / System.IO.Compression.FileSystem），由 SDK 隐式提供
- 删除 `App.config`（assemblyBinding / enforceFIPSPolicy / supportedRuntime 在 .NET 10 无效）

### 跨平台原生库加载

- `Bt.cs` 引入 `NativeLibrary.SetDllImportResolver`：51 处 `[DllImport("biturbo.dll")]` 在静态构造里按 OS 重定向到 `biturbo.dll` / `libbiturbo.so` / `libbiturbo.dylib`
- `RestoreBiturbo` MSBuild target 多平台化：Windows 用 PowerShell 拉 `biturbo.dll`，Unix 用 bash + curl 按 `uname -s` 选 `.so` / `.dylib`

### 测试框架升级

- 单元测试工程 `ForkPlus.Tests` / `ForkPlus.AskPass.Tests` / `ForkPlus.RI.Tests`：`Microsoft.NET.Test.Sdk` 升级到 17.13.0，TFM 同步迁移
- 系统测试工程 `ForkPlus.AutomationTests`：FlaUI 从 3.2.0 升级到 5.0.0（API 无破坏性改动，仅移除旧 TFM + 添加 nullable 注解）
- 反射加载改用 `AssemblyLoadContext.Default.LoadFromAssemblyPath` 替代 `Assembly.LoadFrom`（.NET 10 推荐方式）
- 路径查找改用 `AppContext.BaseDirectory` 替代 `AppDomain.CurrentDomain.BaseDirectory`
- .NET 10 下 `.exe` 是 native apphost，托管代码在同名 `.dll` 中：测试工程同时拷贝 `.exe` 和 `.dll`

### 过时 API 升级

- NLog：`LayoutRenderer.Register<T>(string)` → `LogManager.Setup().SetupExtensions(s => s.RegisterLayoutRenderer<T>("..."))`
- `WebClient` → `HttpClient`（NetworkHelper.cs）；AvatarManager.cs 局部 `#pragma` 静默（事件回调模式留待后续重构）
- `PipeStream.Read` 改循环读满（修复 CA2022 inexact read bug）

### CI 工作流

- GitHub Actions 改为多平台 matrix（windows / ubuntu / macos），`fail-fast: false` 并行
- Windows runner 跑完整流程：restore → build → 单元测试 → AskPass/RI 测试 → 上传 artifact → tag 时打 release zip
- Linux/macOS runner 暂只跑 biturbo 原生库拉取冒烟测试（WPF `net10.0-windows` 在非 Windows 上无法构建完整产物）
- `setup-msbuild` / `setup-nuget` 替换为 `setup-dotnet@v4` 安装 .NET 10 SDK
- `msbuild /t:Restore` → `dotnet restore`，`msbuild /p:Configuration=Release` → `dotnet build`
- 修复 .NET 10 + WPF testhost 偶发不退出导致的 CI 误判：解析 `dotnet test` 输出中的 `Passed! - Failed: N` 判定结果

### 构建产物优化

- 7 个 csproj 全部加 `<GenerateDocumentationFile>false</GenerateDocumentationFile>`：不再生成 `*.xml` 文档注释导出文件
- `RemoveDuplicateWebView2Loader` target 重写为 `CopyWebView2LoaderToRoot`：按 `$(PlatformTarget)` 选 `win-x64/win-x86/win-arm64` 对应的 `WebView2Loader.dll` 拷到 bin 根目录，再删 `runtimes\` 子目录
- 警告清理：NoWarn 静默 WPF/INPC 模式常见无害警告（CS0067/CS0108/CS0169/CS0414/CS1522/CS0652/CS8073/CS8632/CA1416），构建日志 `0 Warning(s)`

### 环境要求变更

- Windows 10 或更高版本（不变）
- **Visual Studio 2022 17.13+，或 .NET 10 SDK（含 Windows Desktop runtime）**
- Git 2.31 或更高版本（不变）
- git-mm 3.0 或更高版本（不变）


## v3.4.1

### Bug 修复

- 修复外观下拉"纯色"二级菜单无法展开的问题
- 修复图片 diff 视图模式按钮（Side-by-Side / Swipe / Onion Skin）未国际化的问题
- 修复 Hex Diff 顶部"源/目标"标签未与下方编辑器对齐，并改名为更直观的"修改前/修改后"
- 修复 Reflog History 窗口列头及"View Reflog..."菜单项未国际化的问题
- 修复重启 ForkPlus 后撤销栈为空时无法打开 Reflog History 界面的问题
- 修复 Reflog 跳转对话框（Jump to HEAD to xxxx / This will reset your xxxx）未国际化的问题
- 修复 commit 完成后撤销按钮未激活的问题
- 修复「Compose WIP into commits...」快捷键与「Commit & Push」重叠的问题，改为 Ctrl+Alt+Enter
- 修复撤销/重做过程中状态栏标题（Stage / Unstage / Reset File / Delete 'X' / Add remote 'X' 等）未国际化的问题

### 新特性

- 图片等二进制 diff 新增 Hex 视图切换按钮，可用 side-by-side 十六进制对比原始字节
- 工具栏新增独立的 Reflog 按钮，始终可用（不依赖撤销栈状态）


## v3.4.0

### Layer 2：工作区级快照（追平 Tower）

v3.3.0 只能 undo HEAD 移动类操作（commit/checkout/reset 等）。v3.4.0 把 discard/stage/unstage/delete branch 这 4 类工作区高频操作也纳入 Undo/Redo 栈，追平 Tower 的工作区级 undo 能力。

#### 数据结构扩展：UndoEntry 增加 PreOperationStashSha

- **`UndoEntry` 新增第 5 字段 `PreOperationStashSha`**：操作前用 `git stash create --include-untracked` 抓的工作区快照 sha。
  - 工作区干净时为 null（HEAD 移动类操作通常如此，节省一次 stash apply）
  - 失败时为 null（降级到 v3.3.0 行为，只恢复 HEAD）
  - Undo 时用 `git stash apply --index <sha>` 恢复工作区 + index 状态
- **向后兼容**：构造函数第 5 参数默认 null，v3.3.0 调用方无需修改。

#### 命令扩展

- **`SnapshotGitCommand`**：新增 `ReadStashCreate` 调 `git stash create --include-untracked`（git < 2.35 回退到不带该选项）。从 2 次 git 进程 → 3 次。
- **`RestoreSnapshotGitCommand`**：新增第 3 步，如有 `PreOperationStashSha` 调 `git stash apply --index`。失败不阻断（HEAD 已恢复，工作区冲突让用户手动解决）。

#### 4 类工作区操作纳入 Undo/Redo

| 操作 | 修改前 | 修改后 | Undo 行为 |
|---|---|---|---|
| Discard 文件变更 | `JobQueue.Add`（不进栈） | `AddUndoable` | stash apply 恢复被丢弃的变更 |
| Stage 文件 | `JobQueue.Add`（不进栈） | `AddUndoable` | stash apply --index 恢复 stage 前的 index 状态 |
| Unstage 文件 | `JobQueue.Add`（不进栈） | `AddUndoable` | stash apply --index 恢复 unstage 前的 index 状态 |
| Delete local branch | 直接 Execute（无队列） | `AddUndoable` | stash apply + reset 恢复分支引用和工作区 |
| Delete remote branch | 直接 Execute（无队列） | `AddUndoable` | 恢复本地 tracking ref（远程需 push 重建） |

修改文件：
- `DiscardChangedFilesCommand.cs`：`JobQueue.Add` → `AddUndoable`，返回 `discardResult`
- `ToggleFileStageCommand.cs`：Stage 和 Unstage 两处 `JobQueue.Add` → `AddUndoable`
- `RemoveLocalBranchWindow.xaml.cs`：`JobQueue.Add` → `AddUndoable`，用 `finalResult` 跟踪多分支结果
- `RemoveRemoteBranchWindow.xaml.cs`：同上

### UX 增强：Reflog 视图

v3.3.0 的 Undo 下拉只能看栈内 50 条历史。v3.4.0 新增 Reflog 视图，让用户能看到完整 reflog（默认 200 条），包括超栈深度（LostCount）以外的历史，并能从任意历史状态恢复。

#### 新增 ReflogWindow

- **新建 `ReflogWindow.xaml` + `.xaml.cs`**：非模态工具窗口（可同时操作仓库和看 reflog）。
- **ListView 展示**：Index（HEAD@{N}）/ SHA 前 8 位 / Operation / Commit Subject / Time（本地时区）。
- **`UndoIndexStore` left-outer join**：命中索引显示 UI 友好操作名（如 "Commit 'fix: bug'"），未命中降级显示 reflog 原生 subject（如 "commit: fix: bug"）。
- **双击跳转**：弹窗确认后走 `AddUndoable("Jump to HEAD@{N}", reset --hard <sha>)`，让用户能 Undo 回到跳转前状态。
- **Refresh 按钮**：重新加载 reflog。

#### 工具栏入口

- **Undo 下拉菜单底部**加 "View Reflog..." 入口（始终可见，让用户能看完整 reflog 历史 + 跳转）。
- **Redo 下拉菜单底部**对称加上同样入口。
- `ShowReflogWindow` 方法非模态打开（`window.Show()` 而非 `ShowDialog()`）。

#### ReflogEntry 扩展

- **`ReflogEntry` 新增 `TimestampUtc` 字段**：解析 `git reflog --pretty=%H%x00%gs%x00%s%x00%ci` 的第 4 字段。
  - `%ci` 格式：`yyyy-MM-dd HH:mm:ss ±zz`，解析为 UTC DateTime。
  - 解析失败静默返回 null（不抛出），其他字段仍正常解析。
- **`ReflogHistoryProvider.ReadHeadReflog`**：reflog 格式从 3 字段扩展到 4 字段（增加 `%ci`）。

### Ctrl+Z 快捷键（v3.0.0 已实现，本次验证）

- **Ctrl+Z** → Undo（`UndoCommand.Shortcut`，v3.0.0）
- **Ctrl+Shift+Z** → Redo（`RedoCommand.Shortcut`，v3.0.0）
- **Ctrl+Y** → Redo（`RedoCommand.SecondaryShortcut`，v3.0.0）
- **作用范围**：主窗口内有效（WPF `CommandBindings`，不抢其他应用快捷键）。`MainWindow.InitializeKeyBindings()` v3.0.0 已注册，本次仅验证无需重做。

### 单元测试

- **`UndoRedoStackTests` 新增 5 个 PreOperationStashSha 测试**：默认 null / 显式赋值 / WithOperationName 保留 / null 归一化 / 栈操作中保持完整。
- **`ReflogHistoryProviderTests` 新增 5 个 TimestampUtc 测试**：+0800 时区解析 / +0000 时区解析 / 老格式无时间 / 空时间 / 格式错误静默返回 null。
- **新增 `ReflogViewItemTests`**（14 个测试）：IndexDisplay 格式 / ShaDisplay 截断 / 短 sha / 空 sha / OperationName 传递 / null 归一化 / CommitSubject / TimeDisplay 本地时间转换 / 空 timestamp。

### 设计说明

- **stash create 而非 write-tree**：`git stash create --include-untracked` 是 git 原生命令，能完整捕获 tracked + untracked 文件变更 + index 状态，且不写入 stash list（悬空 commit，对仓库无副作用）。
- **stash apply 失败不阻断**：HEAD 已恢复是核心目标，工作区冲突让用户手动解决（避免强行 reset 丢数据）。
- **Reflog 视图非模态**：用户可以同时操作仓库和看 reflog，符合工具窗口使用习惯。

## v3.3.0

### 重构：Undo/Redo 分层架构

把 Undo/Redo 系统从「单一内存快照栈」改造为「reflog 真相源 + 索引文件元数据」的分层架构，对标 Sublime Merge 的持久化机制。**完全打破旧代码**：删除 `RepositorySnapshot`，用更轻量的 `UndoEntry` 替代。

#### Layer 1：reflog 作为真相源（持久化 + CLI 兼容）

- **新增 `ReflogHistoryProvider`**：读取 `git reflog HEAD --pretty=format:%H%x00%gs%x00%s`，解析为 `List<ReflogEntry>`，NUL 分隔字段避免 commit message 换行干扰。
  - reflog 是 git 原生持久化的（`.git/logs/HEAD`，默认保留 90 天），跨会话保留 + CLI 操作天然兼容 + 无栈深度限制。
  - 默认读取最近 200 条（防止超大 reflog 拖慢 UI）。
  - 读取失败永不抛出，返回空列表（不阻断 Undo/Redo）。

#### Layer 0：索引文件保留 OperationName

- **新增 `UndoIndexStore`**：读写 `.git/forkplus-undo-index.json`，存储 `{HeadSha → UndoIndexEntry}` 映射，为 reflog 条目附加 UI 友好的操作名（如「Commit 'fix: bug'」「Checkout 'feature/x'」）。
  - **位置**：`.git/forkplus-undo-index.json`（与 reflog 同生命周期，clone 后是空的）。
  - **原子写入**：先写 `.tmp` 再 rename，避免崩溃导致文件损坏。
  - **文件损坏静默恢复**：JSON 解析失败时删除文件重建，不阻断 Undo/Redo。
  - **容量上限**：默认 500 条，LRU 淘汰（按 TimestampUtc 排序删最早的）。
  - 索引与 reflog 不同步时降级显示 reflog 原生 message（如 `commit: fix: bug`），不报错。
- **新增 `UndoIndexEntry`**：4 字段（HeadSha / OperationName / TimestampUtc / OperationType）。`OperationType` 预留给 v3.4+ 的 UI 图标。

#### 数据结构精简：UndoEntry 替代 RepositorySnapshot

- **新增 `UndoEntry`**：4 字段（HeadSha / CurrentBranchName / OperationName / TimestampUtc），替代旧 `RepositorySnapshot` 的 11 字段。
  - HEAD sha 是恢复真相源，所有 ref 状态都跟着 sha 走。
  - OperationName 通过 UndoIndexStore 持久化到 `.git/forkplus-undo-index.json`。
  - 当前分支名用于 Undo 后切回原分支（避免进入 detached HEAD）。
  - 含 `WithOperationName()` 副本方法，支持「先抓快照、后赋名」场景。
- **删除 `RepositorySnapshot`**（含 `RepositorySnapshotTests`）：不再保存 branch list / tag list / stash list / ORIG_HEAD / IsWorkingTreeDirty / ChangedFilesCount 等 11 字段。
  - 旧版重建分支 / tag / stash 的逻辑反而可能产生副作用（如重建已被用户故意删除的分支）。
  - 这些状态在 Undo 时由 reflog 兜底恢复，无需在快照里冗余保存。

#### 命令简化

- **`SnapshotGitCommand`**：从 7 次 git 进程调用简化为 2 次（`git rev-parse HEAD` + `git symbolic-ref --short -q HEAD`），性能提升 ~70%（大仓库尤其明显）。
- **`RestoreSnapshotGitCommand`**：从 5 步组合命令（checkout + reset --hard + 重建分支 + 重建 tag + 重建 stash）简化为 2 步（checkout 切回原分支 + `git reset --hard <sha>`）。

#### UI 层适配

- **`RepositoryUserControl.AddUndoable`**：操作成功后写入 `UndoIndexStore.Record(...)`，把 OperationName 持久化到 `.git/forkplus-undo-index.json`。
- **`RepositoryUserControl.IsWorkingTreeDirty()`**：实时调 `git status --porcelain` 检测工作区是否 dirty，替代旧 `RepositorySnapshot.IsWorkingTreeDirty` 字段。
- **`ToolbarUserControl`**：4 处 `RepositorySnapshot` 引用改为 `UndoEntry`，下拉历史列表 / JumpUndoTo / JumpRedoTo 签名同步更新。

#### 单元测试

- **重写 `UndoRedoStackTests`**：20 个测试覆盖栈空 / MaxDepth / LostCount / JumpTo / CancelLastRecord 等纯逻辑，新增 3 个 UndoEntry 数据结构测试（null OperationName 归一化、WithOperationName 副本）。
- **新增 `ReflogHistoryProviderTests`**：11 个测试覆盖 ParseLine 各种输入（合法行 / 缺字段 / 空行 / 短 sha / 多 NUL 字段 / amend / checkout 类 subject）。
- **新增 `UndoIndexStoreTests`**：19 个测试覆盖 GetIndexPath / Load / Record / Lookup / 容量淘汰 / 文件损坏恢复 / 空文件 / 跨实例持久化 / 特殊字符 / 原子写入不留 .tmp。用临时目录 + 真实 GitModule 实例，不依赖真实 git 进程。

### 设计决策（v3.4+ 待办）

- **Layer 2（工作区级快照）**：追平 Tower 的 discard / stage / 删 branch undo 能力，待 v3.4 实现。
- **UX 增强**：Reflog 视图（与 reflog 兜底联动）、全局 Ctrl+Z 快捷键、Reflog 视图入口，待 v3.4 实现。

## v3.2.0

### 新特性

- **AI Commit Composer（WIP 拆分）**：在 Commit 下拉菜单中新增「Compose WIP into commits...」入口，一键把当前所有 staged 文件按逻辑分组拆成多个独立 commit。
  - **AI 流式生成方案**：调 OpenAI Chat Completions API（流式 SSE），让 AI 根据 staged diff 把文件归类成多个 commit 分组，每个分组给出 subject / body / files / reason，diff 体量超 30000 字符时自动截断防爆 token。
  - **三栏预览窗口**（`AiCommitComposerWindow`）：左栏列出所有 commit 分组、中栏列出选中分组包含的文件、右栏可编辑 subject / body；AI 给出但未匹配到 staged 文件的路径会以橙色「(not staged)」标识；底部提示「N 个 staged 文件未分配到任何分组」，方便用户核对。
  - **可编辑 + 可撤销 + 可取消**：用户可在右栏修改任意分组的 subject / body；分组 subject 为空时会弹窗拦截（git 不允许空 message）；执行期间进度条 + 状态文本实时反馈（如「Composing commit 2/5: refactor auth module」），可随时点 Stop 中止。
  - **执行流程**：点 Apply All 后用 `ComposeWipCommitsGitCommand` 按「先 `git reset HEAD --` 清空 staging，再逐组 stage + commit」顺序执行；空仓库（无 HEAD）时容错忽略 `ambiguous argument 'HEAD'` 错误；任一分组失败立即中止，已提交分组不回滚（与手动 commit 行为一致）。
  - **集成 Undo/Redo 栈**：用 `RepositoryUserControl.AddUndoable` 包裹整批 commit，与 v3.0.0 引入的 Undo/Redo 栈联动，用户可一键撤销整批拆分。
  - **模型下拉**：标题栏内置 AI 模型下拉，复用 AI Review 设置，与 AI Development / AI Code Review / AI Text Result 窗口行为一致。
  - **路径匹配鲁棒性**：AI 给出的路径与 staged 文件路径可能存在大小写 / 分隔符差异，`WipCommitPlan.RebuildMatchedFiles` 用 `NormalizePath`（替换 `\\` 为 `/`、`TrimEnd('/')`、`ToLowerInvariant`）做归一化匹配；重命名 / 复制文件的 `OldPath` 也加入索引，让 AI 给出的旧路径也能命中。
  - **JSON 解析鲁棒性**：`ExtractJsonArray` 用状态机遍历字符串字面量，正确处理嵌套方括号和 markdown 围栏；支持 `{ "groups": [...] }` 和直接 `[...]` 两种格式。
- **国际化**：8 种语言（简中 / 繁中 / 日 / 韩 / 法 / 德 / 西 / 英）补齐 AI Commit Composer 相关 26 条文案。

## v3.1.1

### 新特性

- **外观菜单 - 纯色二级菜单**：将原来平铺在外观下拉里的紫色、绿色主题收拢到「纯色」二级菜单中，并新增 5 种纯色配色（红、橙、黄、青、蓝），每种都有浅色 / 深色两个变体，按彩虹色排序（红→橙→黄→绿→青→蓝→紫）。父菜单「纯色」与子菜单中当前选中的颜色都会打勾。
- **Hex Diff - 左右行对齐**：二进制对比界面工具栏新增「左右行对齐」复选框，默认勾上。勾上时左右两个 HexEditor 同步滚动 —— 一侧拉到第 N 行，另一侧立即跟随到第 N 行。采用 100ms 防抖 + 重入守卫，避免两侧相互触发滚动事件形成回环。

### 修复与改进

- **Undo/Redo 默认开启**：`UndoRedoEnabled` 默认值从 `false` 改为 `true`，新用户开箱即用，无需手动到偏好设置中勾选。
- **Undo/Redo 开关文案国际化**：偏好设置中 `Enable Undo/Redo (experimental, may impact performance on large repos)` 此前为硬编码英文，现已本地化到 8 种语言。
- **修复提交后状态栏一直转圈 / 取消不掉**：用户启用 Undo/Redo 后提交一个文件，撤销栈里已出现该提交，但状态栏仍显示「Commit 1 File」并一直转圈、无法取消。根因是 Job 状态机在取消信号与完成信号之间存在多处覆盖漏洞，本次系统性修复：
  - `JobMonitor.Update` / `Success` / `Fail` 在 `_state == Canceled` 时直接返回，不允许把 Canceled 改回 InProgress / Succeeded / Failed（否则取消信号被吞掉，状态栏继续转圈、Job 实际完成、栈里仍入 entry）。
  - `JobQueue.Schedule` 用 `try/finally` 包裹 `job.Run()`，确保 action 抛异常时 Job 也能从 `_runningJobs` 移除、`Status` 置为 `Finished`，否则 `IsIdle` 永远为 `false`、状态栏永远转圈。
  - `RepositoryUserControl.AddUndoable` 在 action 返回后检查 `monitor.IsCanceled`，已取消时调用 `CancelLastRecord` 弹出栈顶 entry，避免栈里留下「已取消但未弹出」的孤儿。
  - `CommitCommand` 在 commit 成功回调里加 `!monitor.IsCanceled` 守卫，已取消时不再调 `monitor.Success(null)`。

## v3.1.0

### 新特性

- **Binary / Hex Viewer**：为二进制文件新增 Hex 视图，复用 AvalonEdit 的虚拟化、选中、搜索能力，替代原先仅显示文件大小的 "Binary file" 占位符。
  - **单文件 Hex 视图**（`HexContentControl` + `HexEditor`）：点击工作区或提交里的任意二进制文件（图片除外，<=10MB 自动加载，>10MB 仍走原 Binary 视图），即以 Offset / Hex / ASCII 三列展示。工具栏支持：
    - 字节宽度切换（8 / 16 / 32 字节每行）
    - 显示/隐藏 ASCII 列、显示/隐藏 Offset 列
    - 搜索（支持 ASCII 文本或十六进制字节，如 `41 42`）
    - 复制选中原始字节到剪贴板
  - **Hex Diff 视图**（`HexDiffUserControl`）：二进制文件的 Diff 不再只显示两侧大小，而是 side-by-side 展示两份字节流，逐字节比较，差异字节以金色（Gold）背景高亮。<=10MB 的二进制 diff 自动加载 Hex 视图，超过则回退到原 `BinaryDiffUserControl`。
  - **三列着色**（`HexColorizer`）：Offset 列灰色、Hex 列蓝色（高位）/暗红色（低位）、ASCII 列绿色；不可打印字符显示为 `.`；差异字节在 Hex 列与 ASCII 列同时加背景。
  - **设置持久化**：在偏好设置中新增 `HexViewBytesPerRow` / `HexViewShowAscii` / `HexViewShowOffset` 三项，记忆用户上次选择的字节宽度和列显示偏好，单文件视图与 Diff 视图共享设置。
  - **头部工具栏**：新增 `FileControlHeaderMode.Hex` 枚举值，Hex 视图下隐藏 Text/Image 工具栏按钮（Hex 视图自带工具栏），仅显示文件路径。
- **国际化**：8 种语言（简中 / 繁中 / 日 / 韩 / 法 / 德 / 西 / 英）补齐 Hex 视图相关文案（Bytes per row / Show ASCII / Show offset / Source / Destination / Search / Copy as raw bytes）。

## v3.0.4

### 修复与改进

- **Undo/Redo 总开关（默认关闭）**：在偏好设置 → 通用 tab 新增 "Enable Undo/Redo" 复选框，默认不勾选。关闭时 `AddUndoable` 直接走原始 `JobQueue.Add`，跳过所有快照抓取逻辑，性能回到 v3.0.0 之前的水平。需要 Undo/Redo 功能的用户可手动开启。
- **Undo/Redo 性能优化**：修复"提交一条信息要转很久、取消也停不下来"的卡顿问题。根因是 `AddUndoable` 在 UI 线程同步抓取 7 次 git 进程快照（包括 `git status --porcelain`，大仓库很慢），且不响应取消。优化后：
  - 开关开启时，`TakeSnapshot` 推迟到 Job 内（后台线程）执行，UI 线程立即返回，不再阻塞
  - 在抓快照阶段检查 `monitor.IsCanceled`，用户取消时立即跳出，不再卡死
- 工具栏 Undo/Redo 按钮根据开关显示/隐藏（关闭时 Collapsed），设置变更后立即刷新

## v3.0.3

### 修复与改进

- **Undo/Redo 图标改为 PNG 资源**：原先用 `Viewbox+Path` 矢量绘制（v3.0.2 改用 Material Design path 但仍是矢量），与工具栏其他按钮（Fetch/Pull/Push/Stash 均为 40×40 PNG 资源）风格不一致。本次新增 4 个 PNG 资源 `Undo.png` / `UndoDark.png` / `Redo.png` / `RedoDark.png`（40×40 RGBA，light=#797979、dark=#CFCFCF，与现有图标颜色规范一致），并在 `Images.Light.xaml` / `Images.Dark.xaml` 注册 `UndoIcon` / `RedoIcon` 资源，工具栏按钮改用 `Image` + `DynamicResource` 引用，行为与 Fetch/Pull/Push/Stash 完全一致（主题切换时自动跟随 light/dark 版本）。

## v3.0.2

### 修复与改进

- **Undo/Redo 图标重绘**：原先的矢量图标（简单弧形 + 三角形箭头）过于粗糙。改用 Material Design 标准的 undo/redo 图标（24×24 viewBox，filled 风格，弯曲箭头更精细），在 20×20 工具栏尺寸下更清晰、与业界习惯一致。
- **Undo/Redo 性能优化**：合并 `SnapshotGitCommand` 和 `RestoreSnapshotGitCommand` 里的冗余 git 进程调用：
  - `git status --porcelain` 从 2 次合并为 1 次（同时拿 `IsWorkingTreeDirty` 和 `ChangedFilesCount`）
  - `git for-each-ref` 从 2 次合并为 1 次（`refs/heads/` + `refs/tags/` 一次拿全，按 `%(refname)` 前缀分发）
  - 每次 Undo/Redo 减少约 3 次 git 进程启动（小仓库约省 150-450ms），缓解"Redo 后状态栏转圈"的卡顿感。

## v3.0.1

### 修复与改进

- **Undo/Redo 工具栏按钮对齐**：按钮样式从 `ToolbarButton` 改为 `StashToolbarButtonStyle`，与 Stash 按钮组视觉一致（左圆角 + 右侧 dropdown 形成"按钮组"整体感）；图标用 `Viewbox` 包裹并限定为 20×20 + `Stretch=Uniform`，与其他按钮（Fetch/Pull/Push/Stash 都是 20×20 Image）大小对齐。
- **右键"AI 解释提交..."位置调整**：移到"还原提交"下面、"另存为补丁..."上面，符合"AI 操作紧跟相关 Git 操作"的菜单分组约定。
- **AI 文本结果窗口（AI 解释 commit / AI 生成 PR 描述）加模型下拉**：在 Copy 按钮左侧新增模型下拉，列表从 `/v1/models` 拉取，切换后立即保存到设置并生效，下次请求使用新模型；与 AI Development / AI Code Review 窗口行为一致。
- **国际化补齐**：`Copy result to clipboard`、`Stop the current AI task`、`Select AI model` 在 AiTextResultWindow 此前为硬编码英文，现已本地化到 8 种语言。

## v3.0.0

### 新特性

- **Undo / Redo 任意 Git 操作**：参考 GitKraken / Tower，引入仓库级 Undo/Redo 能力，覆盖 commit / checkout / reset / merge / rebase / cherry-pick / revert / create branch / create tag / stash 等所有写操作。每次写操作执行前抓取 HEAD/分支/tag/stash 状态快照入栈，失败不入栈，Undo 时按快照恢复。
- **工具栏 Undo/Redo 按钮组**：在工具栏 Stash 按钮组后新增 Undo / Redo 按钮，旁边的下拉箭头展开历史列表，可直接跳转到任意一步。
- **Undo/Redo 快捷键**：`Ctrl+Z` 撤销，`Ctrl+Shift+Z` 或 `Ctrl+Y` 重做。
- **dirty 工作区弹窗**：Undo/Redo 前若工作区有未提交变更会弹窗询问，可选择先 stash 再恢复，避免误丢工作区修改。
- **已 push commit Undo 弹窗**：Undo 一个已推送到远端的 commit 时弹窗询问处理方式（仅本地 Undo / 本地 Undo + 强制推送 / 取消），防止误改远端历史。
- **超栈深度提示**：Undo 栈上限 50 步，超出丢弃最底部并在下拉历史底部提示「X 个早期操作未在历史中（可通过 reflog 恢复）」。
- **跨会话不持久化**：关闭重开仓库清空 Undo/Redo 栈，避免基于过期快照恢复。

### 国际化

- 8 种语言（简中 / 繁中 / 日 / 韩 / 法 / 德 / 西 / 英）补齐 Undo / Redo / Undo History / Redo History / (unknown) / dirty 弹窗 / 已 push 弹窗 / 超栈深度提示 等文案。

## v2.2.3

### 修复

- **AI 输出内容宽度自适应**：修复 AI Explain / AI 生成 PR 描述等窗口的 markdown 渲染 CSS 中 `max-width: 780px` 硬编码导致窗口拉宽后内容右侧留大片空白的问题，改为 `max-width: 100%` 跟随容器宽度。

## v2.2.2

### 新特性

- **AI 解释 commit 右键菜单**：在所有提交列表（commit 列表 / stash 列表）的右键菜单中，"与本地变更比较"下方新增「AI Explain Commit...」选项，无需进入 commit 详情页即可让 AI 解读任意 commit。AI 未配置时菜单项置灰。
- **部分文件贮藏 AI 命名**：选择若干文件贮藏（Partial Stash）对话框新增「🤖 AI」按钮，根据所选文件相对 HEAD 的 diff 自动生成 stash message，与全量贮藏对话框能力对齐。

### 优化

- **AI Explain 按钮国际化**：commit 详情页的「🤖 AI Explain」按钮文本原为硬编码英文，现按 UI 语言本地化显示（8 种语言）。

## v2.2.1

### 新特性

- **Cherry-pick / Revert 冲突预检**：Cherry-pick 和 Revert 对话框打开时自动用 `git merge-tree` 做无副作用的 3-way merge 预演，在对话框底部状态栏显示「可以无冲突完成」或「将产生冲突」，让用户在执行前心里有数。Cherry-pick 多 commit 场景对每个 commit 逐个预检，任一会冲突即整体提示冲突。

## v2.2.0

### 新特性

- **AI 解释 commit**：commit 详情页新增「🤖 AI Explain」按钮，AI 流式输出该 commit 的概述、变更内容、动机和影响，方便快速理解陌生提交。
- **AI 自动命名 stash**：保存贮藏对话框新增「🤖 AI」按钮，根据工作区 diff 自动生成简洁的 stash message，流式写入输入框。
- **AI 生成 PR 描述**：分支/commit range 右键 AI 菜单新增「Generate PR Description...」，基于 commit 列表和聚合 diff 流式生成结构化 PR 描述（概述/变更内容/测试建议）。

### 优化

- **AI 协助冲突解决扩展到冲突列表**：合并冲突列表页每个文件新增「🤖 AI Resolve」按钮，无需打开三方合并窗口即可一键让 AI 解决该文件所有冲突；SideBySideMergeWindow 的 AI 解决逻辑提取到 OpenAiService 公共方法复用。

## v2.1.5

### 新特性

- **仓库树图问号提示**：仓库树图弹窗标题左侧新增问号图标，鼠标悬停显示说明，解释视图用途、面积含义、操作方式等，支持 8 种语言。

### 优化

- **AI 代码检视流式输出滚动跟随**：流式输出时滚到底部查看新内容，下一个内容块到达后自动跟随最新内容，不再弹回顶部；用户主动上滚浏览历史时保持阅读位置不打断。

### 其他

- 删除 docs 目录下的用户手册。

## v2.1.4

### 修复

- **空仓库无限加载**：`git init` 完毕的新仓库用 ForkPlus 打开不再无限转圈卡死。
- **空仓库状态显示"分离 HEAD"**：空仓库状态栏正确显示当前 branch 名（如 master），不再误显示为"分离 HEAD"。
- **空仓库新建文件夹感知不到**：空仓库在工作区新建文件/文件夹后能正常检测显示，与 `git status` 行为一致。

## v2.1.3

### 新特性

- **自定义颜色导入/导出**：自定义颜色对话框新增"导入颜色"和"导出颜色"按钮，支持 JSON 格式的颜色配置文件，方便分享和备份配色方案。导入时严格校验文件格式（schema、颜色 key 白名单、hex 颜色合法性），格式不对阻止导入并提示具体错误。

## v2.1.2

### 修复

- **随机配色时 Diff 颜色不动**：点击 Random Palette 后 Diff 相关颜色项正常随机变化。
- **换色不立即落盘**：换色后立即保存到 settings.json，关闭/重启不丢失。
- **换色后主界面不刷新**：换色后主界面立即刷新生效，无需重启应用（核心刷新机制重写，模仿主题切换的强力刷新）。

### 优化

- 移除自定义颜色对话框的 OK/Cancel 按钮（换色实时落盘后已失去语义）。
- 注释掉 CI 中的系统测试步骤（windows-latest runner 无交互式桌面会话，WPF UIA 不稳定）。

## v2.1.0

### 新特性

- **用户自定义颜色**：在多预设皮肤基础上，支持对任意皮肤的颜色进行自定义覆盖。主题菜单新增"自定义颜色..."入口，提供 18 个核心颜色的 hex 输入和颜色选择器，改动即时生效，持久化到 settings.json。

## v2.0.0

### 新特性

- **多预设皮肤系统**：从只有 Light/Dark 两个硬编码主题升级为可扩展的多预设皮肤架构，内置 8 套皮肤（Light/Dark/Solarized Light/Solarized Dark/GitHub Light/GitHub Dark/Dracula/Monokai）。兼容旧 settings.json。

## v1.7.0

### 新特性

- **代码行数统计**：仓库统计面板新增代码行数统计区域，集成 tokei 支持 200+ 语言，区分 code/comments/blanks。支持统计当前工作区或历史 commit/分支/tag 快照，提供饼图 + 列表双视图，按占比和明细两个角度看语言分布。
- **分支右键"代码统计"入口**：本地分支右键菜单新增"Code statistics..."，点击以该分支为初始 ref 打开统计窗口并自动滚动到代码行数区域。

## v1.6.4

### 修复

- **仓库树图点击崩溃**：在 Repository Overview 窗口点击文件夹后不再整体崩溃；打开窗口加载完成后也不再崩溃。

### 新特性

- **贡献热力图图例与统计摘要**：贡献热力图下方新增色阶图例（Less/More）和统计摘要（总提交数/最长连续提交天数/最活跃日期）。

## v1.6.3

### 新特性

- **贡献热力图**：统计面板新增 GitHub 风格的 53 周 × 7 天提交热力图，一眼看出近一年的提交活跃度分布，支持按作者统计当天提交数。

## v1.6.2

### 优化

- **跟踪右键改为二级菜单 + 分支级搜索框**："跟踪"右键菜单改为按远端分组的二级菜单，分支那一级顶部加搜索框置顶不受滚动影响，跟踪和检查远端同步状态都复用此模板。

## v1.6.1

### 修复

- **远端同步状态弹窗布局拥挤**：图标和文字不再挤在一起。
- **检查更新"已是最新版本"未显示版本号**：现在显示当前版本号。
- **git mm 子仓变更数量"从有到无"**：子仓变更数字短暂显示后不再变成 0。
- **git mm 子仓视图左侧树/未暂存区为空**：子仓自身的变更不再被误过滤。
- **远端同步状态弹窗显示 `[Dialog Description]` 占位符**：占位符不再暴露。

### 优化

- **"检查 Fork 同步状态"改为"检查远端同步状态"**：表述更准确，不限于 fork 工作流。
- **远端同步状态改为二级菜单选择远端分支**：用户显式选择目标远端分支，立即弹框显示检测中。

### 新特性

- **git mm 子仓右键"作为独立仓库打开"**：子仓 tab 右键菜单新增选项，点击用单仓方式新开一个 tab。

## v1.6.0

### 新特性

- **AI 解决合并冲突**：合并冲突解决窗口新增「🤖 AI Resolve」按钮，一键让 AI 合并两侧变更并解决全部冲突。
- **Fork 工作流同步冲突预检**：push 前预检本地分支与 upstream 目标分支是否会冲突，三态结果展示（安全推送/建议同步/有冲突）。
- **Commit 面板 Gitmoji**：commit subject 输入 `:` 时弹出 gitmoji emoji 选择器（如 `:bug:` → 🐛）。
- **AI 辅助开发对话 Markdown 渲染 + Emoji 彩色显示**：AI 回复改用 WebView2 渲染 Markdown，emoji 显示为彩色。

## v1.5.8

### 修复

- **变更数量大时暂存区/未暂存区被强制平铺**：变更文件数达到 5000 时不再从用户选择的树状自动降级为平铺。

## v1.5.7

### 修复

- **git mm 子仓变更仍不显示**：v1.5.6 修复无效，子仓状态检测命令对齐单仓变更列表参数（含 untracked 文件）。

### 易用性

- **子仓页签右键"打开 git mm 仓"快捷入口**：单仓方式打开的子仓页签右键可快捷跳转到对应的 git mm 页签。

## v1.5.6

### 修复

- **git mm 视图子仓变更不显示**：子仓状态检测命令对齐单仓脏检查参数，规避锁竞争和 fsmonitor 误判。

### 重构

- **AI 代码检视页面**：新增模型下拉选择、状态栏进度承载、流式实时输出、Stop 按钮取消任务、排队/重试状态外显。

## v1.5.5

### 修复

- **git 命令预览过长挤掉确认按钮**：对话框的 git 命令预览区限制最大高度并加滚动条，确认按钮不再被裁出可视区。

## v1.5.4

### 修复

- **AI 排队场景返回错误码**：v1.5.3 修复只对非流式路径生效，本次彻底修复流式路径（影响 AI 辅助开发 + AI 代码检视 + commit 消息生成）。

## v1.5.3

### 优化

- **AI 辅助开发体验**：新增模型下拉选择、需求队列不阻塞输入、停止任务按钮、正确处理排队场景、上下文超长自动压缩、commit 消息即时写入。

## v1.5.2

### 优化

- **AI 辅助开发界面**：AI 按钮迁移到顶部工具栏，对话增加记忆支持连续追问，新增清空按钮和欢迎信息。

### 修复

- **检查更新按钮无反应 + 504 网关超时**：改为"先弹窗后检测"交互，独立 HttpClient 直连 GitHub API 避免系统代理 504。

## v1.5.0

### 新特性

- **自动检测更新**：启动后自动检测新版本（默认 24 小时间隔），帮助菜单新增"Check for Updates..."主动检测，发现新版本时弹出提示窗口。

## v1.4.7

### 优化

- **AI 开发窗口改用流式输出**：AI 生成的文本逐 chunk 实时追加到聊天气泡，不再卡一段时间无输出。
- **AI 开发新增"撤销 AI 修改"按钮**：AI 修改文件后可一键撤销，无需手动 `git checkout`。
- **国际化补全**：AiDevelopmentWindow 中文字面量补齐 7 种语言翻译。

### 修复

- **`PathHelper.GetParent` 空路径崩溃**：对 null/空/非法路径返回 null。
- **单元测试超时**：从 300s 缩短到 120s 减少等待。

## v1.4.6

### 优化

- **AI 检视流式输出 + 超时处理**：OpenAI HTTP 路径改用 SSE 流式输出，Claude CLI 路径新增超时处理。
- **工具栏下拉菜单国际化**：Appearance/Stash/Workspaces 三个下拉菜单的硬编码英文改为本地化。

## v1.4.4

### 新特性

- **命令预览收尾**：补全 6 个执行 git 命令但缺命令预览的弹窗（LeanBranchingStart/Finish、InteractiveRebase、SaveSnapshot、GitLfsTrack、AddGitIgnorePattern）。

### 修复

- **LeanBranchingStartWindow FriendlyName 取错**：改为 `Name` 规避显式接口实现问题。
- **LeanBranchingFinishWindow 编译错误**：修正构造函数括号结构。

## v1.4.3

### 修复

- **新建分支/标签/删除分支弹窗显示 git 命令预览**：构造函数末尾补刷 `RefreshCommandPreview`。
- **"Cannot parse revision" 国际化 + AI 生成提交信息取消后仍写入**：补齐翻译 key，Dispatcher 回调补 `monitor.IsCanceled` 检查。

## v1.4.2

### 修复

- **git mm 下拉框两行**：取版本输出首行去除内嵌换行。
- **交互式变基弹窗闪退**：`Close()` 后补 `return` 避免 NRE。
- **右键"在文件树中显示"闪退**：新增延迟展开模式，`RootItem` 就绪后再展开。
- **追溯/历史弹窗显示 "Cannot parse revision"**：Windows `\r\n` 行尾问题，统一替换为 `\n`。
- **变基/重置分支弹窗默认不显示 git 命令预览**：构造函数末尾补刷。
- **追溯/历史弹窗显示类型名而非错误描述**：基类 `GitCommandError` 重写 `ToString` 返回 `FriendlyDescription`。

## v1.4.1

### 新特性

- **git 命令预览复制按钮**：预览右侧新增复制图标按钮。
- **国际化**：git-mm Instance 标签、远端右键菜单 Edit/Delete 'xxx' 补齐 7 种语言。

### 修复

- **偏好设置打开卡顿**：恢复 `GitMmVersionText` 原始实现，修复版本输出含内嵌换行的问题。

## v1.4.0

### 新特性

- **Git 命令预览**：所有对话框窗口（45 个）底部新增 git 命令预览区域，修改选项时实时更新。

### 修复

- **CI 构建失败**：DeleteWorktreeWindow/CheckoutRevisionWindow 的 struct 与 null 比较问题。
- **打开偏好设置异常**：未找到 git-mm 时 `SelectedItem` 设为 null，添加 `_isRefreshingGitMm` 守卫标志。

## v1.3.4

### 修复

- **所有 push 操作报 "src refspec xxx does not match any"**：移除 `PushGitCommand` 中 5 处 `Quotify()` 调用。

## v1.3.3

### 性能优化

- **启动速度**：合并重复的 git version 子进程，缓存 PATH 遍历结果，git-mm 检测改为后台线程。

### 修复

- **窗口位置/大小/状态不按上次保存恢复**：先设置 WPF 依赖属性再调 `SetWindowPlacement`，新增 `OnStateChanged`。

### 国际化

- 补全 18 个未本地化的命令 Title（Remote/Branch/Tag/Worktree 等），7 种语言各新增 16 个 key。

## v1.3.2

### 修复

- **新文件详情页显示原始 diff 头部**：`git diff` 退出码 1（有差异）不再误判为失败。
- **`PatchParser.Parse` 返回 null 导致 NRE**：原生 tokenizer 失败时返回 `Failure` 而非 null。

## v1.3.1

### 新特性

- **git mm 版本检测**：仅当用户打开 git mm 仓库时检测 git-mm 是否存在及版本是否满足 3.0，偏好设置新增 git-mm 实例选择下拉框。

## v1.3.0

### Git 命令健壮性

- 修复 `Quotify()` 未转义参数内嵌引号的问题。
- 修复 `GetChangedFilesGitCommand` 解析 Copied/Renamed 状态时越界访问崩溃。
- `CommitGitCommand` 写入提交信息显式使用 UTF-8 无 BOM 编码。
- 分支名、远程名、refspec 统一通过 `Quotify()` 包裹。

### 修复

- `Connection.cs` 修复 socket 与内存泄漏。
- 12 处 `async void` 事件处理器补充 try/catch。
- `FileHelper.OpenInWindowsExplorer` 改用 `Process.Start(ProcessStartInfo)`。

### 性能优化

- `GitMmUserControl.RefreshSubrepoRuntimeState` subrepo 状态查询从串行改为最多 4 路并发。
- `RevisionFileTreeUserControl.Refresh` 和 `RevisionChangesUserControl.UpdateDiff` 异步化。

### 国际化

- 修复 9 处 `ErrorWindow` 字符串拼接，改为 `FormatCurrent` 模板化翻译。
- 新增 11 个翻译 key，补全 7 种语言。
