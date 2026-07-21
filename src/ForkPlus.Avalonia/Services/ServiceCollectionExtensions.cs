using System;
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
    //   - Phase 6.4+：IUserSettings / IProcessLauncher
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

