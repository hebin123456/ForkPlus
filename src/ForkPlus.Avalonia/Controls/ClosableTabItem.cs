using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 ClosableTabItem（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/ClosableTabItem.cs（438 行）：
    //   - WPF ClosableTabItem : TabItem
    //   - [TemplatePart] PART_Close (Button) / PART_Header (CenteredDockPanel) / PART_Title (EditableTextBlock)
    //   - TagBrushProperty / IsDirtyProperty DependencyProperty
    //   - Mode 属性（TabItemMode 枚举）
    //   - RepositoryManagerUserControl / RepositoryUserControl / GitMmUserControl 属性
    //   - ActivateRepositoryManagerMode / ActivateRepositoryViewMode / ActivateGitMmMode
    //   - Refresh / RefreshTitle
    //   - PreviewMouseDown/Move + Drop 拖放重排序
    //   - GetContextMenu()：Close All / Close All But This / Rename / Workspaces / 颜色
    //   - NotificationCenter 弱事件订阅
    //
    // Avalonia 版差异（spike 简化策略，task spec：继承 TabItem + 关闭按钮）：
    //   1. 基类 TabItem → Avalonia.Controls.TabItem（API 一致）
    //   2. WPF DependencyProperty.Register → StyledProperty<T>.Register
    //   3. WPF [TemplatePart] + OnApplyTemplate → spike 跳过
    //      （spike 不依赖 ControlTemplate，关闭按钮由外部 axaml 提供）
    //   4. WPF PreviewMouseDown/Move (tunneling) → Avalonia PointerPressed/PointerMoved
    //   5. WPF DragDrop.DoDragDrop → Avalonia DragDrop.DoDragDrop（API 类似）
    //   6. WPF NotificationCenter 弱事件订阅 → spike 跳过（NotificationCenter 在 WPF 工程）
    //   7. WPF VisualTreeAttachmentHelper.TrySetContent → spike 用 Content 直接赋值
    //   8. WPF RepositoryManagerUserControl / RepositoryUserControl / GitMmUserControl
    //      类型来自 ForkPlus.UI.UserControls，spike 用 object 替代
    //   9. WPF TabItemMode 枚举 → spike 本地枚举
    //  10. WPF EditableTextBlock TitleTextBlock → spike 跳过（无 OnApplyTemplate）
    //  11. spike 跳过 GetContextMenu()（依赖 RepositoryManager 单例 + Workspaces）
    //  12. spike 跳过 RenameRepository / RepositoryColors 菜单（依赖 ForkPlus.Core 单例）
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 TabItem + TagBrush / IsDirty StyledProperty
    //   - Mode 属性 + ActivateRepositoryManagerMode / ActivateRepositoryViewMode / ActivateGitMmMode
    //   - Close() 方法（调用父 ClosableTabControl.RemoveTab）
    //   - RefreshTitle() 方法
    public class ClosableTabItem : TabItem
    {
        // 对照 WPF: public static readonly DependencyProperty TagBrushProperty
        //   default Brushes.Transparent
        public static readonly StyledProperty<IBrush> TagBrushProperty =
            AvaloniaProperty.Register<ClosableTabItem, IBrush>(nameof(TagBrush), Brushes.Transparent);

        // 对照 WPF: public static readonly DependencyProperty IsDirtyProperty
        //   default false
        public static readonly StyledProperty<bool> IsDirtyProperty =
            AvaloniaProperty.Register<ClosableTabItem, bool>(nameof(IsDirty));

        // 对照 WPF: public TabItemMode Mode { get; private set; }
        // spike 版本地枚举（替代 WPF ForkPlus.UI.TabItemMode）
        public TabItemMode Mode { get; private set; }

        // 对照 WPF: public RepositoryManagerUserControl RepositoryManagerUserControl
        // spike 版：用 object 替代（避免依赖 WPF 工程 ForkPlus.UI.UserControls）
        public object RepositoryManagerUserControl { get; private set; }

        // 对照 WPF: public RepositoryUserControl RepositoryUserControl
        public object RepositoryUserControl { get; private set; }

        // 对照 WPF: public GitMmUserControl GitMmUserControl
        public object GitMmUserControl { get; private set; }

        public IBrush TagBrush
        {
            get => GetValue(TagBrushProperty);
            set => SetValue(TagBrushProperty, value);
        }

        public bool IsDirty
        {
            get => GetValue(IsDirtyProperty);
            set => SetValue(IsDirtyProperty, value);
        }

        public ClosableTabItem()
        {
            // 对照 WPF: base.PreviewMouseDown += TabItem_PreviewMouseDown
            // spike 版：PointerPressed 替代 PreviewMouseDown
            PointerPressed += TabItem_PointerPressed;
        }

        // 对照 WPF: public void Close()
        //   (base.Parent as ClosableTabControl)?.RemoveTab(this);
        public void Close()
        {
            (Parent as ClosableTabControl)?.RemoveTab(this);
        }

        // 对照 WPF: public void ActivateRepositoryManagerMode()
        //   RepositoryUserControl = null; GitMmUserControl = null;
        //   RepositoryManagerUserControl = new RepositoryManagerUserControl();
        //   Mode = TabItemMode.RepositoryManager;
        //   VisualTreeAttachmentHelper.TrySetContent(this, RepositoryManagerUserControl, ...);
        // spike 版简化：用 object 替代 UserControl 类型，Content 直接赋值
        public void ActivateRepositoryManagerMode(object repositoryManagerUserControl)
        {
            RepositoryUserControl = null;
            GitMmUserControl = null;
            RepositoryManagerUserControl = repositoryManagerUserControl;
            Mode = TabItemMode.RepositoryManager;
            Content = repositoryManagerUserControl;
        }

        // 对照 WPF: public void ActivateRepositoryViewMode(GitModule gitModule)
        // spike 版简化：用 object 替代 GitModule / RepositoryUserControl
        public void ActivateRepositoryViewMode(object repositoryUserControl)
        {
            RepositoryManagerUserControl = null;
            GitMmUserControl = null;
            RepositoryUserControl = repositoryUserControl;
            Mode = TabItemMode.Repository;
            Content = repositoryUserControl;
        }

        // 对照 WPF: public void ActivateGitMmMode(string workspacePath)
        // spike 版简化：用 object 替代 GitMmUserControl
        public void ActivateGitMmMode(object gitMmUserControl)
        {
            RepositoryManagerUserControl = null;
            RepositoryUserControl = null;
            GitMmUserControl = gitMmUserControl;
            Mode = TabItemMode.GitMm;
            Content = gitMmUserControl;
        }

        // 对照 WPF: public void Refresh()
        //   if (Mode == Repository) RepositoryUserControl?.InvalidateAndRefresh(...)
        //   else if (Mode == RepositoryManager) RepositoryManagerUserControl?.Refresh();
        //   else if (Mode == GitMm) GitMmUserControl?.Refresh();
        // spike 版简化：转发到子控件的 Refresh（若实现 IRefreshable 接口）
        public void Refresh()
        {
            // spike 版跳过：子控件的 Refresh 由外部直接调用
            // （spike 不依赖子控件具体类型）
        }

        // 对照 WPF: public void RefreshTitle()
        //   if (Mode == Repository) base.Header = RepositoryUserControl.RepositoryTitle;
        //   else if (Mode == GitMm) base.Header = GitMmUserControl?.WorkspaceTitle ?? "git mm";
        //   else base.Header = PreferencesLocalization.Translate("Repository Manager", ...);
        // spike 版简化：由外部设置 Header（spike 不依赖子控件类型）
        public void RefreshTitle()
        {
            // spike 版跳过：Header 由外部直接设置
        }

        // 对照 WPF: private void TabItem_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        //   _dragStartPoint = e.GetPosition(null);
        // spike 版：PointerPressed 替代 PreviewMouseDown
        private void TabItem_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            // spike 版跳过拖放逻辑（spike 不实现拖放重排序）
            // 对照 WPF: PreviewMouseDown + PreviewMouseMove + Drop 完整拖放流程
        }
    }

    // spike 版本地枚举：TabItemMode（替代 WPF ForkPlus.UI.TabItemMode）
    public enum TabItemMode
    {
        RepositoryManager,
        Repository,
        GitMm,
    }
}
