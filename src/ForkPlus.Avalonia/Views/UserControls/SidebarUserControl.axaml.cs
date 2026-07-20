using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Phase 3.3：Avalonia 版 SidebarUserControl 骨架（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/SidebarUserControl.xaml.cs（2714 行）：
    //   - WPF 构造函数创建 7 个 SidebarGroupItem（Pinned/Branches/Remotes/Tags/Stashes/Submodules/Worktrees）
    //     挂到 _root（FolderSidebarItem）
    //   - WPF Initialize(RepositoryUserControl) 订阅事件 + 注册 key bindings
    //   - WPF UpdateRepositoryData(RepositoryData) 增量 diff 更新（Diff<T>）
    //   - WPF 10+ 个右键菜单生成方法（CreateLocalBranchContextMenuItems 等）
    //
    // Sidebar 与 RepositoryUserControl 的关系：
    //   - Sidebar 是 RepositoryUserControl 的子控件（非独立挂在 MainWindow）
    //   - Phase 3.4 RepositoryUserControl 迁移时会引用本骨架
    //
    // 本 spike 版暂不迁移（留待 Phase 3.4 与 RepositoryUserControl 一起做）：
    //   - 完整 Initialize(RepositoryUserControl) 逻辑
    //   - SidebarTreeView + MultiselectionTreeView（需先迁移自定义控件）
    //   - FilterTextBox 自定义控件（暂用普通 TextBox + TextChanged）
    //   - EditableTextBlock（仓库名可编辑，自定义控件）
    //   - DropDownButton（Repo 设置菜单，自定义控件）
    //   - UpdateRepositoryData / UpdateRepositoryStatus 等 ~15 个刷新方法
    //   - 10+ 个右键菜单生成方法
    //   - SearchTabItem / ServiceTabItem（自定义 TabItem）
    //   - 拖拽 Drop 上下文菜单
    //   - 多语言（ApplyLocalization）
    //   - NotificationCenter 事件订阅
    //
    // 本 spike 版验证：
    //   - 顶层 Grid 2 行布局正确显示
    //   - 5 个 RadioButton（Changes/AllCommits/Branches/Search/Service）能切换
    //   - 空 TabControl 能展示
    public partial class SidebarUserControl : UserControl
    {
        public SidebarUserControl()
        {
            InitializeComponent();
        }

        // ===== Repo 标题栏按钮 =====

        private void RepoSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // 对照 WPF: RepositorySettingsDropdownButtonContextMenu_Opened
            Console.WriteLine("[Sidebar] Repo settings clicked");
        }

        private void RepoHideButton_Click(object sender, RoutedEventArgs e)
        {
            // 对照 WPF: 隐藏 Sidebar
            Console.WriteLine("[Sidebar] Hide clicked");
        }

        // ===== 视图切换 RadioButton =====

        private void ChangesRadioButton_Click(object sender, RoutedEventArgs e)
        {
            // 对照 WPF: Changes_Selected → RepositoryUserControl.SetRepositoryViewMode(Changes)
            Console.WriteLine("[Sidebar] Changes view selected");
        }

        private void AllCommitsRadioButton_Click(object sender, RoutedEventArgs e)
        {
            // 对照 WPF: AllCommits_Selected → RepositoryUserControl.SetRepositoryViewMode(AllCommits)
            Console.WriteLine("[Sidebar] All Commits view selected");
        }

        private void BranchesRadioButton_Click(object sender, RoutedEventArgs e)
        {
            // 对照 WPF: 切换到 BranchesTabItem
            if (SidebarTabControl != null && BranchesTabItem != null)
            {
                SidebarTabControl.SelectedItem = BranchesTabItem;
            }
        }

        private void SearchRadioButton_Click(object sender, RoutedEventArgs e)
        {
            // 对照 WPF: 切换到 SearchTabItem
            if (SidebarTabControl != null && SearchTabItem != null)
            {
                SidebarTabControl.SelectedItem = SearchTabItem;
            }
        }

        private void ServiceRadioButton_Click(object sender, RoutedEventArgs e)
        {
            // 对照 WPF: 切换到 ServiceTabItem
            if (SidebarTabControl != null && ServiceTabItem != null)
            {
                SidebarTabControl.SelectedItem = ServiceTabItem;
            }
        }

        // ===== TabControl 事件 =====

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 对照 WPF: TabControl_SelectionChanged
            if (e.Source is TabControl tabControl && tabControl.SelectedItem is TabItem item)
            {
                Console.WriteLine($"[Sidebar] Tab changed: {item.Header}");
            }
        }

        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 对照 WPF: _refreshFilterAction.Invoke(text) — DelayedAction 防抖
            if (sender is TextBox textBox)
            {
                Console.WriteLine($"[Sidebar] Filter: {textBox.Text}");
            }
        }
    }
}
