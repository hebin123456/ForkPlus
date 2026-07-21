using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ForkPlus.Avalonia.Services;
using ForkPlus.UI;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 ToolbarUserControl（spike 简化升级版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/ToolbarUserControl.xaml.cs（962 行）：
    //   - WPF 构造函数订阅 NotificationCenter 3 个事件 + Initialize(MainWindow) 挂 18 个 Click
    //   - WPF InitializeAppearanceToolBarButtonContextMenu() 构造主题菜单（~100 行）
    //   - WPF RefreshPullPushBadges/RefreshToolbar/RefreshUndoRedoButtons 等 ~15 个刷新方法
    //
    // Avalonia 版差异：
    //   - 自定义控件 ToolbarButton/DropDownButton/ToolbarDropDownButton → 原生 Button
    //   - WPF PNG 图标 → emoji 文本
    //   - WPF Visibility.Collapsed/Visible → Avalonia IsVisible=false/true
    //   - ContextMenu.Open() 在 Avalonia 中直接调用 menu.Open(control)
    //
    // spike 简化：
    //   - 用 StackPanel + Button 显示按钮，emoji 替代 PNG 图标
    //     （Fetch=⬇ / Pull=⬇⬆ / Push=⬆ / Branch=🌿 / Commit=✓ / Stash=📥）
    //   - 主题切换菜单用 ContextMenu 动态构造
    //   - SetRepository(object) / UpdateBadges() / Refresh() 公共方法签名保留
    //   - Undo/Redo/Stash/Branch/OpenIn 下拉菜单暂不实现
    //   - NotificationCenter 事件订阅暂不接入
    public partial class ToolbarUserControl : UserControl
    {
        private readonly IThemeService _themeService;

        // spike 占位：当前仓库引用（真实类型 Repository，Core 工程中）
        public object Repository { get; private set; }

        public ToolbarUserControl(IThemeService themeService)
        {
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            InitializeComponent();
        }

        // ===== 公共方法（对照 WPF）=====

        // 对照 WPF: Initialize(MainWindow mainWindow)
        //   WPF 版挂接 18 个 Click + 3 个 NotificationCenter 事件
        //   spike 版：Click 已在 axaml 绑定，NotificationCenter 暂不接入
        public void Initialize(object mainWindow)
        {
            Console.WriteLine("[Toolbar] Initialize (spike placeholder)");
        }

        // 对照 WPF: Refresh()
        //   刷新工具栏状态（Pull/Push badges / Undo/Redo 启用状态等）
        public void Refresh()
        {
            Console.WriteLine("[Toolbar] Refresh (spike placeholder)");
            UpdateBadges();
        }

        // 对照 WPF: SetRepository(Repository repository)
        //   设置当前仓库，null 时禁用大部分按钮
        public void SetRepository(object repository)
        {
            Repository = repository;
            bool hasRepo = repository != null;
            if (FetchButton != null) FetchButton.IsEnabled = hasRepo;
            if (PullButton != null) PullButton.IsEnabled = hasRepo;
            if (PushButton != null) PushButton.IsEnabled = hasRepo;
            if (BranchButton != null) BranchButton.IsEnabled = hasRepo;
            if (CommitButton != null) CommitButton.IsEnabled = hasRepo;
            if (StashButton != null) StashButton.IsEnabled = hasRepo;
            if (StatusText != null)
            {
                StatusText.Text = hasRepo ? "(repository loaded)" : "(no repository)";
            }
        }

        // 对照 WPF: UpdateBadges()
        //   更新 Pull/Push 数字角标（spike 用 StatusText 占位）
        public void UpdateBadges()
        {
            Console.WriteLine("[Toolbar] UpdateBadges (spike placeholder)");
        }

        // ===== 左侧主操作区按钮（对照 WPF 命令路由到 MainWindow.Commands / RepositoryUserControl.Commands）=====

        private void QuickLaunchButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[Toolbar] Quick Launch clicked");
        }

        private void FetchButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[Toolbar] Fetch clicked");
        }

        private void PullButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[Toolbar] Pull clicked");
        }

        private void PushButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[Toolbar] Push clicked");
        }

        private void CommitButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[Toolbar] Commit clicked");
        }

        private void StashButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[Toolbar] Stash clicked");
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[Toolbar] Undo clicked");
        }

        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[Toolbar] Redo clicked");
        }

        private void ReflogButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[Toolbar] Reflog clicked");
        }

        // ===== 右侧次操作区按钮 =====

        private void BranchButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[Toolbar] Branch clicked");
        }

        private void WorkspacesButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[Toolbar] Workspaces clicked");
        }

        private async void AppearanceButton_Click(object sender, RoutedEventArgs e)
        {
            // 对照 WPF: InitializeAppearanceToolBarButtonContextMenu()
            // spike 版：动态弹出主题选择菜单
            var menu = new ContextMenu();

            // 非纯色主题
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

            // 纯色主题二级菜单
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

            if (sender is Button button)
            {
                menu.Open(button);
            }
        }

        private void ConsoleButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[Toolbar] Console clicked");
        }

        private void AiButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[Toolbar] AI clicked");
        }

        private void OpenInButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[Toolbar] Open in clicked");
        }
    }
}
