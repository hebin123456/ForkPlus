using Avalonia;
using Avalonia.Controls;

namespace ForkPlus.Avalonia
{
    // spike：从 WPF 工程 src/ForkPlus/UI/CustomWindow.cs（338 行）迁移（根命名空间新版）。
    //
    // 对照 WPF 源（namespace ForkPlus.UI）：
    //   - public class CustomWindow : Window
    //   - 5 个 TemplatePart：PART_MinimizeButton / PART_MaximizeButton /
    //     PART_RestoreButton / PART_CloseButton / PART_WindowHeader
    //   - 5 个 DependencyProperty：HeaderHeight / ShowHeader /
    //     HideMinimizeMaximizeButtons / IsTitleVisible / WindowResizeBorderThickness
    //   - 构造函数：SetResourceReference(Style) + WindowChrome（自定义标题栏）+ Loaded += Window_Loaded
    //   - OnApplyTemplate：取 4 个按钮 + header，调 AdjustButtonsVisibilityToWindowState
    //   - OnSourceInitialized：HwndSource.AddHook(HwndSourceHook) 处理 WM_GETMINMAXINFO
    //   - HwndSourceHook：处理 Windows 消息 71/36/132，调整 maximized 边框
    //   - OnStateChanged：maximized 时 BorderThickness=0
    //
    // Avalonia 版差异（spike 简化策略，task spec 关键 API）：
    //   1. 继承 Avalonia.Controls.Window + ExtendClientAreaToDecorationsHint = true
    //      （spike 规范：用 Avalonia 11 内置 ExtendClientAreaToDecorationsHint 替代 WPF WindowChrome）
    //   2. 跳过 WindowChrome / HwndSource / HwndSourceHook（WPF-only，Avalonia 不需要）
    //   3. 跳过 4 个按钮 TemplatePart（ExtendClientAreaToDecorationsHint 后用系统/自绘按钮）
    //   4. task spec 关键 API：WindowTitle / ShowLogo / ShowHeader / ShowCancelButton / ShowSubmitButton
    //      用 StyledProperty 注册（Avalonia 11 等价 WPF DependencyProperty）
    //   5. WindowResizeBorderThickness 跳过（WPF 用于 maximized 状态边框调整，Avalonia 自动处理）
    //   6. HideMinimizeMaximizeButtons 跳过（spike 版不单独隐藏系统按钮）
    //
    // 注意：本文件位于 ForkPlus.Avalonia 根命名空间，与 Dialogs/CustomWindow.cs
    //       （ForkPlus.Avalonia.Dialogs 命名空间）共存不冲突（不同命名空间，不同类型）。
    public class CustomWindow : Window
    {
        // task spec 关键 API：WindowTitle（窗口标题文本，供 axaml 模板绑定）
        public static readonly StyledProperty<string> WindowTitleProperty =
            AvaloniaProperty.Register<CustomWindow, string>(nameof(WindowTitle), defaultValue: "");

        // task spec 关键 API：ShowLogo（是否显示 Logo 图标）
        public static readonly StyledProperty<bool> ShowLogoProperty =
            AvaloniaProperty.Register<CustomWindow, bool>(nameof(ShowLogo), defaultValue: true);

        // task spec 关键 API：ShowHeader（是否显示标题栏 Header 区域）
        // 对照 WPF: public static readonly DependencyProperty ShowHeaderProperty
        public static readonly StyledProperty<bool> ShowHeaderProperty =
            AvaloniaProperty.Register<CustomWindow, bool>(nameof(ShowHeader), defaultValue: true);

        // task spec 关键 API：ShowCancelButton（是否显示取消按钮）
        public static readonly StyledProperty<bool> ShowCancelButtonProperty =
            AvaloniaProperty.Register<CustomWindow, bool>(nameof(ShowCancelButton), defaultValue: true);

        // task spec 关键 API：ShowSubmitButton（是否显示提交按钮）
        public static readonly StyledProperty<bool> ShowSubmitButtonProperty =
            AvaloniaProperty.Register<CustomWindow, bool>(nameof(ShowSubmitButton), defaultValue: true);

        // 对照 WPF: public double HeaderHeight
        // spike 版保留 HeaderHeight 供 axaml 模板绑定
        public static readonly StyledProperty<double> HeaderHeightProperty =
            AvaloniaProperty.Register<CustomWindow, double>(nameof(HeaderHeight), defaultValue: 22.0);

        // 对照 WPF: public bool IsTitleVisible
        // spike 版保留 IsTitleVisible 供 axaml 模板绑定
        public static readonly StyledProperty<bool> IsTitleVisibleProperty =
            AvaloniaProperty.Register<CustomWindow, bool>(nameof(IsTitleVisible), defaultValue: false);

        public string WindowTitle
        {
            get => GetValue(WindowTitleProperty);
            set => SetValue(WindowTitleProperty, value);
        }

        public bool ShowLogo
        {
            get => GetValue(ShowLogoProperty);
            set => SetValue(ShowLogoProperty, value);
        }

        public bool ShowHeader
        {
            get => GetValue(ShowHeaderProperty);
            set => SetValue(ShowHeaderProperty, value);
        }

        public bool ShowCancelButton
        {
            get => GetValue(ShowCancelButtonProperty);
            set => SetValue(ShowCancelButtonProperty, value);
        }

        public bool ShowSubmitButton
        {
            get => GetValue(ShowSubmitButtonProperty);
            set => SetValue(ShowSubmitButtonProperty, value);
        }

        public double HeaderHeight
        {
            get => GetValue(HeaderHeightProperty);
            private set => SetValue(HeaderHeightProperty, value);
        }

        public bool IsTitleVisible
        {
            get => GetValue(IsTitleVisibleProperty);
            set => SetValue(IsTitleVisibleProperty, value);
        }

        public CustomWindow()
        {
            // spike: ExtendClientAreaToDecorationsHint = true 扩展客户区到整个窗口
            // （替代 WPF WindowChrome 自定义标题栏，Avalonia 11 内置跨平台支持）
            ExtendClientAreaToDecorationsHint = true;
        }
    }
}
