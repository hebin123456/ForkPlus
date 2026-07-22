using Avalonia;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 ToolbarDropDownButton（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/ToolbarDropDownButton.cs（35 行）：
    //   - WPF ToolbarDropDownButton : DropDownButton（ForkPlus.UI.Controls.DropDownButton）
    //   - TitleProperty (string) DependencyProperty.Register(..., new PropertyMetadata(null))
    //   - IsArrowVisibleProperty (bool) DependencyProperty.Register(..., new PropertyMetadata(true))
    //   - public string Title { get; set; }
    //   - public bool IsArrowVisible { get; set; }
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 基类 ForkPlus.UI.Controls.DropDownButton → 本 spike 命名空间 DropDownButton
    //      （DropDownButton 已迁移到 ForkPlus.Avalonia/Controls/DropDownButton.cs）
    //   2. WPF DependencyProperty.Register → StyledProperty<T>.Register
    //   3. WPF PropertyMetadata(default) → StyledProperty 默认值通过 defaults 参数
    //      - Title 默认 null（StyledProperty<string> 默认即 null）
    //      - IsArrowVisible 默认 true（通过 defaults: true 指定）
    //   4. task spec：继承 DropDownButton
    //
    // spike 简化：
    //   - 与 WPF 一致：Title / IsArrowVisible StyledProperty + CLR wrapper
    public class ToolbarDropDownButton : DropDownButton
    {
        // 对照 WPF: public static readonly DependencyProperty TitleProperty
        //   = DependencyProperty.Register("Title", typeof(string), typeof(ToolbarDropDownButton), new PropertyMetadata(null));
        public static readonly StyledProperty<string> TitleProperty =
            AvaloniaProperty.Register<ToolbarDropDownButton, string>(nameof(Title));

        // 对照 WPF: public static readonly DependencyProperty IsArrowVisibleProperty
        //   = DependencyProperty.Register("IsArrowVisible", typeof(bool), typeof(ToolbarDropDownButton), new PropertyMetadata(true));
        public static readonly StyledProperty<bool> IsArrowVisibleProperty =
            AvaloniaProperty.Register<ToolbarDropDownButton, bool>(nameof(IsArrowVisible), true);

        // 对照 WPF: public string Title { get; set; }
        public string Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        // 对照 WPF: public bool IsArrowVisible { get; set; }
        public bool IsArrowVisible
        {
            get => GetValue(IsArrowVisibleProperty);
            set => SetValue(IsArrowVisibleProperty, value);
        }
    }
}
