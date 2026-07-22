using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Views
{
    // Phase 4.0：Avalonia 版 MainWindowMenuManager（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/MainWindowMenuManager.cs（393 行）：
    //   - WPF: public class MainWindowMenuManager
    //   - 构造函数：5 个根菜单项 File/View/Repository/Window/Help
    //   - Initialize(): 订阅 NotificationCenter.ActiveTabChanged
    //   - ApplyLocalization(): 翻译 5 个根菜单 Header
    //   - RootMenuItem_SubmenuOpened: 按需构造子菜单（File/View/Repository/Window/Help）
    //   - CreateFileMenuItems / CreateViewMenuItems / CreateRepositoryMenuItems /
    //     CreateWindowMenuItems / CreateAboutMenuItems / CreateDevelopMenuItems
    //   - CreateGitFlowMenuItem / CreateGitLfsMenuItem（嵌套子菜单）
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF Menu/MenuItem → Avalonia.Controls.Menu/MenuItem（API 一致）
    //   2. WPF Separator → Avalonia.Controls.MenuItem（Header=null + IsEnabled=false，
    //      Avalonia Separator 是独立控件）
    //   3. PreferencesLocalization.Translate → ServiceLocator.Localization.Translate
    //   4. NotificationCenter 弱事件订阅 → spike 跳过（spike 不引入 NotificationCenter）
    //   5. WPF SetItems(MenuItem, IEnumerable<Control>) → spike 直接 MenuItem.Items.AddRange
    //   6. WPF commands.XXX.CreateMenuItem(handler) → spike 用 MenuItem + Click 事件
    //   7. WPF commands.XXX.Execute(args) → spike 空实现（命令是空壳）
    //   8. spike 跳过 GitFlow / GitLfs 嵌套子菜单（依赖 RepositoryData/GitFlowSettings 等）
    //
    // spike 简化（task spec 关键 API）：
    //   - 动态菜单构造（File/View/Repository/Window/Help 5 个根菜单）
    //   - ApplyLocalization() 翻译根菜单 Header
    //   - RootMenuItem_SubmenuOpened 按需构造子菜单（spike 简化，每项 Click 空实现）
    public class MainWindowMenuManager
    {
        private readonly Menu _mainMenu;
        private readonly MenuItem _fileMenuItem;
        private readonly MenuItem _repositoryMenuItem;
        private readonly MenuItem _viewMenuItem;
        private readonly MenuItem _windowMenuItem;
        private readonly MenuItem _aboutMenuItem;

        public MainWindowMenuManager(Menu mainMenu)
        {
            _mainMenu = mainMenu;
            _fileMenuItem = AddRootMenuItem("_File");
            _viewMenuItem = AddRootMenuItem("_View");
            _repositoryMenuItem = AddRootMenuItem("_Repository");
            _windowMenuItem = AddRootMenuItem("_Window");
            _aboutMenuItem = AddRootMenuItem("_Help");
        }

        public void Initialize()
        {
            // 对照 WPF: WeakEventManager<NotificationCenter, EventArgs<ClosableTabItem>>.AddHandler(...)
            // spike 版跳过（spike 不引入 NotificationCenter）
            RefreshRepositoryItemState();
        }

        public void ApplyLocalization()
        {
            // 对照 WPF: PreferencesLocalization.Translate → spike 用 ServiceLocator.Localization.Translate
            // spike 版简化：直接用 key 字符串（不实际翻译，避免引入 LocalizationService 依赖循环）
            _fileMenuItem.Header = Translate("_File");
            _viewMenuItem.Header = Translate("_View");
            _repositoryMenuItem.Header = Translate("_Repository");
            _windowMenuItem.Header = Translate("_Window");
            _aboutMenuItem.Header = Translate("_Help");
        }

        private void RefreshRepositoryItemState()
        {
            // 对照 WPF: Visibility visibility = Visibility.Collapsed / Visible
            // Avalonia: IsVisible = false / true
            bool visible = false;
            try
            {
                // spike 版：MainWindow.ActiveRepositoryUserControl 永远返回 null（spike 单 tab 模式）
                // → Repository / View 菜单永远隐藏（spike 简化）
                if (MainWindow.ActiveRepositoryUserControl != null)
                {
                    visible = true;
                }
            }
            finally
            {
                _viewMenuItem.IsVisible = visible;
                _repositoryMenuItem.IsVisible = visible;
            }
        }

        private MenuItem AddRootMenuItem(string header)
        {
            var menuItem = new MenuItem
            {
                Header = Translate(header)
            };
            // 对照 WPF: menuItem.Items.Add(new MenuItem()); （占位空项，SubmenuOpened 时替换）
            menuItem.Items.Add(new MenuItem { Header = "" });
            menuItem.SubmenuOpened += RootMenuItem_SubmenuOpened;
            _mainMenu.Items.Add(menuItem);
            return menuItem;
        }

        private void RootMenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            // 对照 WPF: e.Source is MenuItem menuItem
            if (!(e.Source is MenuItem menuItem))
            {
                return;
            }
            if (menuItem == _fileMenuItem)
            {
                SetItems(_fileMenuItem, CreateFileMenuItems());
            }
            else if (menuItem == _viewMenuItem)
            {
                SetItems(_viewMenuItem, CreateViewMenuItems());
            }
            else if (menuItem == _repositoryMenuItem)
            {
                SetItems(_repositoryMenuItem, CreateRepositoryMenuItems());
            }
            else if (menuItem == _windowMenuItem)
            {
                SetItems(_windowMenuItem, CreateWindowMenuItems());
            }
            else if (menuItem == _aboutMenuItem)
            {
                SetItems(_aboutMenuItem, CreateAboutMenuItems());
            }
        }

        private static IEnumerable<MenuItem> CreateFileMenuItems()
        {
            // 对照 WPF: commands.ShowCreateRepositoryWindow.CreateMenuItem(...)
            // spike 简化：每个菜单项 Click 空实现（命令是空壳）
            yield return CreateMenuItem("New Repository", () => MainWindow.Commands.ShowCreateRepositoryWindow.Execute(null));
            yield return CreateMenuItem("Clone Repository", () => MainWindow.Commands.ShowCloneWindow.Execute(null));
            yield return CreateMenuItem("Init git mm Repository", () => MainWindow.Commands.ShowInitGitMmRepositoryWindow.Execute(null));
            yield return CreateSeparator();
            yield return CreateMenuItem("New Tab", () => MainWindow.Commands.NewTab.Execute(null));
            yield return CreateMenuItem("Open Repository", () => MainWindow.Instance?.OpenRepositoryViaDialog());
            yield return CreateMenuItem("Quick Launch", () => MainWindow.Commands.ShowQuickLaunchWindow.Execute(null));
            yield return CreateMenuItem("Close Tab", () => MainWindow.Commands.CloseActiveTab.Execute(null));
            yield return CreateSeparator();
            yield return CreateMenuItem("Configure SSH Keys", () => MainWindow.Commands.ShowConfigureSSHKeysWindow.Execute(null));
            yield return CreateMenuItem("Accounts", () => MainWindow.Commands.ShowAccountsWindow.Execute(null));
            yield return CreateSeparator();
            yield return CreateMenuItem("Preferences", () => MainWindow.Commands.ShowPreferencesWindow.Execute(null));
            yield return CreateSeparator();
            yield return CreateMenuItem("Exit", () => MainWindow.Commands.ExitApplication.Execute(null));
        }

        private static IEnumerable<MenuItem> CreateViewMenuItems()
        {
            yield return CreateMenuItem("Commit View", () => MainWindow.Commands.ActivateCommitView.Execute(null));
            yield return CreateMenuItem("Revision List", () => MainWindow.Commands.ActivateRevisionList.Execute(null));
            yield return CreateSeparator();
            yield return CreateMenuItem("Show HEAD", () => MainWindow.Commands.ShowHead.Execute(null));
            yield return CreateSeparator();
            yield return CreateMenuItem("Hide Tags", () => MainWindow.Commands.ToggleHideTags.Execute(null));
            yield return CreateMenuItem("Hide Stashes", () => MainWindow.Commands.ToggleHideStashesInRevisionList.Execute(null));
            yield return CreateMenuItem("Show Lost Commits (Reflog)", () => MainWindow.Commands.ToggleShowReflogInRevisionList.Execute(null));
            yield return CreateMenuItem("Collapse All Merge Revisions", () => MainWindow.Commands.ToggleCollapseAllMergeRevisions.Execute(null));
            yield return CreateMenuItem("Filter by Active Branch", () => MainWindow.Commands.ToggleReferenceFilter.Execute(null));
        }

        private static IEnumerable<MenuItem> CreateRepositoryMenuItems()
        {
            yield return CreateMenuItem("Refresh Repository Data", () => MainWindow.Commands.RefreshRepositoryData.Execute(null));
            yield return CreateSeparator();
            yield return CreateMenuItem("Fetch", () => MainWindow.Commands.ShowFetchWindow.Execute(null));
            yield return CreateMenuItem("Pull", () => MainWindow.Commands.ShowPullWindow.Execute(null));
            yield return CreateMenuItem("Push", () => MainWindow.Commands.ShowPushWindow.Execute(null));
            yield return CreateSeparator();
            yield return CreateMenuItem("Save Stash", () => MainWindow.Commands.ShowSaveStashWindow.Execute(null));
            yield return CreateSeparator();
            yield return CreateMenuItem("Create Branch", () => MainWindow.Commands.ShowCreateBranchWindow.Execute(null));
            yield return CreateMenuItem("Create Tag", () => MainWindow.Commands.ShowCreateTagWindow.Execute(null));
            yield return CreateMenuItem("Create Worktree", () => MainWindow.Commands.ShowCreateWorktreeWindow.Execute(null));
            yield return CreateSeparator();
            yield return CreateMenuItem("Apply Patch", () => { });
            yield return CreateSeparator();
            yield return CreateMenuItem("Open in File Explorer", () => MainWindow.Commands.OpenRepositoryInFileExplorer.Execute(null));
            yield return CreateMenuItem("Open in Shell", () => MainWindow.Commands.OpenRepositoryInShellTool.Execute(null));
        }

        private static IEnumerable<MenuItem> CreateWindowMenuItems()
        {
            yield return CreateMenuItem("Previous Tab", () => MainWindow.Commands.SelectPreviousTab.Execute(null));
            yield return CreateMenuItem("Next Tab", () => MainWindow.Commands.SelectNextTab.Execute(null));
            yield return CreateSeparator();
            yield return CreateMenuItem("Switch Theme", () => MainWindow.Commands.SwitchApplicationTheme.Execute(null));
            yield return CreateSeparator();
            yield return CreateMenuItem("Increase Layout Scale", () => MainWindow.Commands.IncreaseLayoutScale.Execute(null));
            yield return CreateMenuItem("Decrease Layout Scale", () => MainWindow.Commands.DecreaseLayoutScale.Execute(null));
        }

        private static IEnumerable<MenuItem> CreateAboutMenuItems()
        {
            yield return CreateMenuItem("Check for Updates", () => MainWindow.Commands.UpdateApplication.Execute(null));
            yield return CreateSeparator();
            yield return CreateMenuItem("Keyboard Shortcuts", () => MainWindow.Commands.OpenKeyboardShortcuts.Execute(null));
            yield return CreateMenuItem("Performance Diagnostics", () => MainWindow.Commands.ShowPerformanceDiagnosticsWindow.Execute(null));
            yield return CreateSeparator();
            yield return CreateMenuItem("About ForkPlus", () => MainWindow.Commands.ShowAboutWindow.Execute(null));
        }

        // spike 版：MenuItem 工厂方法（替代 WPF commands.XXX.CreateMenuItem(handler)）
        private static MenuItem CreateMenuItem(string header, Action clickHandler)
        {
            var menuItem = new MenuItem
            {
                Header = Translate(header)
            };
            menuItem.Click += (s, e) => clickHandler();
            return menuItem;
        }

        // spike 版：Separator 工厂方法（Avalonia Separator 是独立控件，不在 MenuItem.Items 中）
        // 对照 WPF: yield return new Separator();
        // spike 版用空 MenuItem 模拟（Avalonia Separator 添加到 MenuItem.Items 会报类型不匹配）
        private static MenuItem CreateSeparator()
        {
            return new MenuItem
            {
                Header = "───────────────",
                IsEnabled = false
            };
        }

        // 对照 WPF: SetItems(MenuItem, IEnumerable<Control>) — spike 直接 MenuItem.Items.AddRange
        private static void SetItems(MenuItem menu, IEnumerable<MenuItem> items)
        {
            menu.Items.Clear();
            foreach (var item in items)
            {
                menu.Items.Add(item);
            }
        }

        // spike 版：本地翻译方法（替代 PreferencesLocalization.Translate）
        // 对照 WPF: PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
        // spike 版：返回原字符串（移除 _ 助记符前缀），不实际翻译
        private static string Translate(string text)
        {
            // spike 版：剥离助记符 _ 前缀（实际翻译留待 LocalizationService 完整接入）
            return text.Replace("_", "");
        }
    }
}
