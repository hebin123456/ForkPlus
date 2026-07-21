using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using ForkPlus.Avalonia.Services;
using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Settings;
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

            // Phase 3.1b：从 ForkPlusSettings.Default 恢复窗口位置/大小/状态。
            // 对照 WPF MainWindow.OnSourceInitialized：从 ForkPlusSettings.Default.MainWindowLocationState
            // 读取 Left/Top/Width/Height/WindowState，应用到 Avalonia 窗口。
            // 注意：ForkPlusSettings 的 WindowState 是 Core 跨平台枚举（ForkPlus.UI.WindowState），
            // Avalonia 窗口用 global::Avalonia.Controls.WindowState，值与 Core 枚举一致（Normal=0/Minimized=1/Maximized=2）。
            WindowLocationState saved = ForkPlusSettings.Default.MainWindowLocationState;
            // 如果上次关闭时是最小化，恢复为 Normal（避免启动就最小化，用户找不到窗口）
            ForkPlus.UI.WindowState restoreState = saved.WindowState == ForkPlus.UI.WindowState.Minimized
                ? ForkPlus.UI.WindowState.Normal
                : saved.WindowState;
            if (saved.Width > 0 && saved.Height > 0)
            {
                Position = new PixelPoint((int)saved.Left, (int)saved.Top);
                Width = saved.Width;
                Height = saved.Height;
            }
            WindowState = (global::Avalonia.Controls.WindowState)restoreState;
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

            // Phase 3.2：通过 DI 创建 ToolbarUserControl 装入 ToolbarContainer。
            // ToolbarUserControl 需要 IThemeService 注入，故不在 XAML 中实例化。
            LoadToolbarUserControl();

            // Phase 3.13：创建 RepositoryUserControl 装入 MainContentContainer
            // 对照 WPF MainWindow.xaml Row 1 ClosableTabControl 装入 RepositoryUserControl
            LoadRepositoryUserControl();
        }

        // Phase 3.2：通过 DI 创建 ToolbarUserControl 装入 ToolbarContainer
        private void LoadToolbarUserControl()
        {
            Console.WriteLine("[MainWindow] LoadToolbarUserControl (Phase 3.2)");
            var toolbar = _serviceProvider.GetRequiredService<ToolbarUserControl>();
            if (ToolbarContainer != null)
            {
                ToolbarContainer.Content = toolbar;
            }
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
            SaveWindowLocationState();
        }

        // Phase 3.1b：窗口位置/大小/状态变化时保存到 ForkPlusSettings（对照 WPF
        // MainWindow.OnSizeChanged/OnLocationChanged/OnStateChanged → ForkPlusSettings.Default.MainWindowLocationState = ...）。
        // 仅在 _startupFinished 后保存，避免初始恢复阶段的双向赋值循环。
        private void SaveWindowLocationState()
        {
            if (!_startupFinished)
            {
                return;
            }
            ForkPlus.UI.WindowState coreState = (ForkPlus.UI.WindowState)WindowState;
            var state = new WindowLocationState(
                Position.X, Position.Y, Width, Height, coreState);
            ForkPlusSettings.Default.MainWindowLocationState = state;
            ForkPlusSettings.Default.Save();
        }

        private void MainWindow_Activated(object sender, EventArgs e)
        {
            // 对照 WPF Window_Activated：刷新仓库状态（Phase 3.x 后期接入）
        }

        // ===== 系统按钮（对照 WPF ControlTemplate 中的 SystemCommands 绑定）=====

        // Phase 0.4：Core 引入了 ForkPlus.UI.WindowState 枚举。MainWindow 继承自
        // global::Avalonia.Controls.Window，bare "WindowState" 在实例方法中会被解析为继承的
        // 实例属性 this.WindowState（Avalonia.Controls.WindowState 类型），无法用于
        // 访问枚举常量。又因本文件位于 namespace ForkPlus.Avalonia.Views，bare
        // "Avalonia.Controls.WindowState" 会被解析为 ForkPlus.Avalonia.Controls.WindowState
        // （不存在），所以必须用 global:: 前缀。

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = global::Avalonia.Controls.WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == global::Avalonia.Controls.WindowState.Maximized
                ? global::Avalonia.Controls.WindowState.Normal
                : global::Avalonia.Controls.WindowState.Maximized;
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
