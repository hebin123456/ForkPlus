using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ForkPlus.Avalonia.Services;
using ForkPlus.UI;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Phase 3.2：Avalonia 版 ToolbarUserControl 骨架（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/ToolbarUserControl.xaml.cs（962 行）：
    //   - WPF 构造函数订阅 NotificationCenter 3 个事件 + Initialize(MainWindow) 挂 18 个 Click
    //   - WPF InitializeAppearanceToolBarButtonContextMenu() 构造主题菜单（~100 行）
    //   - WPF RefreshPullPushBadges/RefreshToolbar/RefreshUndoRedoButtons 等 ~15 个刷新方法
    //
    // 本 spike 版暂不迁移（留待 Phase 3.2 后续子阶段）：
    //   - 完整 Initialize(MainWindow) 逻辑（命令路由到 MainWindow.Commands / RepositoryUserControl.Commands）
    //   - 主题菜单完整构造（暂用简化版 ContextMenu）
    //   - Undo/Redo/Stash/Branch/OpenIn 下拉菜单
    //   - Pull/Push 数字角标
    //   - 多语言（ApplyLocalization）
    //   - NotificationCenter 事件订阅
    //
    // 本 spike 版验证：
    //   - 三列布局正确显示
    //   - 按钮 Click 事件能触发（暂用 placeholder 逻辑）
    //   - Appearance 主题切换菜单可工作（验证 IThemeService）
    public partial class ToolbarUserControl : UserControl
    {
        private readonly IThemeService _themeService;

        public ToolbarUserControl(IThemeService themeService)
        {
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            InitializeComponent();
        }

        // ===== 左侧主操作区按钮（Phase 3.2 后期接入 MainWindow.Commands）=====

        private void QuickLaunchButton_Click(object sender, RoutedEventArgs e)
        {
            // 对照 WPF: MainWindow.Commands.ShowQuickLaunchWindow.Execute()
            Console.WriteLine("[Toolbar] Quick Launch clicked");
        }

        private void FetchButton_Click(object sender, RoutedEventArgs e)
        {
            // 对照 WPF: QuickFetch / ShowFetchWindow
            Console.WriteLine("[Toolbar] Fetch clicked");
        }

        private void PullButton_Click(object sender, RoutedEventArgs e)
        {
            // 对照 WPF: QuickPull / ShowPullWindow
            Console.WriteLine("[Toolbar] Pull clicked");
        }

        private void PushButton_Click(object sender, RoutedEventArgs e)
        {
            // 对照 WPF: QuickPush / ShowPushWindow
            Console.WriteLine("[Toolbar] Push clicked");
        }

        private void StashButton_Click(object sender, RoutedEventArgs e)
        {
            // 对照 WPF: MainWindow.Commands.ShowSaveStashWindow.Execute(repo, gitModule)
            Console.WriteLine("[Toolbar] Stash clicked");
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            // 对照 WPF: MainWindow.Commands.Undo.Execute(repo)
            Console.WriteLine("[Toolbar] Undo clicked");
        }

        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            // 对照 WPF: MainWindow.Commands.Redo.Execute(repo)
            Console.WriteLine("[Toolbar] Redo clicked");
        }

        private void ReflogButton_Click(object sender, RoutedEventArgs e)
        {
            // 对照 WPF: ShowReflogWindow(repo)
            Console.WriteLine("[Toolbar] Reflog clicked");
        }

        // ===== 右侧次操作区按钮 =====

        private void BranchButton_Click(object sender, RoutedEventArgs e)
        {
            // 对照 WPF: MainWindow.Commands.ShowCreateBranchWindow.Execute(repo, null)
            Console.WriteLine("[Toolbar] Branch clicked");
        }

        private void WorkspacesButton_Click(object sender, RoutedEventArgs e)
        {
            // 对照 WPF: SwitchWorkspace.Execute() / InitializeWorkspacesToolbarDropdownButtonContextMenu()
            Console.WriteLine("[Toolbar] Workspaces clicked");
        }

        private async void AppearanceButton_Click(object sender, RoutedEventArgs e)
        {
            // 对照 WPF: InitializeAppearanceToolBarButtonContextMenu()
            // 本 spike 版：弹出一个简单的主题选择菜单
            var menu = new ContextMenu();

            // 非纯色主题（对照 WPF: ThemeTypeExtensions.AllThemes + IsSolidColor() 过滤）
            foreach (ThemeType theme in ThemeTypeExtensions.AllThemes)
            {
                if (theme.IsSolidColor()) continue;

                var item = new MenuItem
                {
                    Header = theme.SkinName(),
                    IsChecked = _themeService.CurrentTheme == theme,
                    ToggleType = MenuItemToggleType.Radio
                };
                ThemeType themeCopy = theme;  // 闭包捕获
                item.Click += (_, _) => _themeService.ApplyTheme(themeCopy);
                menu.Items.Add(item);
            }

            // 分隔符
            menu.Items.Add(new Separator());

            // 纯色主题二级菜单（对照 WPF: "Solid Colors" 子菜单）
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

            // 弹出菜单
            if (sender is Button button)
            {
                menu.Open(button);
            }
        }

        private void ConsoleButton_Click(object sender, RoutedEventArgs e)
        {
            // 对照 WPF: MainWindow.Commands.OpenRepositoryInShellTool.Execute(gitModule)
            Console.WriteLine("[Toolbar] Console clicked");
        }

        private void AiButton_Click(object sender, RoutedEventArgs e)
        {
            // 对照 WPF: 检查 OpenAiService.IsAiReviewConfigured() 后打开 AiDevelopmentWindow
            Console.WriteLine("[Toolbar] AI clicked");
        }

        private void OpenInButton_Click(object sender, RoutedEventArgs e)
        {
            // 对照 WPF: OpenInDropDownButtonContextMenu_Opened
            Console.WriteLine("[Toolbar] Open in clicked");
        }
    }
}
