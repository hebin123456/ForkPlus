using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ForkPlus.Avalonia.Controls;
using ForkPlus.Avalonia.Services;
using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI;
using Microsoft.Extensions.DependencyInjection;

namespace ForkPlus.Avalonia.Views
{
    // Phase 4.0：Avalonia 版 MainWindow（完整迁移 spike 版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/MainWindow.xaml.cs（589 行）：
    //   - WPF 用 ui:CustomWindow 基类（自定义标题栏 ControlTemplate + TemplatePart）
    //   - Avalonia 用 ExtendClientAreaToDecorationsHint + 自定义标题栏 Border
    //   - WPF Row 2 是 ClosableTabControl（多仓库 tab 切换）
    //   - spike 用 ClosableTabControl 替代（spike 简化版，保留多 tab 切换 API）
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. Window 基类不变（Avalonia.Controls.Window）
    //   2. Visibility.Collapsed/Visible → IsVisible = false/true
    //   3. Dispatcher.Invoke → Dispatcher.UIThread.Post
    //   4. OnLoaded override → Loaded += 事件
    //   5. MainWindow.Instance 单例 → spike 保留 public static MainWindow Instance
    //   6. TabManager → ClosableTabControl 简化
    //   7. Application.Current → Application.Current!
    //   8. RepositoryStatusManager / UpdateCheckManager / AutomaticBackgroundFetchManager
    //      保留为内部字段（spike 版简化为日志占位，不接入真实后台 fetch）
    //   9. InitializeKeyBindings → spike 跳过（30 个 CommandBinding 快捷键暂未迁移）
    //  10. OnSourceInitialized → 构造函数中恢复窗口位置（Avalonia 没有 OnSourceInitialized）
    //
    // spike 简化（task spec 关键 API）：
    //   - public static MainWindow Instance 单例
    //   - public ClosableTabControl TabManager { get; }
    //   - public static RepositoryUserControl ActiveRepositoryUserControl
    //   - public void OpenRepository(Repository?)
    //   - public void CloseTab(string path)
    //   - public void RefreshTitle()
    //   - public void ShowMainWindow()
    //   - public void InitializeKeyBindings()（spike 空实现）
    public partial class MainWindow : Window
    {
        private readonly IThemeService _themeService;
        private readonly IServiceProvider _serviceProvider;
        private bool _startupFinished;

        // 当前活动的 RepositoryUserControl（File → Open Repository 时复用）
        private RepositoryUserControl _repositoryUserControl;

        // 对照 WPF: private readonly AutomaticBackgroundFetchManager _automaticBackgroundFetchManager
        // spike 版：用 spike 版的 AutomaticBackgroundFetchManager（namespace ForkPlus.Avalonia）
        private readonly AutomaticBackgroundFetchManager _automaticBackgroundFetchManager = new AutomaticBackgroundFetchManager();

        // 对照 WPF: private readonly UpdateCheckManager _updateCheckManager
        private readonly UpdateCheckManager _updateCheckManager = new UpdateCheckManager();

        // 对照 WPF: private readonly RepositoryStatusManager _repositoryStatusManager
        private readonly RepositoryStatusManager _repositoryStatusManager = new RepositoryStatusManager();

        // 对照 WPF: public static readonly MainWindowCommands Commands = new MainWindowCommands();
        public static readonly MainWindowCommands Commands = new MainWindowCommands();

        // 对照 WPF: private MainWindowMenuManager _menuManager;
        private MainWindowMenuManager _menuManager;

        // 对照 WPF: public static MainWindow Instance => Application.Current.MainWindow as MainWindow;
        // spike 版：保留 public static MainWindow Instance 静态字段（task spec 要求）
        public static MainWindow Instance { get; private set; }

        // 对照 WPF: public TabManager TabManager { get; }
        // spike 版：用 ClosableTabControl 简化（spike 不引入完整 TabManager 类）
        public ClosableTabControl TabManager { get; private set; }

        // 对照 WPF: public JobQueue JobQueue { get; }
        // spike 版：跳过（无 JobQueue 类，spike 用 null 占位）
        public object JobQueue => null;

        public MainWindow(IThemeService themeService, IServiceProvider serviceProvider)
        {
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            InitializeComponent();
            RefreshTitle();
            _themeService.ThemeChanged += (_, _) => { /* Phase 4.0 暂不更新 ThemeInfo */ };

            // spike 版：Instance 单例（WPF 是 Application.Current.MainWindow 派生属性）
            Instance = this;

            // Phase 3.1b：从 ForkPlusSettings.DEFAULT 恢复窗口位置/大小/状态。
            WindowLocationState saved = ForkPlusSettings.Default.MainWindowLocationState;
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

            // 对照 WPF: StartupTimeReporter.MainWindowCreated();
            StartupTimeReporter.MainWindowCreated();

            // 对照 WPF: base.SizeChanged += MainWindow_SizeChanged;
            // Avalonia 用 PropertyChanged 观察 Width/Height/Position（spike 简化在 Closing 时一次性保存）
        }

        // 对照 WPF: public void RefreshTitle()
        public void RefreshTitle()
        {
            Title = "ForkPlus";
        }

        // 对照 WPF: public void ApplyLocalization()
        public void ApplyLocalization()
        {
            _menuManager?.ApplyLocalization();
            // 对照 WPF: Toolbar.ApplyLocalization(); TabManager?.RefreshTabTitles();
            // spike 版跳过（Toolbar 已由 DI 创建，spike 不实现 ApplyLocalization）
        }

        // 对照 WPF: public void PreventRefreshAfterChildDialogClose(string reason)
        public void PreventRefreshAfterChildDialogClose(string reason)
        {
            // spike 版：占位（不实际阻止刷新）
        }

        // 对照 WPF: public void RefreshRepositoriesStatus()
        public void RefreshRepositoriesStatus()
        {
            _repositoryStatusManager.Refresh();
        }

        // 对照 WPF: public void ShowNotificationManager()
        public void ShowNotificationManager()
        {
            // spike 版跳过（NotificationManagerUserControl 未在 MainWindow 装入）
        }

        // 对照 WPF: public void CheckForUpdates()
        public void CheckForUpdates()
        {
            _updateCheckManager.CheckNow();
        }

        // 对照 WPF: public static RepositoryUserControl ActiveRepositoryUserControl
        //   => Instance?.TabManager.ActiveRepositoryUserControl;
        // spike 版：TabManager 用 ClosableTabControl，没有 ActiveRepositoryUserControl 属性
        // → 返回 null 占位（spike 阶段 RepositoryUserControl 未在 tab 中管理）
        public static object ActiveRepositoryUserControl => null;

        // 对照 WPF: public void OpenRepository(Repository?) — 通过 TabManager.OpenRepository(path)
        // spike 版：简化为装入 MainContentContainer（spike 不实现多 tab）
        public void OpenRepository(object repository)
        {
            Console.WriteLine("[MainWindow] OpenRepository (spike)");
            LoadRepositoryUserControl();
        }

        // 对照 WPF: public void CloseTab(string path)
        public void CloseTab(string path)
        {
            Console.WriteLine($"[MainWindow] CloseTab('{path}') (spike)");
            // spike 版跳过（单 tab 模式，不实现关闭）
        }

        // 对照 WPF: public void ShowMainWindow()
        public void ShowMainWindow()
        {
            Show();
            Activate();
        }

        // 对照 WPF: private void InitializeKeyBindings()
        // spike 版：跳过（30 个 CommandBinding 暂未迁移，留待 Phase 4.x 后期）
        public void InitializeKeyBindings()
        {
            Console.WriteLine("[MainWindow] InitializeKeyBindings (spike skipped)");
        }

        // ===== 窗口生命周期事件（对照 WPF MainWindow.xaml.cs）=====

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 对照 WPF ForkWindow_Loaded：
            //   StartupTimeReporter.MainWindowLoaded();
            //   _menuManager.Initialize();
            //   InitializeKeyBindings();
            //   TabManager.RestoreSession();
            //   Toolbar.RefreshWorkspacesButton();
            //   RefreshTitle();
            //   RefreshRepositoriesStatus();
            //   _updateCheckManager.Start();
            //   App.CliArguments.RunCommand();
            //   base.Dispatcher.Async(StartupTimeReporter.UIReady);
            _startupFinished = true;
            StartupTimeReporter.MainWindowLoaded();
            Console.WriteLine("[MainWindow] Loaded — startup finished");

            // Phase 4.0：菜单管理器初始化（spike 版仅添加 File/View/Repository/Window/Help 5 个根菜单项）
            if (MainMenu != null)
            {
                _menuManager = new MainWindowMenuManager(MainMenu);
                _menuManager.Initialize();
            }

            // Phase 3.2：通过 DI 创建 ToolbarUserControl 装入 ToolbarContainer。
            LoadToolbarUserControl();

            // Phase 3.13：创建 RepositoryUserControl 装入 MainContentContainer
            // 对照 WPF: TabManager.RestoreSession() → OpenRepository(repository)
            LoadRepositoryUserControl();

            // 对照 WPF: _updateCheckManager.Start();
            _updateCheckManager.Start();

            // 对照 WPF: base.Dispatcher.Async(StartupTimeReporter.UIReady);
            Dispatcher.UIThread.Post(StartupTimeReporter.UIReady);
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

            // Phase 4.0：用 ClosableTabControl 替代单 ContentContainer
            // spike 阶段简化：直接装入 MainContentContainer（不创建 tab）
            // 真实多 tab 切换留待 Phase 4.x 后期接入 TabManager
            _repositoryUserControl = _serviceProvider.GetRequiredService<RepositoryUserControl>();
            if (MainContentContainer != null)
            {
                MainContentContainer.Content = _repositoryUserControl;
            }

            // 对照 WPF: OpenRepository(repository) → EnsureLayoutInitialized + UpdateRepositoryData
            // spike 阶段没有真实 repository，传 null 触发 EnsureLayoutInitialized 装配骨架
            _repositoryUserControl.OpenRepository(null);
        }

        private void MainWindow_Closing(object sender, WindowClosingEventArgs e)
        {
            // 对照 WPF Window_Closing：保存窗口状态 + 关闭 NotificationManager
            Console.WriteLine("[MainWindow] Closing");
            SaveWindowLocationState();
        }

        // Phase 3.1b：窗口位置/大小/状态变化时保存到 ForkPlusSettings。
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
            // 对照 WPF Window_Activated：刷新仓库状态（Phase 4.x 后期接入）
            // spike 版跳过刷新逻辑（spike 不接入 RefreshActiveCommitViewStatus）
        }

        // ===== 菜单事件 handler（对照 WPF MainWindowMenuManager 动态构造的菜单项）=====

        // File → Open Repository：打开文件夹选择对话框 → 创建 GitModule → OpenRepository
        // 对照 WPF: OpenRepositoryCommand（src/ForkPlus/UI/Commands/OpenRepositoryCommand.cs）
        private async void File_OpenRepository_Click(object sender, RoutedEventArgs e)
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Open Repository",
                AllowMultiple = false
            });
            if (folders.Count == 0) return;

            string path = folders[0].Path.LocalPath;
            var result = new OpenGitRepositoryGitCommand().Execute(path);
            if (!result.Succeeded || result.Result == null)
            {
                Console.WriteLine($"[MainWindow] OpenRepository failed: {path} is not a git repository");
                return;
            }

            Console.WriteLine($"[MainWindow] OpenRepository: {path}");
            _repositoryUserControl?.OpenRepository(result.Result);
            RefreshTitle();
        }

        private void File_CloneRepository_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[MainWindow] Clone Repository (not yet implemented)");
        }

        private void File_Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void View_ToggleToolbar_Click(object sender, RoutedEventArgs e)
        {
            if (ToolbarContainer != null)
                ToolbarContainer.IsVisible = !ToolbarContainer.IsVisible;
        }

        private void View_ToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            // RepositoryUserControl 内部的 Sidebar 可见性切换
            // spike 阶段暂不实现（需要访问 RepositoryUserControl 内部控件）
            Console.WriteLine("[MainWindow] Toggle Sidebar (not yet implemented)");
        }

        private void View_Appearance_Click(object sender, RoutedEventArgs e)
        {
            // 在 Light / Dark / Dracula / SolarizedDark 4 个主题间循环切换
            ThemeType[] cycle = { ThemeType.Light, ThemeType.Dark, ThemeType.Dracula, ThemeType.SolarizedDark };
            int currentIdx = Array.IndexOf(cycle, _themeService.CurrentTheme);
            if (currentIdx < 0) currentIdx = 0;
            int nextIdx = (currentIdx + 1) % cycle.Length;
            _themeService.ApplyTheme(cycle[nextIdx]);
        }

        private void Repository_Fetch_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[MainWindow] Repository → Fetch");
        }

        private void Repository_Pull_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[MainWindow] Repository → Pull");
        }

        private void Repository_Push_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[MainWindow] Repository → Push");
        }

        private void Repository_Branch_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[MainWindow] Repository → Branch");
        }

        private void Repository_Commit_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[MainWindow] Repository → Commit");
        }

        private void Repository_Stash_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[MainWindow] Repository → Stash");
        }

        private void Window_Preferences_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[MainWindow] Window → Preferences");
        }

        private void Help_About_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[MainWindow] Help → About");
        }

        private void Help_Feedback_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[MainWindow] Help → Feedback");
        }
    }
}
