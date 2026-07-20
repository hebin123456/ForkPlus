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
        }
    }
}

