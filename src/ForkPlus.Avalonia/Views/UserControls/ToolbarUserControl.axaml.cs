using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ForkPlus.Avalonia.Services;
using ForkPlus.Git;
using ForkPlus.UI;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 ToolbarUserControl。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/ToolbarUserControl.xaml.cs（962 行）：
    //   - WPF 构造函数订阅 NotificationCenter 3 个事件 + Initialize(MainWindow) 挂 18 个 Click
    //   - WPF RefreshPullPushBadges/RefreshToolbar/RefreshUndoRedoButtons 等 ~15 个刷新方法
    //
    // 接线策略：主操作按钮通过 MainWindow.Instance 公共方法调用（与菜单命令共用同一套方法），
    //           Refresh() 从 ActiveRepositoryUserControl 读取分支名/ahead-behind 更新 StatusText + badges。
    public partial class ToolbarUserControl : UserControl
    {
        private readonly IThemeService _themeService;

        public ToolbarUserControl(IThemeService themeService)
        {
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            InitializeComponent();
        }

        // ===== 公共方法（对照 WPF）=====

        public void Initialize(object mainWindow)
        {
            Refresh();
        }

        // 对照 WPF: Refresh() — 刷新工具栏状态（分支名 / Pull-Push badges / 按钮启用状态）
        public void Refresh()
        {
            UpdateStatusText();
            UpdateBadges();
        }

        // 对照 WPF: SetRepository(Repository repository) — null 时禁用大部分按钮
        public void SetRepository(object repository)
        {
            bool hasRepo = repository != null;
            if (FetchButton != null) FetchButton.IsEnabled = hasRepo;
            if (PullButton != null) PullButton.IsEnabled = hasRepo;
            if (PushButton != null) PushButton.IsEnabled = hasRepo;
            if (BranchButton != null) BranchButton.IsEnabled = hasRepo;
            if (StashButton != null) StashButton.IsEnabled = hasRepo;
            Refresh();
        }

        // 对照 WPF: RefreshPullPushBadges — 读 RepositoryData.References.ActiveBranch 的 upstream ahead/behind
        // spike 简化：从 ActiveRepositoryUserControl.RepositoryData 读取
        public void UpdateBadges()
        {
            RepositoryUserControl ruc = GetActiveRepositoryUserControl();
            RepositoryData data = ruc?.RepositoryData;
            LocalBranch activeBranch = data?.References?.ActiveBranch;

            bool showBadges = activeBranch != null && !string.IsNullOrEmpty(activeBranch.UpstreamFullReference);
            if (PullBadge != null) PullBadge.IsVisible = showBadges;
            if (PushBadge != null) PushBadge.IsVisible = showBadges;
            // spike：ahead/behind 计数依赖 UpstreamStatusCache，暂显示占位 0
            // WPF: badges.Text = status.BehindCount / status.AheadCount
        }

        // 对照 WPF: StatusUserControl — 显示当前分支名
        // spike 简化：用 StatusText 显示分支名（WPF 是独立 StatusUserControl 300px）
        private void UpdateStatusText()
        {
            RepositoryUserControl ruc = GetActiveRepositoryUserControl();
            LocalBranch activeBranch = ruc?.RepositoryData?.References?.ActiveBranch;
            if (StatusText != null)
            {
                StatusText.Text = activeBranch != null
                    ? activeBranch.Name
                    : "(no repository)";
            }
        }

        private static RepositoryUserControl GetActiveRepositoryUserControl()
        {
            return MainWindow.ActiveRepositoryUserControl as RepositoryUserControl;
        }

        // ===== 左侧主操作区按钮（对照 WPF 命令路由到 MainWindow 公共方法）=====

        private void QuickLaunchButton_Click(object sender, RoutedEventArgs e)
        {
            // spike 跳过（依赖 QuickLaunchWindow）
        }

        private void FetchButton_Click(object sender, RoutedEventArgs e)
            => MainWindow.Instance?.ShowFetchWindow();

        private void PullButton_Click(object sender, RoutedEventArgs e)
            => MainWindow.Instance?.ShowPullWindow();

        private void PushButton_Click(object sender, RoutedEventArgs e)
            => MainWindow.Instance?.ShowPushWindow();

        private void CommitButton_Click(object sender, RoutedEventArgs e)
            => MainWindow.Instance?.ActivateCommitView();

        private void StashButton_Click(object sender, RoutedEventArgs e)
            => MainWindow.Instance?.ShowSaveStashWindow();

        // 对照 WPF StashToolbarDropdownButtonContextMenu_Opened
        // spike 简化：弹出 Stash 操作菜单（Stash / Stash All / Pop Stash / Apply Stash）
        private void StashDropdownButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var menu = new ContextMenu();
                menu.Items.Add(CreateMenuItem("Stash", () => MainWindow.Instance?.ShowSaveStashWindow()));
                menu.Items.Add(new Separator());
                menu.Items.Add(CreateMenuItem("Pop Last Stash", () => { }));
                menu.Items.Add(CreateMenuItem("Apply Last Stash", () => { }));
                menu.Items.Add(new Separator());
                menu.Items.Add(CreateMenuItem("Stash List...", () => { }));
                menu.Open(button);
            }
        }

        // Undo/Redo 依赖未迁移的 UndoStack 系统，spike 暂不接线
        private void UndoButton_Click(object sender, RoutedEventArgs e) { }
        private void UndoDropdownButton_Click(object sender, RoutedEventArgs e) { }
        private void RedoButton_Click(object sender, RoutedEventArgs e) { }
        private void RedoDropdownButton_Click(object sender, RoutedEventArgs e) { }
        private void ReflogButton_Click(object sender, RoutedEventArgs e) { }

        // ===== 右侧次操作区按钮 =====

        private void BranchButton_Click(object sender, RoutedEventArgs e)
            => MainWindow.Instance?.ShowCreateBranchWindow();

        // 对照 WPF BranchToolbarDropdownButtonContextMenu_Opened
        // spike 简化：弹出 Branch 操作菜单（Create Branch / Create Tag / Checkout...）
        private void BranchDropdownButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var menu = new ContextMenu();
                menu.Items.Add(CreateMenuItem("Create Branch...", () => MainWindow.Instance?.ShowCreateBranchWindow()));
                menu.Items.Add(CreateMenuItem("Create Tag...", () => MainWindow.Instance?.ShowCreateTagWindow()));
                menu.Open(button);
            }
        }

        // Workspaces 依赖未迁移的 WorkspaceManager 切换系统，spike 暂不接线
        private void WorkspacesButton_Click(object sender, RoutedEventArgs e) { }

        // 对照 WPF: InitializeAppearanceToolBarButtonContextMenu()
        private async void AppearanceButton_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            foreach (ThemeType theme in ThemeTypeExtensions.AllThemes)
            {
                if (theme.IsSolidColor()) continue;
                var item = new MenuItem
                {
                    Header = theme.SkinName(),
                    IsChecked = _themeService.CurrentTheme == theme,
                    ToggleType = MenuItemToggleType.Radio
                };
                ThemeType themeCopy = theme;
                item.Click += (_, _) => _themeService.ApplyTheme(themeCopy);
                menu.Items.Add(item);
            }
            menu.Items.Add(new Separator());
            var solidColorsParent = new MenuItem { Header = "Solid Colors" };
            foreach (ThemeType solidTheme in ThemeTypeExtensions.SolidColorThemes)
            {
                var subItem = new MenuItem
                {
                    Header = solidTheme.SkinName(),
                    IsChecked = _themeService.CurrentTheme == solidTheme,
                    ToggleType = MenuItemToggleType.Radio
                };
                ThemeType solidCopy = solidTheme;
                subItem.Click += (_, _) => _themeService.ApplyTheme(solidCopy);
                solidColorsParent.Items.Add(subItem);
            }
            menu.Items.Add(solidColorsParent);
            if (sender is Button btn) menu.Open(btn);
        }

        private void ConsoleButton_Click(object sender, RoutedEventArgs e) { }
        private void AiButton_Click(object sender, RoutedEventArgs e) { }

        // 对照 WPF OpenInToolbarDropDownButton — 弹出"打开方式"菜单
        private void OpenInButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var menu = new ContextMenu();
                menu.Items.Add(CreateMenuItem("File Explorer", () => MainWindow.Instance?.OpenRepositoryInFileExplorer()));
                menu.Items.Add(CreateMenuItem("Terminal", () => MainWindow.Instance?.OpenRepositoryInShell()));
                menu.Open(button);
            }
        }

        // ===== 辅助方法 =====

        private static MenuItem CreateMenuItem(string header, Action click)
        {
            var item = new MenuItem { Header = header };
            item.Click += (_, _) => click();
            return item;
        }
    }
}
