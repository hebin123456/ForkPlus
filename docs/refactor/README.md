# ForkPlus WPF → Avalonia 重构总览

> 分支：`master-refactor`
> 原则：**先抽象、后迁移**。分 6 阶段渐进式推进，每阶段保证构建通过、功能不退化。

## 阶段索引

| 阶段 | 状态 | 文档 | 目标 |
|------|------|------|------|
| 0 | 已完成 | [phase0-abstraction.md](phase0-abstraction.md) | 补全跨平台抽象层（接口 + WPF 实现 + 注册到 ServiceLocator） |
| 1 | 已完成 | [phase1-domain-validation.md](phase1-domain-validation.md) | 验证领域层（Git/Biturbo/Accounts/Jobs/Settings/Utils）零 WPF 依赖 |
| 2 | 已完成 | [phase2-commands-dewpf.md](phase2-commands-dewpf.md) | Commands 层去 WPF 化（MessageBox/Process/OpenDialog/Tab管理/应用操作 + 本地化） |
| 3 | 待开始 | [phase3-viewmodel-extraction.md](phase3-viewmodel-extraction.md) | 抽取 ViewModel 层（最大工作量，重构分水岭） |
| 4 | 待开始 | [phase4-avalonia-migration.md](phase4-avalonia-migration.md) | Avalonia UI 重写（换基类 + 换 XAML + 换第三方库） |
| 5 | 待开始 | [phase5-platform-crossplatform.md](phase5-platform-crossplatform.md) | 平台特定功能跨平台化（Win32 P/Invoke / IPC / 主题） |
| 6 | 待开始 | [phase6-remove-wpf.md](phase6-remove-wpf.md) | 移除 WPF 框架依赖，切换 TFM 到 net10.0 |

## 核心判断标准

**"VM 干净 + 抽象层就位 + 控件替换 + 平台 API 跨平台化"四者缺一不可。**

判断 VM 是否干净：随便挑一个 ViewModel，把 `using System.Windows.*` 全删掉能不能编译通过？能，就水到渠成；不能，就还没到位。

## 架构现状诊断

项目**不是 MVVM 架构**，本质是 View-first + 事件驱动 + x:Name 直访：

- **无 MVVM 框架**：`.csproj` 无 CommunityToolkit.Mvvm / Prism / MVVMLight
- **无 ViewModel 目录**：`*ViewModel.cs` 是 ListView 数据 DTO，不是承载视图状态的 VM
- **x:Name : Binding ≈ 2.8 : 1**（176 vs 63），靠命名控件 + 事件直访为主
- **业务逻辑大量塞在 .xaml.cs 里**：IPC、注册表、Win32 P/Invoke、AI 对话状态、第三方控件全在 View 里

**好消息**：`Services/` 已有 7 个跨平台接口 + WPF 实现（IDispatcher / IClipboardService 等），是最值钱的资产；领域层（Git/Biturbo/Accounts/Jobs/Settings）基本不依赖 WPF。

## 重灾区文件清单

| 文件 | 主要问题 |
|------|----------|
| `src/ForkPlus/App.xaml.cs` | IPC、注册表、Win32 P/Invoke、MessageBox、主题字典全在 Application 类 |
| `src/ForkPlus/UI/MainWindow.xaml.cs` | Manager/队列直接持有、OnDrop/OnKeyDown 业务、设置持久化、WindowChrome |
| `src/ForkPlus/UI/CustomWindow.cs` | WPF Window 基类、DependencyProperty、WindowChrome、HwndSource |
| `src/ForkPlus/UI/Dialogs/ForkPlusDialogWindow.cs` | 所有 Dialog 基类、pack URI、dialog chrome |
| `src/ForkPlus/UI/UserControls/RepositoryUserControl.xaml.cs` | 持有 GitModule/RepositoryData/JobQueue/UndoRedoStack |
| `src/ForkPlus/UI/UserControls/CommitUserControl.xaml.cs` | Dispatcher.CheckAccess、x:Name 直访 |
| `src/ForkPlus/UI/Dialogs/AiDevelopmentWindow.xaml.cs` | WebView2 + AI 对话历史 + 流式状态 + 撤销快照 |
| `src/ForkPlus/UI/UserControls/StatisticsUserControl.xaml.cs` | OxyPlot.Wpf、nested ViewModel |
| `src/ForkPlus/UI/Dialogs/CloneWindow.xaml.cs` | x:Name 直访、git 命令预览生成 |
| `src/ForkPlus/UI/Commands/OpenFileInDefaultEditorCommand.cs` | Win32 AssocQueryString P/Invoke |
| `src/ForkPlus/UI/Commands/CheckForkSyncCommand.cs` | 4 处 MessageBox.Show |
| `src/ForkPlus/UI/Dialogs/CustomColorsDialog.xaml.cs` | 12 处 MessageBox.Show（最多） |
| `src/ForkPlus/UI/Helpers/WindowLocationStateExtensions.cs` | Win32 SetWindowPlacement |
| `src/ForkPlus/WindowsCredentialManager.cs` | Windows 凭据管理器 |
