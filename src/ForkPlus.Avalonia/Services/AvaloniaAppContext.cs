using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Services
{
    // Phase 6.1：IAppContext 的 Avalonia 实现。
    //
    // 对照 WPF 工程 src/ForkPlus/Services/Wpf/WpfAppContext.cs：
    //   WPF: 所有属性直接转发到静态 App 类（App.ForkDirectoryPath / App.OSVersion /
    //   App.InstanceDirectory / App.ProcessId / App.UserAgent / ...），
    //   Shutdown 调 System.Windows.Application.Current.Shutdown()。
    //
    // Avalonia 11 实现（跨平台，不依赖 WPF 静态 App 类）：
    //   - AppDataDirectory：Environment.SpecialFolder.LocalApplicationData
    //     （Windows: %LOCALAPPDATA%，Linux: ~/.local/share，macOS: ~/Library/Application Support）
    //     对照 WPF App.ForkDirectoryPath 返回的是 exe 所在目录；这里改用 LocalApplicationData
    //     作为应用数据根目录更合理（用户配置/缓存应放此处而非 exe 目录）。
    //   - ForkDataDirectoryPath：AppDataDirectory 下 "ForkPlus" 子目录
    //   - RepositoriesFilePath：ForkDataDirectoryPath 下 "Repositories.json"
    //   - OSVersion：Environment.OSVersion.Version
    //   - InstanceDirectory：AppContext.BaseDirectory（exe 所在目录，对照 WPF App.InstanceDirectory）
    //   - ProcessId / ProcessIdString：Environment.ProcessId
    //   - UserAgent：构造 "ForkPlus/<version>" 字符串
    //   - ForkCredentialHelperPath：基于 ForkDirectoryPath，Windows 加 .exe 后缀
    //   - ForkDirectoryPath：AppContext.BaseDirectory（exe 所在目录）
    //   - Shutdown：通过 IClassicDesktopStyleApplicationLifetime.Shutdown() 优雅退出
    public class AvaloniaAppContext : IAppContext
    {
        // AppDataDirectory：跨平台本地应用数据目录（用户级配置/缓存标准位置）。
        public string AppDataDirectory => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        public string ForkDataDirectoryPath => Path.Combine(AppDataDirectory, "ForkPlus");

        public string RepositoriesFilePath => Path.Combine(ForkDataDirectoryPath, "Repositories.json");

        public Version OSVersion => Environment.OSVersion.Version;

        // AppContext.BaseDirectory 是 .NET 运行时提供的 exe 所在目录（跨平台）。
        // 对照 WPF App.InstanceDirectory（同样返回 exe 所在目录）。
        public string InstanceDirectory => AppContext.BaseDirectory;

        public int ProcessId => Environment.ProcessId;

        public string ProcessIdString => ProcessId.ToString();

        public string UserAgent => $"ForkPlus/{ThisAssemblyVersion}";

        // ForkCredentialHelperPath：Fork 内置 credential helper 可执行文件路径。
        // 平台差异：Windows 用 .exe 后缀，Linux/macOS 无后缀。
        public string ForkCredentialHelperPath => Path.Combine(ForkDirectoryPath,
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "git-credential-fork.exe" : "git-credential-fork");

        // ForkDirectoryPath：应用安装目录（exe 所在目录），对照 WPF App.ForkDirectoryPath。
        public string ForkDirectoryPath => AppContext.BaseDirectory;

        public void Shutdown()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
            else
            {
                Environment.Exit(0);
            }
        }

        private static string ThisAssemblyVersion =>
            typeof(AvaloniaAppContext).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    }
}
