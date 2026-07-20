using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Phase 3.4 / 3.12：Avalonia 版 RepositoryUserControl（spike 装配版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/RepositoryUserControl.xaml.cs（1392 行）：
    //   - 33 个公共方法（RefreshRepositoryTitle/UpdateRepositoryData/OpenRepository/
    //     InvalidateAndRefresh/SetRepositoryViewMode/ShowLoading/HideLoading/
    //     DisableUserInterface/EnableUserInterface/ResetNotificationBar 等）
    //   - 6 个私有字段（_isDirty/_layoutInitialized/_viewMode/
    //     _loadedRepositoryData/_undoRedo/_currentRepository 等）
    //   - 延迟初始化模式：EnsureLayoutInitialized() 在首次 InvalidateAndRefresh 时
    //     创建 Sidebar + Content 装入 RepositorySidebarContainer/RepositoryContentContainer
    //   - Undo/Redo 子系统（v3.0.0，约 390 行）
    //   - NotificationCenter 事件订阅
    //   - SidebarGridSplitter.DragCompleted 持久化列宽
    //   - 依赖：RepositoryManager.Instance / ForkPlusSettings.Default / NotificationCenter.Current /
    //     MainWindow.Instance / GitModule / RepositoryData / RepositoryStatus / CommitGraphCache
    //
    // Phase 3.12 升级（本版本）：
    //   - 注入 IServiceProvider（DI 容器）
    //   - EnsureLayoutInitialized 真实创建 SidebarUserControl + RepositoryContentUserControl
    //     装入 RepositorySidebarContainer / RepositoryContentContainer
    //   - 调用 RepositoryContentUserControl.Initialize(this, null) 注入依赖
    //   - 验证 DI 链路端到端打通
    //
    // 本 spike 版暂不迁移（留待 Phase 3.4 后续子阶段）：
    //   - 33 个公共方法的完整实现
    //   - Undo/Redo 子系统
    //   - NotificationCenter 事件订阅
    //   - SidebarGridSplitter.DragCompleted 持久化
    //   - OpenRepository/UpdateRepositoryData/InvalidateAndRefresh 完整刷新链
    //   - SidebarUserControl.Initialize(this) 注入（spike 阶段 Sidebar 还没真实 Initialize 方法）
    //
    // 本 spike 版验证：
    //   - Grid 3 列 × 3 行布局正确显示
    //   - NotificationBar / Sidebar / GridSplitter / Content 4 个区域可见
    //   - EnsureLayoutInitialized 真实创建 SidebarUserControl + RepositoryContentUserControl 装入容器
    //   - GridSplitter 可拖动
    public partial class RepositoryUserControl : UserControl
    {
        // 对照 WPF 6 个私有字段（spike 版只保留 _layoutInitialized + _viewMode + _serviceProvider）
        private readonly IServiceProvider _serviceProvider;
        private bool _layoutInitialized;
        private string _viewMode;

        // RepositoryContentUserControl 反向引用（EnsureLayoutInitialized 后赋值，
        // 供 SetRepositoryViewMode 等 public 方法转发调用）
        private RepositoryContentUserControl _content;

        public RepositoryUserControl(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            InitializeComponent();
            _layoutInitialized = false;
            _viewMode = "RevisionViewMode"; // 对照 WPF 默认 RepositoryViewMode.RevisionViewMode
        }

        // ===== 延迟初始化（对照 WPF EnsureLayoutInitialized）=====

        // 对照 WPF: private void EnsureLayoutInitialized()
        //   首次调用时创建 SidebarUserControl 装入 RepositorySidebarContainer，
        //   创建 RepositoryContentUserControl 装入 RepositoryContentContainer，
        //   调用 RepositoryContentUserControl.Initialize(this, sidebarSearchTabItem) 注入依赖。
        //
        // Phase 3.12 真实装配：用 DI 容器解析 SidebarUserControl + RepositoryContentUserControl。
        public void EnsureLayoutInitialized()
        {
            if (_layoutInitialized)
            {
                return;
            }

            Console.WriteLine("[RepositoryUserControl] EnsureLayoutInitialized (Phase 3.12 真实装配)");

            // 对照 WPF: 创建 SidebarUserControl 装入 RepositorySidebarContainer
            var sidebar = _serviceProvider.GetRequiredService<SidebarUserControl>();
            if (RepositorySidebarContainer != null)
            {
                RepositorySidebarContainer.Content = sidebar;
            }
            // TODO Phase 3.x: sidebar.Initialize(this); — spike 阶段 SidebarUserControl 还没真实 Initialize 方法

            // 对照 WPF: 创建 RepositoryContentUserControl 装入 RepositoryContentContainer
            _content = _serviceProvider.GetRequiredService<RepositoryContentUserControl>();
            if (RepositoryContentContainer != null)
            {
                RepositoryContentContainer.Content = _content;
            }

            // 对照 WPF: RepositoryContentUserControl.Initialize(this, sidebarSearchTabItem)
            // spike 阶段 sidebarSearchTabItem 传 null（真实 SearchTabItem 待 Phase 3.x 后续迁移）
            _content?.Initialize(this, null);

            _layoutInitialized = true;
        }

        // ===== 主要公共方法（对照 WPF 33 个公共方法的入口占位）=====

        // 对照 WPF: public void OpenRepository(Repository repository)
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
            _content?.RefreshRevisionItems(null, data, null, null);
        }

        // 对照 WPF: public void SetRepositoryViewMode(RepositoryViewMode mode)
        //   切换 Content 区显示哪个 View（RevisionViewMode/CommitViewMode）
        //   转发给 RepositoryContentUserControl.SetRepositoryViewMode
        public void SetRepositoryViewMode(string mode)
        {
            Console.WriteLine($"[RepositoryUserControl] SetRepositoryViewMode (spike placeholder): {mode}");
            _viewMode = mode;
            EnsureLayoutInitialized();
            _content?.SetRepositoryViewMode(mode);
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
