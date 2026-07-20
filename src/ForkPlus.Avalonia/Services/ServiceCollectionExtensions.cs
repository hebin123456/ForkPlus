using System;
using ForkPlus.Avalonia.Views;
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
    //   - Phase 3：IGitEnvironment / IUserSettings / IAppContext / ILocalizationService
    //   - Phase 5：IMarkdownRenderer（替换 WebView2 渲染）
    //   - Phase 6：IDialogService / INotificationService / IClipboardService / IProcessLauncher
    internal static class ServiceCollectionExtensions
    {
        public static void ConfigureServices(IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            // Phase 2.1：主题服务（Fluent 兜底）
            services.AddSingleton<IThemeService, AvaloniaThemeService>();

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
        }
    }
}

