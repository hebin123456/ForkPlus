using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
    // Avalonia 11：AvaloniaNameSourceGenerator 会为继承自 Window/UserControl/Control 的
    // axaml 生成 InitializeComponent() 方法，但 Application 根元素不在生成范围内。
    // 故 App.axaml.cs 直接调用 AvaloniaXamlLoader.Load(this) 加载 XAML（与 generator
    // 生成的 InitializeComponent 内部实现一致，见 generated/*.g.cs）。
    public partial class App : Application
    {
        private IHost _host;

        public override void Initialize()
        {
            // 直接调用 AvaloniaXamlLoader.Load 加载 App.axaml
            // （generator 不为 Application 根元素生成 InitializeComponent）
            AvaloniaXamlLoader.Load(this);
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
                // Phase 3.1：启动 MainWindow 作为主窗口（spike 骨架版）
                // Phase 1 的 AboutWindow 保留（通过菜单可达）
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                desktop.MainWindow = mainWindow;

                // Avalonia 11 的 Application 基类没有 OnExit 虚方法（WPF 才有
                // OnExit(ApplicationShutdownEventArgs)）。Exit 事件由 IControlledApplicationLifetime
                // 暴露，订阅后在应用退出时优雅关闭 Host，确保后台服务
                // （如 GitOperationQueue）正确释放资源。
                desktop.Exit += OnDesktopExit;
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void OnDesktopExit(object sender, ControlledApplicationLifetimeExitEventArgs e)
        {
            _host?.StopAsync(TimeSpan.FromSeconds(5));
            _host?.Dispose();
        }
    }
}

