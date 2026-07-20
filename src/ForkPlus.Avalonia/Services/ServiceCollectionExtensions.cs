using System;
using ForkPlus.Avalonia.Views;
using Microsoft.Extensions.DependencyInjection;

namespace ForkPlus.Avalonia.Services
{
    // Phase 1：DI 容器注册入口。
    // App.OnFrameworkInitializationCompleted 调用 ConfigureServices(IServiceCollection)
    // 把所有 Core 抽象接口的实现注册到容器，并解析 AboutWindow 作为启动窗口。
    //
    // Phase 1 注册项（占位）：
    //   - AboutWindow：通过 ActivatorUtilities 构造（暂无依赖注入的服务）
    //
    // 后续 Phase 会逐步注册：
    //   - Phase 3：IGitEnvironment / IUserSettings / IAppContext / ILocalizationService
    //     （这些接口已在 Phase 0.2 抽象并在 WPF 工程实现，Avalonia 工程将提供 Avalonia 平台实现）
    //   - Phase 5：IMarkdownRenderer（替换 WebView2 渲染）
    //   - Phase 6：IDialogService / INotificationService / IClipboardService / IProcessLauncher
    //     （各平台实现，Windows 走系统 API，macOS/Linux 走跨平台库）
    internal static class ServiceCollectionExtensions
    {
        public static void ConfigureServices(IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            // Views（Phase 1 仅 AboutWindow；Phase 3 起逐步加 MainWindow / RepositoryUserControl 等）
            services.AddTransient<AboutWindow>();
        }
    }
}
