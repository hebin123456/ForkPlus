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
    //   - LocalApplicationData：跨平台本地应用数据根目录（Windows: %LOCALAPPDATA%，
    //     Linux: ~/.local/share，macOS: ~/Library/Application Support）。
    //   - AppDataDirectory：LocalApplicationData 下 "ForkPlus" 子目录——对照 WPF
    //     App.ForkDirectoryPath（settings.json 所在目录）。Phase 0.4 修正：原来返回
    //     LocalApplicationData 根目录，与 WPF 不一致，会导致 settings.json 找不到。
    //   - ForkDataDirectoryPath：LocalApplicationData 下 "ForkPlusData" 子目录——对照
    //     WPF App.ForkDataDirectoryPath（repositories.toml 所在目录）。
    //   - RepositoriesFilePath：ForkDataDirectoryPath 下 "repositories.toml"——
    //     对照 WPF App.RepositoriesFilePath（之前误写为 "Repositories.json"，修正）。
    //   - ForkDirectoryPath：与 AppDataDirectory 相同（WPF 也是同样语义，App.ForkDirectoryPath
    //     既是 settings 目录也是其他全局配置的目录）。
    //   - OSVersion：Environment.OSVersion.Version
    //   - InstanceDirectory：AppContext.BaseDirectory（exe 所在目录，对照 WPF App.InstanceDirectory）
    //   - ProcessId / ProcessIdString：Environment.ProcessId
    //   - UserAgent：构造 "ForkPlus/<version>" 字符串
    //   - ForkCredentialHelperPath：基于 InstanceDirectory，Windows 加 .exe 后缀
    //   - Shutdown：通过 IClassicDesktopStyleApplicationLifetime.Shutdown() 优雅退出
    public class AvaloniaAppContext : IAppContext
    {
        // LocalApplicationData：跨平台本地应用数据根目录。
        // Windows: %LOCALAPPDATA%，Linux: ~/.local/share，macOS: ~/Library/Application Support。
        private static string LocalApplicationData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // AppDataDirectory：用户级应用数据目录（settings.json 所在位置）。
        // 对照 WPF App.ForkDirectoryPath = Path.Combine(LocalApplicationData, "ForkPlus")。
        public string AppDataDirectory => Path.Combine(LocalApplicationData, "ForkPlus");

        // ForkDataDirectoryPath：另一个数据目录（repositories.toml 所在位置）。
        // 对照 WPF App.ForkDataDirectoryPath = Path.Combine(LocalApplicationData, "ForkPlusData")。
        public string ForkDataDirectoryPath => Path.Combine(LocalApplicationData, "ForkPlusData");

        // RepositoriesFilePath：repositories.toml 路径，对照 WPF App.RepositoriesFilePath。
        public string RepositoriesFilePath => Path.Combine(ForkDataDirectoryPath, "repositories.toml");

        public Version OSVersion => Environment.OSVersion.Version;

        // AppContext.BaseDirectory 是 .NET 运行时提供的 exe 所在目录（跨平台）。
        // 对照 WPF App.InstanceDirectory（同样返回 exe 所在目录）。
        public string InstanceDirectory => AppContext.BaseDirectory;

        public int ProcessId => Environment.ProcessId;

        public string ProcessIdString => ProcessId.ToString();

        public string UserAgent => $"ForkPlus/{ThisAssemblyVersion}";

        // ForkCredentialHelperPath：Fork 内置 credential helper 可执行文件路径。
        // 该二进制随发布包放在 exe 同级目录，所以基于 InstanceDirectory。
        // 平台差异：Windows 用 .exe 后缀，Linux/macOS 无后缀。
        public string ForkCredentialHelperPath => Path.Combine(InstanceDirectory,
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "git-credential-fork.exe" : "git-credential-fork");

        // ForkDirectoryPath：用户级应用数据目录（与 AppDataDirectory 一致），对照 WPF
        // App.ForkDirectoryPath。Phase 0.4 修正：原来返回 AppContext.BaseDirectory（exe 目录），
        // 但 WPF App.ForkDirectoryPath 实际是 %LOCALAPPDATA%\ForkPlus，需要保持一致以读取
        // 已有的 settings.json 和 custom-commands.json 等全局配置。
        public string ForkDirectoryPath => AppDataDirectory;

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
