using System;
using ForkPlus.Accounts;
using ForkPlus.Avalonia.Views;
using ForkPlus.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ForkPlus.Avalonia.Services
{
    // Phase 1/2.1：DI 容器注册入口。
    // App.OnFrameworkInitializationCompleted 调用 ConfigureServices(IServiceCollection)
    // 把所有 Core 抽象接口的实现注册到容器，并解析 AboutWindow 作为启动窗口。
    //
    // Phase 1 注册项：
    //   - AboutWindow：启动窗口（占位）
    //
    // Phase 2.1 注册项：
    //   - IThemeService → AvaloniaThemeService（Fluent 兜底，22 套主题变体迁移在 Phase 2.2-2.4）
    //
    // 后续 Phase 会逐步注册：
    //   - Phase 3：IGitEnvironment / ILocalizationService
    //   - Phase 5：IMarkdownRenderer（替换 WebView2 渲染）
    //   - Phase 6.1（已完成）：IClipboardService / IDispatcher / IAppContext / IDesignModeService
    //   - Phase 6.2（已完成）：IToastNotificationService
    //   - Phase 6.3（已完成）：IDialogService
    //   - Phase 6.4a（已完成）：IUserSettings（spike stub，所有属性返回默认值；Phase 0.4 + 6.4b 升级为真实持久化实现）
    //   - Phase 6.5（已完成）：IWindowManagerService
    //   - Phase 6.6a（已完成）：IGitEnvironment（spike stub，which/where 查找 git 路径）
    //   - Phase 6.7（已完成）：ITimerService（Avalonia DispatcherTimer）
    //   - Phase 6.8（已完成）：ILocalizationService（复用 Core 的 LocalizationService）
    //   - Phase 6.9（已完成）：IAccountManager（复用 Core 的 AccountManager.Current 静态单例）
    internal static class ServiceCollectionExtensions
    {
        public static void ConfigureServices(IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            // Phase 2.1：主题服务（Fluent 兜底）
            services.AddSingleton<IThemeService, AvaloniaThemeService>();

            // Phase 6.1：4 个简单 Core 接口的 Avalonia 实现
            // 对照 WPF 工程 src/ForkPlus/Services/Wpf/Wpf*.cs（9 个 Wpf 实现中的 4 个简单项）。
            // 复杂项（IDialogService / IUserSettings）留待后续 Phase。
            services.AddSingleton<IClipboardService, AvaloniaClipboardService>();
            services.AddSingleton<IDispatcher, AvaloniaDispatcher>();
            services.AddSingleton<IAppContext, AvaloniaAppContext>();
            services.AddSingleton<IDesignModeService, AvaloniaDesignModeService>();

            // Phase 6.2：IToastNotificationService 的 Avalonia 实现
            // 对照 WPF 工程 src/ForkPlus/Services/Wpf/WpfToastNotificationService.cs（WinRT Toast，Windows-only）。
            // 用 Avalonia.Controls.Notifications.WindowNotificationManager（三平台统一，无 OSPlatform 分支），
            // 首次 Show() 时懒加载注入主窗口可视树（不改 MainWindow.axaml）。
            services.AddSingleton<IToastNotificationService, AvaloniaToastNotificationService>();

            // Phase 6.3：IDialogService 的 Avalonia 实现
            // 对照 WPF 工程 src/ForkPlus/UI/OpenDialog.cs（用 Microsoft-WindowsAPICodePack-Shell，Windows-only）：
            //   - CommonOpenFileDialog (IsFolderPicker=true/false) → TopLevel.StorageProvider.OpenFilePickerAsync / OpenFolderPickerAsync
            //   - CommonSaveFileDialog → （接口暂未抽 ShowSaveFileDialog，留待后续 Phase 补充）
            //   - WPF MessageBox.Show（49 处调用）→ 构造简单 Window + ShowDialog(owner) 模态显示
            //   - ForkPlus.UI.Dialogs.ErrorWindow → ShowError 同样走 ShowDialog 模态窗口
            // 用 Avalonia 11 StorageProvider API（三平台统一，不引入任何 Windows-only 包）。
            services.AddSingleton<IDialogService, AvaloniaDialogService>();

            // Phase 6.4b：IUserSettings 的 Avalonia 真实实现
            // 对照 WPF 工程 src/ForkPlus/Services/Wpf/WpfUserSettings.cs（委托到 ForkPlusSettings.Default）。
            // Phase 0.4 已把 ForkPlusSettings 从 WPF 迁入 Core，所有 17 个属性委托到
            // ForkPlusSettings.Default.Xxx（读 settings.json，写入持久化）。
            services.AddSingleton<IUserSettings, AvaloniaUserSettings>();

            // Phase 6.5：IWindowManagerService 的 Avalonia 实现
            // 对照 WPF 工程 src/ForkPlus/Services/Wpf/WpfWindowManagerService.cs（44 行）：
            //   - ActivateAndShowNotifications → 取 Avalonia 主窗口调用 Activate() + 最小化恢复
            //     （Avalonia 工程暂未集成 NotificationManager 面板，spike 阶段只激活窗口）
            //   - TryActivateWindowByTitle → 遍历 IClassicDesktopStyleApplicationLifetime.Windows
            //   - DispatchToUiThread → Avalonia.Threading.Dispatcher.UIThread.Post
            // 调用方：ForkPlus.Core.Accounts.NotificationManager（仅 2 处，Toast 点击回调）。
            services.AddSingleton<IWindowManagerService, AvaloniaWindowManagerService>();

            // Phase 6.6b：IGitEnvironment 的 Avalonia 真实实现
            // 对照 WPF 工程 src/ForkPlus/Services/Wpf/WpfGitEnvironment.cs（34 行，纯转发壳）。
            // Phase 0.4 已把 ForkPlusSettings 迁入 Core，git 路径解析升级为：
            //   - GitPath：环境变量 forkgitinstance → ForkPlusSettings.Default.GitInstancePath → ForkGitInstancePath
            //   - ShellPath / BashPath：从 GitPath 派生（Windows）/ 返回 "sh"/"bash"（Unix）
            //   - OverrideCredentialHelper / OverrideCredentialHelperBt：null（依赖 AccountManager，待 Phase 6.7+ 接入）
            //   - ForkGitInstancePath：Windows 用 ForkDirectoryPath/gitInstance/2.50.1/bin/git.exe，Unix null
            //   - AppName："ForkPlus" / CliArguments：Environment.GetCommandLineArgs()
            // Core 工程中 95 处引用 ServiceLocator.GitEnvironment.Xxx（遍布 Git/Shell/Jobs/UI/Accounts）。
            services.AddSingleton<IGitEnvironment, AvaloniaGitEnvironment>();

            // Phase 6.7：ITimerService 的 Avalonia 实现
            // 对照 WPF 工程 src/ForkPlus/Services/Wpf/WpfTimerService.cs（封装 System.Windows.Threading.DispatcherTimer）。
            // Avalonia.Threading.DispatcherTimer API 与 WPF 几乎对称（Interval / IsEnabled / Tick / Start / Stop）。
            // 注册为 Transient：调用方 NotificationManager 每次构造新实例时都创建新的 DispatcherTimer，
            // 避免多个 NotificationManager 共用一个 Timer 互相干扰。
            // 调用方：ForkPlus.Core.Accounts.NotificationManager（仅 1 处，通知轮询定时器）。
            services.AddTransient<ITimerService, AvaloniaTimerService>();

            // Phase 6.8：ILocalizationService 直接复用 Core 的 LocalizationService（平台无关）
            // 对照 WPF 工程 src/ForkPlus/App.xaml.cs:613 注入 LocalizationService(appContext, () => ForkPlusSettings.Default.UiLanguage)。
            // LocalizationService 已在 Core 工程中（src/ForkPlus.Core/Services/LocalizationService.cs），
            // 完全平台无关，依赖 IAppContext（取 ForkDataDirectoryPath 加载用户语言文件）+ Func<string>（当前语言 provider）。
            // Phase 0.4 已把 ForkPlusSettings 迁入 Core，语言 provider 升级为 () => ForkPlusSettings.Default.UiLanguage
            // （之前 spike 阶段硬编码返回 "zh-Hans"）。
            // 调用方：Core 工程中 100+ 文件 / 433+ 处调用 ServiceLocator.Localization.Xxx
            // （OpenAiService / Accounts/* / Git/Commands/* / Utils/Http/ServiceError 等最大耦合点）。
            services.AddSingleton<ILocalizationService>(sp =>
                new LocalizationService(
                    sp.GetRequiredService<IAppContext>(),
                    () => ForkPlus.Settings.ForkPlusSettings.Default.UiLanguage));

            // Phase 6.9：IAccountManager 直接复用 Core 的 AccountManager.Current 静态单例
            // 对照 WPF 工程 src/ForkPlus/App.xaml.cs:627 注入 AccountManager.Current。
            // AccountManager 已在 Core 工程中（src/ForkPlus.Core/Accounts/AccountManager.cs），
            // 完全平台无关，已实现 IAccountManager 接口。注入时用静态单例 Current。
            // 调用方：ForkPlus.Core/Git/Commands/GetRemotesGitCommand.cs:89
            // （按 host + username 查找已配置账号用于区分同 host 不同账号、附加 credential helper）。
            services.AddSingleton<IAccountManager>(AccountManager.Current);

            // Views
            // Phase 3.1：MainWindow 作为启动窗口（spike 骨架版）
            // Phase 1 的 AboutWindow 保留（通过菜单/按钮可达）
            services.AddSingleton<MainWindow>();
            services.AddTransient<AboutWindow>();

            // Phase 3.2：ToolbarUserControl（spike 简化版）
            services.AddTransient<Views.UserControls.ToolbarUserControl>();

            // Phase 3.3：SidebarUserControl（spike 简化版）
            // Phase 3.4 RepositoryUserControl.EnsureLayoutInitialized 会注入到 Sidebar 容器
            services.AddTransient<Views.UserControls.SidebarUserControl>();

            // Phase 3.4：RepositoryUserControl（spike 简化版，最大最复杂的 UserControl）
            // Grid 3x3 布局 + Sidebar/Content 占位 + 33 个公共方法入口占位
            services.AddTransient<Views.UserControls.RepositoryUserControl>();

            // Phase 3.5：RevisionListViewUserControl（spike 简化版）
            // 装入 RepositoryUserControl.RepositoryContentContainer（spike 跳过 RepositoryContentUserControl 这层）
            // GraphCellView 自绘（Phase 2.5 难点）暂用 Border 占位
            services.AddTransient<Views.UserControls.RevisionListViewUserControl>();

            // Phase 3.6：RevisionDetailsUserControl + RevisionFileTreeUserControl（spike 简化版）
            // RevisionDetails 装入 RepositoryContentUserControl Row 2（spike 跳过这层）
            // RevisionFileTree 装入 RevisionDetails 的 FileTree tab
            services.AddTransient<Views.UserControls.RevisionDetailsUserControl>();
            services.AddTransient<Views.UserControls.RevisionFileTreeUserControl>();

            // Phase 3.7：FileListUserControl + FileControlHeaderUserControl（spike 简化版）
            // FileList 装入 RevisionChangesUserControl Row 2 / StageFileUserControl / CommitUserControl
            // FileControlHeader 作为 DiffUserControl / RevisionFileTreeUserControl 等的子控件
            services.AddTransient<Views.UserControls.FileListUserControl>();
            services.AddTransient<Views.UserControls.FileControlHeaderUserControl>();

            // Phase 3.9a：FileDiffControl + CommitFileDiffControl 容器骨架（spike 简化版）
            // FileDiffControl 是 diff 渲染主控件（WPF 是 FileDiffControl.cs 纯 C# Grid，无 XAML）
            // 5 种 sub-view 占位（Text/Binary/Hex/Submodule/Fallback），不引入 AvaloniaEdit
            // CommitFileDiffControl 继承 FileDiffControl，加 chunk stage/unstage 事件占位
            // Phase 3.8 CommitUserControl 和 Phase 3.10 RevisionSummaryUserControl 都依赖这两个控件占位
            // 真实 AvalonEdit 子树迁移留待 Phase 2.6 + 3.9b
            services.AddTransient<Views.UserControls.FileDiffControl>();
            services.AddTransient<Views.UserControls.CommitFileDiffControl>();

            // Phase 3.8：CommitUserControl（spike 简化版）
            // Grid 3 列：StageFile 占位 / GridSplitter / 嵌套 Grid 3 行
            // （CommitFileDiffControl + 分隔 + commit message 编辑区）
            // 装入 RepositoryContentUserControl（由 RepositoryViewMode.CommitViewMode 触发显示）
            services.AddTransient<Views.UserControls.CommitUserControl>();

            // Phase 3.10：StatusUserControl + RevisionSummaryUserControl（spike 简化版）
            // Status 装入 ToolbarUserControl Row 0（状态栏 + 标题动画 + Activity Manager）
            // RevisionSummary 装入 RevisionDetailsUserControl Commit tab（commit 详情 + DiffList）
            services.AddTransient<Views.UserControls.StatusUserControl>();
            services.AddTransient<Views.UserControls.RevisionSummaryUserControl>();

            // Phase 3.11：RepositoryContentUserControl（中间包装层，串联 Phase 3.4-3.10 所有 spike）
            // 装入 RepositoryUserControl.RepositoryContentContainer
            // 内含 RevisionView（RevisionListView + RevisionDetails）+ CommitView（CommitUserControl）
            // 由 RepositoryViewMode.RevisionViewMode/CommitViewMode 切换可见性
            services.AddTransient<Views.UserControls.RepositoryContentUserControl>();

            // Phase 3.4b：NotificationBarUserControl（完整迁移版）
            // 对照 WPF 工程 src/ForkPlus/UI/UserControls/NotificationBarUserControl.xaml.cs（395 行）。
            // 仓库顶部通知栏（推送/拉取/冲突/状态条），可折叠。
            // spike 简化：MainWindow.Instance → onRepositoryRefresh 回调，PNG 图标 → emoji，
            // 动画 → IsVisible 切换，Inline 列表 → NotificationViewModel POCO。
            // 装入 RepositoryUserControl Row 0（NotificationBar 区域）。
            services.AddTransient<Views.UserControls.NotificationBarUserControl>();

            // Phase 3.5b：RevisionListStatusBarUserControl（完整迁移版）
            // 对照 WPF 工程 src/ForkPlus/UI/UserControls/RevisionListStatusBarUserControl.xaml.cs（110 行）。
            // commit 列表底部的状态栏（显示总数、过滤结果数、loading 指示器）。
            // spike 简化：NotificationCenter 不可访问 → SetStatus/ShowLoading/SetCount API，
            // 保留 ToFriendlyName + InvalidateStatusBarTextBlockMeasurement 完整逻辑。
            // 装入 RevisionListViewUserControl 底部。
            services.AddTransient<Views.UserControls.RevisionListStatusBarUserControl>();

            // Phase 3.8b：StageFileUserControl（完整迁移版 — 单文件行）
            // 对照 WPF 工程 src/ForkPlus/UI/UserControls/StageFileUserControl.xaml.cs（498 行）。
            // WPF 版是双列表控件，spike 版按 task spec 简化为单文件行：
            // 复选框 + 文件名 + 状态图标 emoji（M=📝 / A=✨ / D=🗑 / R=🔀）。
            // 装入 CommitUserControl Col 0（staged/unstaged 文件列表区域）。
            services.AddTransient<Views.UserControls.StageFileUserControl>();

            // Phase 5.2a：AiTextResultWindow（spike 骨架版，通用 AI 文本结果流式显示窗口）
            // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/AiTextResultWindow.xaml.cs（483 行）。
            // 功能1（AI 解释 commit）和功能3（AI 生成 PR 描述）共用。
            // spike 版用 Markdown.Avalonia.Tight 替代 WebView2，保留公共方法签名（StartStreaming /
            // OnChunk / OnSuccess / OnError / ApplyLocalization）。RunRequest / JobMonitor /
            // OpenAiService 等完整迁移留 Phase 5.2b。
            services.AddTransient<Dialogs.AiTextResultWindow>();

            // Phase 5.3a：AiDevelopmentWindow（spike 骨架版，AI 辅助开发多轮对话窗口）
            // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/AiDevelopmentWindow.xaml.cs（1906 行）。
            // spike 版用 Markdown.Avalonia.Tight 替代 WebView2（C#↔JS 双向通信改为简单 C# 方法调用），
            // 保留构造函数签名 AiDevelopmentWindow(RepositoryUserControl, GitModule)。
            // ProcessRequest / OpenAiRequestStreamingWithRetry / JobQueue / ParseAiResponse /
            // ApplyFileChanges / ShowDiffResults / UndoAiChanges 等完整迁移留 Phase 5.3b。
            services.AddTransient<Dialogs.AiDevelopmentWindow>();

            // Phase 5.4a：AiCodeReviewWindow（spike 骨架版，3 个 AI 窗口中最复杂的一个，
            // 因为 WPF 版有真实的 C# ↔ JS 双向通信）。
            // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/AiCodeReviewWindow.xaml.cs（1758 行）。
            // spike 版完全弃用 WebView2，改用 Markdown.Avalonia.Tight 的 MarkdownScrollViewer：
            //   - WPF 版的 C#↔JS 双向通信（CoreWebView2.WebMessageReceived 处理
            //     preview-suggestion/apply-suggestion/scroll-at-bottom 三种 postMessage，
            //     C# → JS ExecuteScriptAsync window.scrollTo 自动滚动）改为简单的 C# 方法直接调用
            //     （PreviewSuggestionButton_Click → PreviewSuggestion、ApplySuggestionButton_Click → ApplySuggestion）
            //   - HTML 文档拼接（CSS + CreateStatusHtml + CreateReviewBodyHtml + CreateSuggestionsHtml +
            //     CreateAllReviewResultsHtml + previewSuggestion/applySuggestion JS 函数注入）全部跳过，
            //     markdown 文档直接用 GFM 写出，suggestion 操作改用原生 Avalonia Button + ListBox
            // 保留构造函数签名 AiCodeReviewWindow(RepositoryUserControl, AiCodeReviewTarget, AiAgent)。
            // ReviewWithAiAgent / ReviewWithOpenAi / ReviewFilesWithOpenAi / JobQueue / OpenAiService /
            // ExtractSuggestions / ApplySuggestion 文件写入 / PreviewSuggestion + AiSuggestionPreviewWindow
            // 等完整迁移留 Phase 5.4b。
            services.AddTransient<Dialogs.AiCodeReviewWindow>();
        }
    }
}

