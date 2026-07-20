using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using ForkPlus.Avalonia.Services;
using ForkPlus.UI;

namespace ForkPlus.Avalonia.Views
{
    // Phase 3.1：Avalonia 版 MainWindow 骨架（spike 最简版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/MainWindow.xaml.cs（579 行）：
    //   - WPF 用 ui:CustomWindow 基类（自定义标题栏 ControlTemplate + TemplatePart）
    //   - Avalonia 用 ExtendClientAreaToDecorationsHint + 自定义标题栏 Border
    //
    // 本 spike 版暂不迁移（留待 Phase 3.2-3.x）：
    //   - ToolbarUserControl（Phase 3.2）
    //   - ClosableTabControl + TabManager（Phase 3.4）
    //   - MainWindowMenuManager（动态菜单）
    //   - NotificationManagerUserControl（通知 Popup）
    //   - AutomaticBackgroundFetchManager / UpdateCheckManager / RepositoryStatusManager
    //   - ~30 个 CommandBinding 快捷键（InitializeKeyBindings）
    //   - WindowLocationState 持久化（OnSourceInitialized 中恢复窗口位置）
    //
    // 本 spike 版验证：
    //   - 自定义标题栏（ExtendClientAreaToDecorationsHint）
    //   - 窗口生命周期事件（Loaded/Closing/Activated）
    //   - 系统按钮（最小化/最大化/关闭）
    //   - IThemeService 主题切换
    public partial class MainWindow : Window
    {
        private readonly IThemeService _themeService;
        private bool _startupFinished;

        public MainWindow(IThemeService themeService)
        {
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            InitializeComponent();
            RefreshTitle();
            UpdateThemeInfo();
            _themeService.ThemeChanged += (_, _) => UpdateThemeInfo();
        }

        private void RefreshTitle()
        {
            // 对照 WPF MainWindow.RefreshTitle()：WPF 用 App.AppName + Workspace 名称
            // Phase 3.1 简化：固定 "ForkPlus Avalonia"
            // Phase 3.4 起会接入 IWorkspaceSettings 读取当前工作区名
            if (TitleTextBlock != null)
            {
                TitleTextBlock.Text = "ForkPlus Avalonia";
            }
            Title = "ForkPlus Avalonia";
        }

        private void UpdateThemeInfo()
        {
            if (ThemeInfoTextBlock != null)
            {
                ThemeInfoTextBlock.Text = "Theme: " + _themeService.CurrentTheme.SkinName() +
                                          (_themeService.CurrentTheme.IsDarkBase() ? " (Dark)" : " (Light)");
            }
        }

        // ===== 窗口生命周期事件（对照 WPF MainWindow.xaml.cs）=====

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 对照 WPF ForkWindow_Loaded：设置 _startUpFinished = true + RestoreSession
            _startupFinished = true;
            UpdateStatus("Loaded — startup finished");
            Console.WriteLine("[MainWindow] Loaded — startup finished");
        }

        private void MainWindow_Closing(object sender, WindowClosingEventArgs e)
        {
            // 对照 WPF Window_Closing：保存窗口状态 + 关闭 NotificationManager
            UpdateStatus("Closing — saving window state");
            Console.WriteLine("[MainWindow] Closing");
        }

        private void MainWindow_Activated(object sender, EventArgs e)
        {
            // 对照 WPF Window_Activated：刷新仓库状态（Phase 3.x 后期接入）
            if (_startupFinished && StatusTextBlock != null)
            {
                // 避免启动期重复刷新（WPF 用 _startUpFinished guard）
            }
        }

        // ===== 系统按钮（对照 WPF ControlTemplate 中的 SystemCommands 绑定）=====

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // ===== 主题切换（验证 IThemeService）=====

        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            // 在 Light / Dark / Dracula / SolarizedDark 4 个主题间循环切换
            // 验证 AvaloniaThemeService.LoadThemeResources 工作
            ThemeType[] cycle = { ThemeType.Light, ThemeType.Dark, ThemeType.Dracula, ThemeType.SolarizedDark };
            int currentIdx = Array.IndexOf(cycle, _themeService.CurrentTheme);
            if (currentIdx < 0) currentIdx = 0;
            int nextIdx = (currentIdx + 1) % cycle.Length;
            _themeService.ApplyTheme(cycle[nextIdx]);
        }

        private void UpdateStatus(string message)
        {
            if (StatusTextBlock != null)
            {
                StatusTextBlock.Text = message;
            }
            Console.WriteLine("[MainWindow] " + message);
        }
    }
}
