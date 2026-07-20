using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ForkPlus.Avalonia.Services;
using ForkPlus.Avalonia.Views;
using ForkPlus.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ForkPlus.Avalonia
{
    // Phase 1：Avalonia Application 实现。
    // 与 WPF 主工程 src/ForkPlus/App.xaml.cs 的 Application 实现并存互不冲突——
    // 两者在不同的 assembly 中（namespace 不同），由各自 exe 启动加载。
    //
    // 职责：
    //   1. 加载 XAML 资源（App.axaml，含 FluentTheme）
    //   2. 在 OnFrameworkInitializationCompleted 中构建 DI 容器
    //   3. 显示启动窗口（Phase 1 = AboutWindow，Phase 3 起替换为 MainWindow）
    //
    // Avalonia 11：axaml 编译时由 source generator 生成 InitializeComponent() 方法（partial class），
    // 无需手写 AvaloniaXamlLoader.Load(this)（该方法在 11.x 已过时）。
    public partial class App : Application
    {
        private IHost _host;

        public override void Initialize()
        {
            // InitializeComponent 由 App.axaml 编译生成的 partial class 提供
            InitializeComponent();
        }

        public override void OnFrameworkInitializationCompleted()
        {
            // 构建 DI 容器：注册 Core 抽象接口的实现（Phase 1 仅占位，Phase 3-6 逐步填充）
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    ServiceCollectionExtensions.ConfigureServices(services);
                })
                .Build();

            // 启动 Host（后台服务生命周期管理；Phase 1 暂无后台服务）
            _host.Start();

            // Phase 2.1：应用默认主题（Light）。
            // Phase 3 起会从 IUserSettings 读取用户上次选择的主题。
            var themeService = _host.Services.GetRequiredService<IThemeService>();
            themeService.ApplyTheme(ForkPlus.UI.ThemeType.Light);

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Phase 1：启动 AboutWindow 作为占位（端到端验证 Avalonia 骨架可启动）
                // Phase 3 起替换为 MainWindow（迁移自 WPF 工程的 src/ForkPlus/UI/MainWindow.xaml）
                var aboutWindow = _host.Services.GetRequiredService<AboutWindow>();
                desktop.MainWindow = aboutWindow;
            }

            base.OnFrameworkInitializationCompleted();
        }

        protected override void OnExit(ApplicationShutdownEventArgs e)
        {
            // 优雅关闭 Host，确保后台服务（如 GitOperationQueue）正确释放资源
            _host?.StopAsync(TimeSpan.FromSeconds(5));
            _host?.Dispose();
            base.OnExit(e);
        }
    }
}

