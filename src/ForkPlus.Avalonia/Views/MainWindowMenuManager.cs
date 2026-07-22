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
            // 调用 MainWindow.Instance 公共方法（替代空壳 RelayCommand.Execute）
            yield return CreateMenuItem("New Repository", () => { });  // spike 跳过（依赖 ShowCreateRepositoryWindow）
            yield return CreateMenuItem("Clone Repository", () => MainWindow.Instance?.ShowCloneWindow());
            yield return CreateMenuItem("Init git mm Repository", () => { });  // spike 跳过
            yield return CreateSeparator();
            yield return CreateMenuItem("New Tab", () => { });  // spike 单 tab
            yield return CreateMenuItem("Open Repository", () => MainWindow.Instance?.OpenRepositoryViaDialog());
            yield return CreateMenuItem("Quick Launch", () => { });  // spike 跳过
            yield return CreateMenuItem("Close Tab", () => { });  // spike 单 tab
            yield return CreateSeparator();
            yield return CreateMenuItem("Configure SSH Keys", () => MainWindow.Instance?.ShowConfigureSshKeysWindow());
            yield return CreateMenuItem("Accounts", () => MainWindow.Instance?.ShowAccountsWindow());
            yield return CreateSeparator();
            yield return CreateMenuItem("Preferences", () => MainWindow.Instance?.ShowPreferencesWindow());
            yield return CreateSeparator();
            yield return CreateMenuItem("Exit", () => MainWindow.Instance?.ExitApplication());
        }

        private static IEnumerable<MenuItem> CreateViewMenuItems()
        {
            yield return CreateMenuItem("Commit View", () => MainWindow.Instance?.ActivateCommitView());
            yield return CreateMenuItem("Revision List", () => MainWindow.Instance?.ActivateRevisionList());
            yield return CreateSeparator();
            yield return CreateMenuItem("Show HEAD", () => MainWindow.Instance?.ShowHEAD());
            yield return CreateSeparator();
            yield return CreateMenuItem("Hide Tags", () => MainWindow.Instance?.ToggleHideTags());
            yield return CreateMenuItem("Hide Stashes", () => MainWindow.Instance?.ToggleHideStashes());
            yield return CreateMenuItem("Show Lost Commits (Reflog)", () => MainWindow.Instance?.ShowReflogWindow());
            yield return CreateMenuItem("Collapse All Merge Revisions", () => MainWindow.Instance?.CollapseAllMerges());
            yield return CreateMenuItem("Filter by Active Branch", () => { });  // spike 跳过（依赖未迁移的 RevisionContextSearch/Filter 系统）
        }

        private static IEnumerable<MenuItem> CreateRepositoryMenuItems()
        {
            yield return CreateMenuItem("Refresh Repository Data", () => MainWindow.Instance?.RefreshRepositoryData());
            yield return CreateSeparator();
            yield return CreateMenuItem("Fetch", () => MainWindow.Instance?.ShowFetchWindow());
            yield return CreateMenuItem("Pull", () => MainWindow.Instance?.ShowPullWindow());
            yield return CreateMenuItem("Push", () => MainWindow.Instance?.ShowPushWindow());
            yield return CreateSeparator();
            yield return CreateMenuItem("Save Stash", () => MainWindow.Instance?.ShowSaveStashWindow());
            yield return CreateSeparator();
            yield return CreateMenuItem("Create Branch", () => MainWindow.Instance?.ShowCreateBranchWindow());
            yield return CreateMenuItem("Create Tag", () => MainWindow.Instance?.ShowCreateTagWindow());
            yield return CreateMenuItem("Create Worktree", () => MainWindow.Instance?.ShowCreateWorktreeWindow());
            yield return CreateSeparator();
            yield return CreateMenuItem("Apply Patch", () => MainWindow.Instance?.ShowApplyPatchWindow());
            yield return CreateSeparator();
            yield return CreateMenuItem("Open in File Explorer", () => MainWindow.Instance?.OpenRepositoryInFileExplorer());
            yield return CreateMenuItem("Open in Shell", () => MainWindow.Instance?.OpenRepositoryInShell());
        }

        private static IEnumerable<MenuItem> CreateWindowMenuItems()
        {
            yield return CreateMenuItem("Previous Tab", () => { });  // spike 单 tab
            yield return CreateMenuItem("Next Tab", () => { });  // spike 单 tab
            yield return CreateSeparator();
            yield return CreateMenuItem("Switch Theme", () => MainWindow.Instance?.SwitchApplicationTheme());
            yield return CreateSeparator();
            yield return CreateMenuItem("Increase Layout Scale", () => { });  // spike 跳过
            yield return CreateMenuItem("Decrease Layout Scale", () => { });  // spike 跳过
        }

        private static IEnumerable<MenuItem> CreateAboutMenuItems()
        {
            yield return CreateMenuItem("Check for Updates", () => { });  // spike 跳过
            yield return CreateSeparator();
            yield return CreateMenuItem("Keyboard Shortcuts", () => { });  // spike 跳过
            yield return CreateMenuItem("Performance Diagnostics", () => { });  // spike 跳过
            yield return CreateSeparator();
            yield return CreateMenuItem("About ForkPlus", () => MainWindow.Instance?.ShowAboutWindow());
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
