using System;
using Avalonia;

namespace ForkPlus.Avalonia
{
    // Phase 1：Avalonia 应用程序入口。
    // 与 WPF 主工程 src/ForkPlus/App.xaml.cs 的 Application 实现并存互不冲突——
    // 两者在不同的 assembly 中，由各自 exe 启动加载。
    //
    // 启动流程：
    //   1. BuildAvaloniaApp() 配置 Avalonia 应用实例（with InterFont + Diagnostics）
    //   2. DI 容器在 App.OnFrameworkInitializationCompleted 中构建（见 App.axaml.cs）
    //   3. AboutWindow 作为启动窗口（Phase 1 占位，Phase 3 起替换为 MainWindow）
    internal static class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            // Phase 1 阶段不实际调用 biturbo P/Invoke（libgit2 + zlib 原生库），
            // 仅验证 Avalonia 工程骨架可启动。biturbo 原生库的拉取与跨平台路径解析
            // 留待 Phase 3 真正打开仓库时处理（届时会在 Avalonia.csproj 加 RestoreBiturbo
            // target，与 ForkPlus.csproj / ForkPlus.Core.Tests.csproj 同样模式）。
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
        }
    }
}
