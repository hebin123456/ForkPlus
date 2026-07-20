using System;
using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ForkPlus.Avalonia.Views
{
    // Phase 1：Avalonia 版 About 窗口（占位实现）。
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/AboutWindow.xaml.cs 的简化版：
    //   - 显示版本号（从 AssemblyInformationalVersionAttribute 读取，与 WPF App.Version 同样逻辑）
    //   - 显示 Copyright（"Copyright © {year} Hebin"，与 WPF AboutWindow 同样格式）
    //   - GitHub 按钮：跨平台打开 URL（Windows/macOS 用 Process.Start，Linux 用 xdg-open）
    //   - License 按钮：Phase 1 占位，仅打印日志；Phase 4.12 迁移 LegalWindow
    //
    // Avalonia 11：axaml 编译时由 source generator 生成 InitializeComponent() 方法（partial class），
    // 无需手写 AvaloniaXamlLoader.Load(this)（该方法在 11.x 已过时）。
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            VersionTextBlock.Text = "Version " + GetVersion();
            CopyrightTextBlock.Text = string.Format("Copyright © {0} Hebin", DateTime.Now.Year);
        }

        private static string GetVersion()
        {
            AssemblyInformationalVersionAttribute informationalVersion =
                Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (informationalVersion != null && !string.IsNullOrEmpty(informationalVersion.InformationalVersion))
            {
                return informationalVersion.InformationalVersion;
            }
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                return version.ToString();
            }
            return "0.0.0.0";
        }

        // 跨平台打开 URL（与 WPF 工程 OpenInBrowser 扩展方法等价）。
        // Windows: UseShellExecute=true 直接走默认浏览器
        // macOS: 用 open 命令
        // Linux: 用 xdg-open 命令
        private static void OpenUrl(string url)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    UseShellExecute = true,
                };
                if (OperatingSystem.IsWindows())
                {
                    psi.FileName = url;
                }
                else if (OperatingSystem.IsMacOS())
                {
                    psi.FileName = "open";
                    psi.Arguments = $"\"{url}\"";
                }
                else
                {
                    psi.FileName = "xdg-open";
                    psi.Arguments = $"\"{url}\"";
                }
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                // Phase 1 占位：日志输出（Phase 6 接入 NLog 后改为 logger.Warn(ex)）
                Console.WriteLine($"Failed to open URL '{url}': {ex.Message}");
            }
        }

        private void OpenGitHub_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string url)
            {
                OpenUrl(url);
            }
        }

        private void OpenLicense_Click(object sender, RoutedEventArgs e)
        {
            // Phase 1 占位：不弹 Legal 窗口，仅打印。
            // Phase 4.12 迁移 LegalWindow（src/ForkPlus/UI/Dialogs/LegalWindow.xaml）
            Console.WriteLine("License button clicked — LegalWindow migration pending Phase 4.12");
        }
    }
}

