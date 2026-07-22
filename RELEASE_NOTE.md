# Release Notes

本文件记录 ForkPlus 各版本的变更。从 v1.3.0 开始，每次发布都会在此更新。

## v3.5.2

### Bug 修复

- 修复 AI 辅助开发界面 AI 回答内容过长时溢出消息容器、撑爆整页滚动的问题（单条消息 WebView2 限高 + 内部滚动）。
- 修复未配置 AI 时点开"AI 开发"弹出的原生 MessageBox 未国际化的问题，改用 ForkPlus 自带提示框，并提供"打开偏好设置"按钮直达 AI Enhancement 配置页。
- 修复 AI 总是回复"没有目录读取权限"的问题：增强系统提示，明确告知模型它不能访问本地文件系统，需要文件内容时由用户粘贴。

### 新特性

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
