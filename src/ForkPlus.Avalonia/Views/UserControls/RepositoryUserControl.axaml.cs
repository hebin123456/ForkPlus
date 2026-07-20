using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Phase 3.4：Avalonia 版 RepositoryUserControl 骨架（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/RepositoryUserControl.xaml.cs（1392 行）：
    //   - 33 个公共方法（RefreshRepositoryTitle/UpdateRepositoryData/OpenRepository/
    //     InvalidateAndRefresh/SetRepositoryViewMode/ShowLoading/HideLoading/
    //     DisableUserInterface/EnableUserInterface/ResetNotificationBar 等）
    //   - 6 个私有字段（_isDirty/_layoutInitialized/_viewMode/
    //     _loadedRepositoryData/_undoRedo/_currentRepository 等）
    //   - 延迟初始化模式：EnsureLayoutInitialized() 在首次 InvalidateAndRefresh 时
    //     创建 Sidebar + Content 装入 RepositorySidebarContainer/RepositoryContentContainer
    //   - Undo/Redo 子系统（v3.0.0，约 390 行）：ExecuteInternal/Undo/Redo/CanUndo/CanRedo
    //   - NotificationCenter 事件订阅（RepositoryNameChanged/RepositoryColorChanged/
    //     UpdateRepoStatusAutomaticallyChanged 等）
    //   - SidebarGridSplitter.DragCompleted 持久化列宽到 ForkPlusSettings
    //   - 依赖：RepositoryManager.Instance / ForkPlusSettings.Default / NotificationCenter.Current /
    //     MainWindow.Instance / GitModule / RepositoryData / RepositoryStatus / CommitGraphCache
    //
    // 本 spike 版暂不迁移（留待 Phase 3.4 后续子阶段 + Phase 3.5-3.10 一起做）：
    //   - 完整 EnsureLayoutInitialized() 逻辑（DI 解析 SidebarUserControl + 内容 UserControl 装入容器）
    //   - 33 个公共方法的完整实现
    //   - Undo/Redo 子系统
    //   - NotificationCenter 事件订阅
    //   - SidebarGridSplitter.DragCompleted 持久化
    //   - OpenRepository/UpdateRepositoryData/InvalidateAndRefresh 完整刷新链
    //
    // 本 spike 版验证：
    //   - Grid 3 列 × 3 行布局正确显示
    //   - NotificationBar / Sidebar / GridSplitter / Content 4 个占位区域可见
    //   - GridSplitter 可拖动（Avalonia GridSplitter 默认行为，无需 code-behind）
    public partial class RepositoryUserControl : UserControl
    {
        // 对照 WPF 6 个私有字段（spike 版只保留 _layoutInitialized + _viewMode 占位）
        private bool _layoutInitialized;
        private string _viewMode;

        public RepositoryUserControl()
        {
            InitializeComponent();
            _layoutInitialized = false;
            _viewMode = "AllCommits"; // 对照 WPF 默认 ViewMode.AllCommits
        }

        // ===== 延迟初始化（对照 WPF EnsureLayoutInitialized）=====

        // 对照 WPF: private void EnsureLayoutInitialized()
        //   首次调用时创建 SidebarUserControl 装入 RepositorySidebarContainer，
        //   创建内容 UserControl（默认 RevisionListViewUserControl）装入 RepositoryContentContainer。
        //   spike 版只打日志，真正实现留待 Phase 3.5（RevisionListViewUserControl 迁移后）。
        private void EnsureLayoutInitialized()
        {
            if (_layoutInitialized)
            {
                return;
            }

            Console.WriteLine("[RepositoryUserControl] EnsureLayoutInitialized (spike placeholder)");
            // TODO Phase 3.5+: DI 解析 SidebarUserControl，装入 RepositorySidebarContainer.Content
            // TODO Phase 3.5+: DI 解析 RevisionListViewUserControl，装入 RepositoryContentContainer.Content
            _layoutInitialized = true;
        }

        // ===== 主要公共方法（对照 WPF 33 个公共方法的入口占位）=====

        // 对照 WPF: public void OpenRepository(Repository repository)
        //   仓库切换入口：重置 _isDirty/_layoutInitialized，触发 EnsureLayoutInitialized + UpdateRepositoryData
        public void OpenRepository(object repository)
        {
            Console.WriteLine($"[RepositoryUserControl] OpenRepository (spike placeholder): {repository}");
            EnsureLayoutInitialized();
        }

        // 对照 WPF: public void InvalidateAndRefresh()
        //   标脏 + 触发刷新链（EnsureLayoutInitialized → UpdateRepositoryData → UpdateRepositoryStatus）
        public void InvalidateAndRefresh()
        {
            Console.WriteLine("[RepositoryUserControl] InvalidateAndRefresh (spike placeholder)");
            EnsureLayoutInitialized();
        }

        // 对照 WPF: public void UpdateRepositoryData(RepositoryData data)
        //   增量刷新 Sidebar + Content 数据
        public void UpdateRepositoryData(object data)
        {
            Console.WriteLine($"[RepositoryUserControl] UpdateRepositoryData (spike placeholder): {data}");
        }

        // 对照 WPF: public void SetRepositoryViewMode(ViewMode mode)
        //   切换 Content 区显示哪个 UserControl（AllCommits/Changes/FileHistory/Blame 等）
        public void SetRepositoryViewMode(string mode)
        {
            Console.WriteLine($"[RepositoryUserControl] SetRepositoryViewMode (spike placeholder): {mode}");
            _viewMode = mode;
        }

        // 对照 WPF: public void RefreshRepositoryTitle()
        public void RefreshRepositoryTitle()
        {
            Console.WriteLine("[RepositoryUserControl] RefreshRepositoryTitle (spike placeholder)");
        }

        // 对照 WPF: public void ShowLoading() / HideLoading()
        public void ShowLoading()
        {
            Console.WriteLine("[RepositoryUserControl] ShowLoading (spike placeholder)");
        }

        public void HideLoading()
        {
            Console.WriteLine("[RepositoryUserControl] HideLoading (spike placeholder)");
        }

        // 对照 WPF: public void DisableUserInterface() / EnableUserInterface()
        //   操作期间禁用/启用 UI（避免并发触发）
        public void DisableUserInterface()
        {
            Console.WriteLine("[RepositoryUserControl] DisableUserInterface (spike placeholder)");
        }

        public void EnableUserInterface()
        {
            Console.WriteLine("[RepositoryUserControl] EnableUserInterface (spike placeholder)");
        }
    }
}
