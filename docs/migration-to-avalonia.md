# ForkPlus WPF → Avalonia UI 渐进式迁移计划

> 起始版本：v3.5.0（已完成 .NET 10 迁移）
> 目标版本：v4.0.0（跨平台 Windows / macOS / Linux）
> 创建时间：2026-07-20

## 一、现状基线

| 维度 | 数量 | 跨平台阻塞点 |
|---|---|---|
| TFM | `net10.0-windows10.0.19041.0` | 整个工程绑定 Windows |
| XAML 文件 | 243 | namespace + 控件名 + Triggers 全部不兼容 |
| C# 源文件 | 1332 | 大量 `System.Windows.*` API |
| 主题 XAML | 69 | ControlTemplate 语法差异 |
| 自定义 WPF 控件 | 173 | `OnRender` / `DependencyProperty` API 不同 |
| DependencyProperty | 57 | 注册 API 不同 |
| 用 Triggers 的 XAML | 47 | Avalonia 无 Triggers 概念 |
| Windows-only 依赖 | 5 个 | AvalonEdit / WebView2 / WinAPICodePack / WinUI.Notifications / OxyPlot.Wpf |
| 已跨平台依赖 | 4 个 | Newtonsoft.Json / NLog / NuGet.Versioning / biturbo native |
| 总代码量 | 175,181 行 | |

## 二、目标终态

- TFM：`net10.0`（无 `-windows` 后缀）
- UI 框架：Avalonia 11+
- 平台：Windows / macOS / Linux 三平台原生运行
- CI：三平台均跑完整构建 + 单元测试 + Release zip
- 所有 Windows-only 依赖替换为跨平台等价物

## 三、核心原则

1. **Strangler Fig（绞杀者）模式**：WPF 老工程与新 Avalonia 工程并存，逐窗口迁移，不做 big bang 重写
2. **业务逻辑先剥离**：UI 与 Git/业务层解耦，确保迁移期间业务逻辑只写一次
3. **每阶段可独立发布**：任何阶段失败都能回滚到上一阶段 tag
4. **CI 持续验证**：每个阶段都要 CI 全绿才能合入
5. **并存期冻结 WPF 新功能**：Phase 3-6 期间 WPF 工程只接收 bug fix，新功能直接写在 Avalonia 工程

## 四、分阶段路线图

### Phase 0：抽象层剥离（不引入 Avalonia）

**目标**：把 UI 与业务逻辑解耦，让未来迁移 Avalonia 时业务层零改动

| 步骤 | 内容 |
|---|---|
| 0.1 | 新建 `src/ForkPlus.Core` 类库工程（TFM = `net10.0`，无 Windows 后缀） |
| 0.2 | 把 `src/ForkPlus` 下纯业务逻辑命名空间移入 Core：`Git/`、`Biturbo/`、`Settings/`、`IO/`、`NetworkHelper.cs`、`Tools/`、`AI/`（如有）等 |
| 0.3 | 抽 `IMarkdownRenderer` 接口，封装 4 个 WebView2 窗口的 Markdown→UI 渲染逻辑 |
| 0.4 | 抽 `IDialogService` 接口，封装所有 `CommonOpenFileDialog` / `FolderBrowserDialog` 调用点 |
| 0.5 | 抽 `INotificationService` 接口，封装 Toast 通知 |
| 0.6 | 抽 `IClipboardService` / `IProcessLauncher` 等系统调用接口 |
| 0.7 | ForkPlus.csproj 改为引用 ForkPlus.Core，WPF 工程只保留 UI 代码 |

**验收**：ForkPlus.Core 零 UI 依赖能独立编译；ForkPlus.csproj 编译通过；单元测试全绿

**关键风险**：循环依赖。Core 不能引用任何 `System.Windows.*`，所有 UI 依赖通过接口反转

---

### Phase 1：建立 Avalonia 骨架工程

**目标**：在解决方案里新增 `src/ForkPlus.Avalonia` 工程，与 WPF 工程并存，端到端跑通一个最小窗口

| 步骤 | 内容 |
|---|---|
| 1.1 | 新建 `src/ForkPlus.Avalonia` 工程，TFM = `net10.0` |
| 1.2 | 引用 `ForkPlus.Core` |
| 1.3 | 引入 `Avalonia` + `Avalonia.Markup` + `Avalonia.Desktop` + `Avalonia.Themes.Fluent` + `Avalonia.Diagnostics` |
| 1.4 | 实现 `App.axaml` + `Program.cs`（Avalonia Application 启动入口，与 WPF `App.xaml` 并存互不冲突） |
| 1.5 | 实现依赖注入容器（Microsoft.Extensions.DependencyInjection），把 `IMarkdownRenderer` / `IDialogService` / `INotificationService` 等接口注入 |
| 1.6 | 写一个最简单的 `AboutWindow.axaml` 端到端跑起来（显示版本号 + License 链接） |
| 1.7 | 解决方案配置：ForkPlus.Avalonia 输出 exe，ForkPlus（WPF）保持 exe 但不作为 CI 默认构建目标 |

**验收**：`dotnet run --project src/ForkPlus.Avalonia` 能弹出 About 窗口；CI 加入 Avalonia 工程构建

**关键风险**：biturbo native 在非 Windows 上的 P/Invoke 路径解析。Phase 1 就要在 Windows 上验证，Phase 7 在三平台验证

---

### Phase 2：基础控件与主题迁移

**目标**：把 69 个主题 XAML、共用样式、自定义控件库迁到 Avalonia。这是工作量最大的阶段

| 步骤 | 内容 |
|---|---|
| 2.1 | 主题策略决策：先用 `Avalonia.Themes.Fluent` 兜底，再逐步迁移 12 套自定义主题（Light/Dark/Solarized/GitHub/Dracula/Monokai/纯色 7 色） |
| 2.2 | 把 47 处 `<Style.Triggers>` / `<DataTrigger>` / `<MultiTrigger>` 改写为 Avalonia `Selector` 语法（CSS 风格，如 `Button:pointerover`） |
| 2.3 | 把 57 个 `DependencyProperty.Register` 改为 `StyledProperty.Register` 或 `DirectProperty.Register` |
| 2.4 | 迁移 173 个自定义控件的 `OnRender(DrawingContext)` → `Render(DrawingContext)`，注意 `DrawingContext` API 差异 |
| 2.5 | 迁移 `RevisionGraphUserControl`（commit graph 渲染，最难，可能要重写为 SkiaSharp 直接绘制） |
| 2.6 | 替换 AvalonEdit 6.3 → AvaloniaEdit 11.x（API 90% 相似，破坏点：`TextArea` 事件、`Selection` 类型） |
| 2.7 | 替换 OxyPlot.Wpf 2.2 → OxyPlot.Avalonia 2.x（`PlotView` API 差异） |
| 2.8 | 替换 CommunityToolkit.WinUI.Notifications → 跨平台通知实现（`INotificationService` 各平台实现） |
| 2.9 | 迁移 HexEditor / HexDiffUserControl（基于 AvalonEdit 的自定义控件，依赖 2.6） |

**验收**：ForkPlus.Avalonia 能渲染所有主题样式；基础控件正确显示；单元测试全绿

**关键风险**：
- 69 个主题 XAML 的工作量巨大，建议拆为子阶段（先 Light/Dark，再 Solarized/GitHub，再 Dracula/Monokai，最后纯色 7 套）
- RevisionGraph 渲染是技术难点，可能需要单独 spike 验证

---

### Phase 3：核心窗口迁移

**目标**：迁移应用主入口和仓库主视图，让 Avalonia 版本能打开仓库做基础操作

| 步骤 | 内容 |
|---|---|
| 3.1 | `MainWindow.xaml` 迁移（菜单、工具栏、tab 容器） |
| 3.2 | `ToolbarUserControl` 迁移 |
| 3.3 | `SidebarUserControl` 迁移 |
| 3.4 | `RepositoryUserControl` 迁移（最大最复杂的 UserControl） |
| 3.5 | `RevisionListViewUserControl` 迁移（依赖 2.5 的 RevisionGraph） |
| 3.6 | `RevisionDetailsUserControl` / `RevisionFileTreeUserControl` 迁移 |
| 3.7 | `FileListUserControl` / `FileControlHeaderUserControl` 迁移 |
| 3.8 | `CommitUserControl` 迁移 |
| 3.9 | Diff 渲染相关 UserControl 迁移（依赖 AvaloniaEdit） |
| 3.10 | `StatusUserControl` / `RevisionSummaryUserControl` 迁移 |

**验收**：能打开仓库、看 commit graph、看 diff、commit 文件；CI 全绿

**关键风险**：RepositoryUserControl 是核心，拆解不当会卡住整个阶段。建议先 spike 一个最简版（只显示 commit list），再逐步补功能

---

### Phase 4：对话框批量迁移

**目标**：迁移约 100 个 Dialog 窗口。按功能分组，每组独立 PR

| 分组 | 涉及对话框 |
|---|---|
| 4.1 Branch 操作 | CreateBranch / CheckoutBranch / RemoveLocalBranch / RenameLocalBranch / RebaseBranch / MergeBranch / ResetBranch / CherryPick / RevertRevision |
| 4.2 Tag 操作 | CreateTag / RemoveTag / PushTag / PushMultipleTags / TagDetails |
| 4.3 Stash 操作 | SaveStash / ApplyStash / RemoveStash / RenameStash / CreatePartialStash |
| 4.4 Submodule / Worktree | AddSubmodule / DeleteSubmodule / CreateWorktree / DeleteWorktree / CheckoutBranchAsWorktree |
| 4.5 Remote / Fetch / Pull / Push | EditRemote / Fetch / Pull / Push / PushMultipleBranches / Clone / TrackRemoteBranch / RemoveRemoteBranch / ChangeRemoteTracking |
| 4.6 Git Flow | GitFlowInit / StartFeature / FinishFeature / StartRelease / FinishRelease / StartHotfix / FinishHotfix |
| 4.7 Git Lfs | GitLfsPull / GitLfsFetch / GitLfsTrack / GitLfsStatus |
| 4.8 git mm | InitGitMmRepository / GitMmStart / GitMmSync / GitMmUpload |
| 4.9 Lean Branching | LeanBranchingStart / LeanBranchingFinish |
| 4.10 Settings | PreferencesWindow / RepositorySettingsWindow / ConfigureWorkspaces / ConfigureGitInstance / ConfigureSshKeys |
| 4.11 Account | AccountsWindow / GitHubLogin / GitHubEnterpriseLogin / GitLabLogin / GiteaLogin / BitbucketLogin / BitbucketServerLogin / OpenAiLogin / AddAccount |
| 4.12 其他 | About / Welcome / Error / MessageBox / UpdateCheck / UpdateAvailable / Benchmark / PerformanceDiagnostics / Legal / FileHistory / Blame / RepositoryStatistics / RepositoryOverview / CustomActionResult / CustomCommandUI / InteractiveRebase / SaveSnapshot / SaveAsPatch / ApplyPatch / SideBySideMerge / DiffPopup / GoToLine / LongOperation / Reflog / SshPassphrase / GenerateNewSshKey / AddGitIgnorePattern / AddGitignoreTemplate / OpenRepositoryAlert / ForkSyncCheck / RunSharedCustomCommandConfirmation / CustomColorsDialog / CheckoutRevision / CheckoutAndSync / RescanRepositories |

**验收**：所有对话框在 Avalonia 版本能打开并完成基本功能；CI 全绿

**关键风险**：单个 PR 不要太大，建议每分组一个 PR，便于 review 和回滚

---

### Phase 5：AI 窗口迁移（WebView2 替换）

**目标**：迁移 4 个 WebView2 窗口，彻底移除 `Microsoft.Web.WebView2` 依赖

| 步骤 | 内容 |
|---|---|
| 5.1 | `GitMmReferenceWindow` 迁移（最简单，静态 md 渲染）→ 用 `Markdown.Avalonia` 或 `Avalonia.WebView` |
| 5.2 | `AiTextResultWindow` 迁移（流式 chunk 渲染 + 滚动追踪） |
| 5.3 | `AiDevelopmentWindow` 迁移（多个动态 WebView 实例 + 高度自适应） |
| 5.4 | `AiCodeReviewWindow` 迁移（最难，含 C#↔JS 双向通信，suggestion 按钮要重新设计为 Avalonia 控件） |
| 5.5 | 删除 `Microsoft.Web.WebView2` PackageReference |
| 5.6 | 删除 `CopyWebView2LoaderToRoot` MSBuild Target |
| 5.7 | 删除 `WebView2EnvironmentHelper` |

**验收**：4 个 AI 窗口在 Avalonia 下功能完整；WebView2 包彻底移除；CI 全绿

**技术选型建议**：
- 简单窗口（GitMmReference）→ `Markdown.Avalonia`（原生控件渲染，跨平台）
- 流式窗口（AiTextResult / AiDevelopment）→ `Markdown.Avalonia` + 节流渲染
- 复杂窗口（AiCodeReview）→ `Avalonia.WebView`（CEF 内核，保留 JS 通信）或重构 suggestion UI 为纯 Avalonia 控件

---

### Phase 6：系统功能与测试迁移

**目标**：替换所有剩余 Windows-only 系统依赖

| 步骤 | 内容 |
|---|---|
| 6.1 | 替换 `Microsoft-WindowsAPICodePack-Shell` → `Avalonia.CommonDialog`（或各平台原生对话框） |
| 6.2 | 替换系统通知（`INotificationService` 各平台实现：Windows 用 `Windows.UI.Notifications`、macOS 用 `NSUserNotificationCenter`、Linux 用 `libnotify`） |
| 6.3 | 替换"在文件管理器中打开"等 Shell 集成（各平台条件编译） |
| 6.4 | 替换系统托盘（如有）→ `Avalonia.Controls.TrayIcon` |
| 6.5 | FlaUI 系统测试 → Avalonia UI 自动化（`Avalonia.Headless` + 平台特定 E2E） |
| 6.6 | 单元测试工程迁移到 `net10.0`（移除 `-windows` TFM） |

**验收**：所有 Windows-only 依赖全部移除；`ForkPlus.csproj`（WPF）依赖图零 Windows-only 包；CI 全绿

---

### Phase 7：切换启动入口 + 跨平台 CI

**目标**：Avalonia 版本成为默认，WPF 工程退出历史舞台

| 步骤 | 内容 |
|---|---|
| 7.1 | `ForkPlus.Avalonia` 设为解决方案默认启动工程 |
| 7.2 | `src/ForkPlus`（WPF 工程）标记 deprecated，归档到 `archive/` 目录或删除 |
| 7.3 | TFM 统一为 `net10.0`（无 `-windows`） |
| 7.4 | CI matrix 在 `windows-latest` / `ubuntu-latest` / `macos-latest` 三平台均跑完整构建 + 单元测试 |
| 7.5 | Release 工作流发布三平台 zip（Windows zip / macOS zip / Linux zip） |
| 7.6 | README 8 种语言文档刷新跨平台说明 |
| 7.7 | RELEASE_NOTE.md 新增 v4.0.0 跨平台版本说明 |
| 7.8 | 打 `v4.0.0` tag |

**验收**：三平台能运行 ForkPlus；CI 三平台全绿；Release 三平台 zip

---

## 五、阶段依赖关系图

```
Phase 0 (抽象层)
   ↓
Phase 1 (Avalonia 骨架)
   ↓
Phase 2 (主题+控件库)  ←── 工作量最大
   ↓
Phase 3 (核心窗口)     ←── 技术难点最多
   ↓
Phase 4 (对话框批量)   ←── 可拆子 PR 并行
   ↓
Phase 5 (AI 窗口)
   ↓
Phase 6 (系统功能+测试)
   ↓
Phase 7 (切换入口+跨平台 CI)
```

## 六、关键风险与对策

| 风险 | 影响 | 对策 |
|---|---|---|
| **RevisionGraph 自定义渲染** | 卡住 Phase 3 | Phase 2 先做技术 spike，验证 Avalonia `Render` + SkiaSharp 可行性 |
| **47 处 Triggers 重写** | 工作量巨大 | 优先用 Avalonia 内置伪类（`:pointerover` / `:pressed` / `:selected`），少数复杂逻辑用 code-behind |
| **69 个主题 XAML** | 工作量巨大 | 先 Fluent 兜底，12 套主题分批迁移，每套独立 PR |
| **biturbo native 在 macOS/Linux** | 跨平台运行失败 | Phase 1 在 Windows 验证 P/Invoke，Phase 7 前在 macOS/Linux 验证 |
| **并存期双 UI 维护成本** | Phase 3-6 期间开发效率下降 | 冻结 WPF 新功能，只接收 bug fix；Avalonia 版本未就绪前 WPF 仍是发布版本 |
| **FlaUI 系统测试跨平台** | E2E 测试失效 | 降级为各平台 `Avalonia.Headless` 单元测试 + 关键路径手动测试 |
| **AvaloniaEdit 性能** | 大文件 diff 渲染卡顿 | Phase 2 spike 10MB 文件 diff 性能 baseline |
| **AI 窗口 JS 通信** | AiCodeReview 重设计成本高 | 保留 `Avalonia.WebView` 方案作为兜底，避免重设计 suggestion 按钮 |

## 七、版本与发布策略

| 版本 | 阶段 | 说明 |
|---|---|---|
| v3.5.x | Phase 0-1 | WPF 仍是发布版本，Avalonia 工程内部构建验证 |
| v3.6.x | Phase 2-3 | WPF 仍是发布版本，Avalonia 版本可内部 demo |
| v3.7.x | Phase 4-5 | WPF 仍是发布版本，Avalonia 版本功能基本完整 |
| v3.8.x | Phase 6-7 | 双版本并存发布，Avalonia 标记为 beta |
| **v4.0.0** | Phase 7 完成 | Avalonia 版本成为唯一发布版本，三平台跨平台 |

## 八、每阶段验证清单

每个阶段必须满足才能合入 master：

- [ ] 单元测试通过率不下降（不低于上一阶段 baseline）
- [ ] CI 三平台全绿（Phase 7 前 Windows 全跑，非 Windows 跑冒烟）
- [ ] 手动 smoke test 关键路径（打开仓库 / commit / push / pull / diff / AI 审查）
- [ ] 性能 baseline 对比（启动时间 / 大仓库刷新时间 / diff 渲染时间）
- [ ] 该阶段独立 tag，可回滚
- [ ] RELEASE_NOTE.md 更新

## 九、Phase 0 启动检查清单

Phase 0 是纯重构，不引入任何 Avalonia 依赖，风险最低：

- [ ] 0.1 新建 `src/ForkPlus.Core` 类库工程（TFM = `net10.0`，无 Windows 后缀）
- [ ] 0.2 把纯业务逻辑命名空间移入 Core
- [ ] 0.3 抽 `IMarkdownRenderer` 接口
- [ ] 0.4 抽 `IDialogService` 接口
- [ ] 0.5 抽 `INotificationService` 接口
- [ ] 0.6 抽 `IClipboardService` / `IProcessLauncher` 等系统调用接口
- [ ] 0.7 ForkPlus.csproj 改为引用 ForkPlus.Core
- [ ] 0.8 本地构建 0 Warning
- [ ] 0.9 CI 三平台全绿
