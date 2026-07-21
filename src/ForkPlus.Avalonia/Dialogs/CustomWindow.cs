using Avalonia;
using Avalonia.Controls;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.0a：Avalonia 版 CustomWindow（从 WPF 工程迁移）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/CustomWindow.cs（334 行）：
    //   - public class CustomWindow : Window
    //   - 5 个 TemplatePart：PART_MinimizeButton / PART_MaximizeButton /
    //     PART_RestoreButton / PART_CloseButton / PART_WindowHeader
    //   - 4 个 DependencyProperty：HeaderHeight / ShowHeader /
    //     HideMinimizeMaximizeButtons / IsTitleVisible / WindowResizeBorderThickness
    //   - 构造函数：SetResourceReference(Style) + WindowChrome（自定义标题栏）+
    //     Loaded += Window_Loaded
    //   - OnApplyTemplate：取 4 个按钮 + header，调 AdjustButtonsVisibilityToWindowState
    //   - OnSourceInitialized：HwndSource.AddHook(HwndSourceHook) 处理 WM_GETMINMAXINFO
    //   - HwndSourceHook：处理 Windows 消息 71/36/132（WM_WINDOWPOSCHANGING /
    //     WM_GETMINMAXINFO / WM_NCHITTEST），调整 maximized 边框
    //   - OnStateChanged：maximized 时 BorderThickness=0
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. spike 版直接继承 Avalonia.Window，不做自定义 chrome（Avalonia 11
    //      自带跨平台窗口 chrome，Win/macOS/Linux 都能用系统标题栏）
    //   2. 跳过 WindowChrome / HwndSource / HwndSourceHook（WPF-only，Avalonia 不需要）
    //   3. 跳过 4 个按钮 TemplatePart（用系统标题栏的 Min/Max/Restore/Close）
    //   4. 保留 ShowHeader / IsTitleVisible 属性（StyledProperty）供 ForkPlusDialogWindow 用
    //   5. WindowResizeBorderThickness 跳过（WPF 用于 maximized 状态边框调整，
    //      Avalonia 系统窗口管理器自动处理）
    //
    // 本 spike 版暂不迁移（留 Phase 4.0c 或更后）：
    //   - 自定义窗口 chrome（如需去掉系统标题栏做全自绘标题栏，Phase 4 后期再统一）
    //   - WM_GETMINMAXINFO 处理（Windows 特有的最小/最大尺寸约束）
    //   - HideMinimizeMaximizeButtons（用系统按钮，不单独隐藏）
    //
    // 本 spike 版验证：
    //   - ForkPlusDialogWindow 可继承 CustomWindow 拿到 Avalonia.Window 全部能力
    //   - ShowHeader / IsTitleVisible 属性可绑定到 axaml 模板
    public class CustomWindow : Window
    {
        // 对照 WPF: public static readonly DependencyProperty ShowHeaderProperty
        public static readonly StyledProperty<bool> ShowHeaderProperty =
            AvaloniaProperty.Register<CustomWindow, bool>(nameof(ShowHeader), defaultValue: true);

        // 对照 WPF: public static readonly DependencyProperty IsTitleVisibleProperty
        public static readonly StyledProperty<bool> IsTitleVisibleProperty =
            AvaloniaProperty.Register<CustomWindow, bool>(nameof(IsTitleVisible), defaultValue: false);

        // 对照 WPF: public static readonly DependencyProperty HeaderHeightProperty
        public static readonly StyledProperty<double> HeaderHeightProperty =
            AvaloniaProperty.Register<CustomWindow, double>(nameof(HeaderHeight), defaultValue: 22.0);

        public bool ShowHeader
        {
            get => GetValue(ShowHeaderProperty);
            set => SetValue(ShowHeaderProperty, value);
        }

        public bool IsTitleVisible
        {
            get => GetValue(IsTitleVisibleProperty);
            set => SetValue(IsTitleVisibleProperty, value);
        }

        public double HeaderHeight
        {
            get => GetValue(HeaderHeightProperty);
            private set => SetValue(HeaderHeightProperty, value);
        }

        public CustomWindow()
        {
            // spike 版：不做自定义 chrome，Avalonia 11 默认系统标题栏跨平台可用
            // Phase 4.0c 如需自绘标题栏，再覆盖 Window 的 Template + 添加 PART_ 按钮处理
        }
    }
}
