using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using ForkPlus.Avalonia.Services;
using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.UI;
using Microsoft.Extensions.DependencyInjection;

namespace ForkPlus.Avalonia.Views
{
    // Phase 3.1 / 3.13：Avalonia 版 MainWindow（spike 端到端装配版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/MainWindow.xaml.cs（579 行）：
    //   - WPF 用 ui:CustomWindow 基类（自定义标题栏 ControlTemplate + TemplatePart）
    //   - Avalonia 用 ExtendClientAreaToDecorationsHint + 自定义标题栏 Border
    //   - WPF Row 2 是 ClosableTabControl（多仓库 tab 切换），spike 简化为单个 RepositoryUserControl
    //
    // Phase 3.13 升级（本版本）：
    //   - 注入 IServiceProvider（DI 容器，用于创建 RepositoryUserControl）
    //   - Loaded 时创建 RepositoryUserControl 装入 MainContentContainer
    //   - 调用 repositoryUserControl.OpenRepository(null) 触发 EnsureLayoutInitialized
    //     → 真实创建 SidebarUserControl + RepositoryContentUserControl 装入容器
    //     → RepositoryContentUserControl.Initialize 订阅 RevisionListView 4 个事件 +
    //       向下注入到 RevisionDetails/CommitUserControl/RevisionListViewUserControl
    //   - 验证端到端 DI 链路：MainWindow → RepositoryUserControl → Sidebar/RepositoryContent → ...
    //
    // 本 spike 版暂不迁移（留待 Phase 3.x 后期）：
    //   - ClosableTabControl + TabManager（多仓库 tab 切换）
    //   - MainWindowMenuManager（动态菜单构造）
    //   - NotificationManagerUserControl（通知中心 Popup）
    //   - 自动后台 fetch / 更新检查 / 仓库状态管理
    //   - ~30 个 CommandBinding 快捷键（InitializeKeyBindings）
    //   - WindowLocationState 持久化（OnSourceInitialized 中恢复窗口位置）
    //
    // 本 spike 版验证：
    //   - 自定义标题栏（ExtendClientAreaToDecorationsHint）
    //   - 窗口生命周期事件（Loaded/Closing/Activated）
    //   - 系统按钮（最小化/最大化/关闭）
    //   - IThemeService 主题切换
    //   - Phase 3.13：Loaded 时 RepositoryUserControl 装入 + EnsureLayoutInitialized 触发
    //     → 整个 Phase 3 spike 树（Sidebar + RepositoryContent + RevisionListView +
    //       RevisionDetails + CommitUserControl + ...）端到端装配
    public partial class MainWindow : Window
    {
        private readonly IThemeService _themeService;
        private readonly IServiceProvider _serviceProvider;
        private bool _startupFinished;

        public MainWindow(IThemeService themeService, IServiceProvider serviceProvider)
        {
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            InitializeComponent();
            RefreshTitle();
            _themeService.ThemeChanged += (_, _) => { /* Phase 3.13 暂不更新 ThemeInfo，原 TextBlock 已移除 */ };
        }

        private void RefreshTitle()
        {
            // 对照 WPF MainWindow.RefreshTitle()：WPF 用 App.AppName + Workspace 名称
            // Phase 3.13 简化：固定 "ForkPlus Avalonia"
            const string title = "ForkPlus Avalonia";
            Title = title;
            if (TitleTextBlock != null)
            {
                TitleTextBlock.Text = title;
            }
        }

        // ===== 窗口生命周期事件（对照 WPF MainWindow.xaml.cs）=====

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 对照 WPF ForkWindow_Loaded：设置 _startUpFinished = true + RestoreSession
            _startupFinished = true;
            Console.WriteLine("[MainWindow] Loaded — startup finished");

            // Phase 3.13：创建 RepositoryUserControl 装入 MainContentContainer
            // 对照 WPF MainWindow.xaml Row 1 ClosableTabControl 装入 RepositoryUserControl
            LoadRepositoryUserControl();
        }

        // Phase 3.13：创建 RepositoryUserControl 装入 MainContentContainer
        // 并触发 EnsureLayoutInitialized 装配 Sidebar + RepositoryContent
        private void LoadRepositoryUserControl()
        {
            Console.WriteLine("[MainWindow] LoadRepositoryUserControl (Phase 3.13)");

            var repositoryUserControl = _serviceProvider.GetRequiredService<RepositoryUserControl>();
            if (MainContentContainer != null)
            {
                MainContentContainer.Content = repositoryUserControl;
            }

            // 对照 WPF: OpenRepository(repository) → EnsureLayoutInitialized + UpdateRepositoryData
            // spike 阶段没有真实 repository，传 null 触发 EnsureLayoutInitialized 装配骨架
            repositoryUserControl.OpenRepository(null);
        }

        private void MainWindow_Closing(object sender, WindowClosingEventArgs e)
        {
            // 对照 WPF Window_Closing：保存窗口状态 + 关闭 NotificationManager
            Console.WriteLine("[MainWindow] Closing");
        }

        private void MainWindow_Activated(object sender, EventArgs e)
        {
            // 对照 WPF Window_Activated：刷新仓库状态（Phase 3.x 后期接入）
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
    }
}
