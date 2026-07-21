using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 PlaceholderTextBox（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/PlaceholderTextBox.cs（51 行）：
    //   - WPF PlaceholderTextBox : TextBox
    //   - PlaceholderProperty (string) + IconProperty (ImageSource) DependencyProperty
    //   - Loaded：base.ContextMenu = GetContextMenu()
    //   - GetContextMenu：ContextMenu + AddDefaultTextBoxMenuItems(this)
    //     （添加 Cut/Copy/Paste 等默认菜单项）
    //
    // Avalonia 版差异（spike 简化策略，task spec：继承 TextBox + Watermark）：
    //   1. 基类 TextBox → Avalonia.Controls.TextBox（内置 Watermark 属性，等价 WPF Placeholder）
    //   2. WPF DependencyProperty.Register → StyledProperty<T>.Register
    //   3. WPF PlaceholderProperty (string) → spike 映射到 Avalonia TextBox.Watermark
    //      （内置 placeholder，OnPropertyChanged 同步）
    //   4. WPF IconProperty (ImageSource) → spike 保留 StyledProperty<IImage>
    //      （Avalonia 用 IImage 接口替代 WPF ImageSource）
    //   5. WPF Loaded + GetContextMenu + AddDefaultTextBoxMenuItems
    //      → spike 跳过（Avalonia TextBox 已内置 ContextMenu 默认菜单项）
    //   6. spike 跳过 GetContextMenu 虚方法（Avalonia TextBox 自动管理 ContextMenu）
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 TextBox + Placeholder StyledProperty（同步到 Watermark）
    //   - Icon StyledProperty<IImage>
    public class PlaceholderTextBox : TextBox
    {
        // 对照 WPF: PlaceholderProperty (string)
        // spike 版：OnPropertyChanged 同步到 Avalonia TextBox.Watermark（内置 placeholder）
        public static readonly StyledProperty<string> PlaceholderProperty =
            AvaloniaProperty.Register<PlaceholderTextBox, string>(nameof(Placeholder));

        // 对照 WPF: IconProperty (ImageSource)
        // spike 版：用 IImage 替代 ImageSource（Avalonia 用 IImage 接口）
        public static readonly StyledProperty<IImage> IconProperty =
            AvaloniaProperty.Register<PlaceholderTextBox, IImage>(nameof(Icon));

        public string Placeholder
        {
            get => GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public IImage Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public PlaceholderTextBox()
        {
            // 对照 WPF: base.Loaded += delegate { base.ContextMenu = GetContextMenu(); };
            // spike 版跳过：Avalonia TextBox 已内置 ContextMenu 默认菜单项
        }

        // 对照 WPF: Placeholder 变化时同步 ContextMenu（WPF 通过 GetContextMenu）
        // spike 版：Placeholder 变化时同步到 Avalonia TextBox.Watermark
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == PlaceholderProperty)
            {
                // Placeholder → Watermark 同步（spike 简化：用 Avalonia 内置 Watermark 替代 WPF Placeholder）
                Watermark = Placeholder;
            }
        }
    }
}
