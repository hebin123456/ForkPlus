using Avalonia;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 ToolbarButton（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/ToolbarButton.cs（22 行）：
    //   - WPF ToolbarButton : System.Windows.Controls.Button
    //   - TitleProperty (string) DependencyProperty.Register("Title", typeof(string), typeof(ToolbarButton), new PropertyMetadata(null))
    //   - public string Title { get; set; }
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 基类 System.Windows.Controls.Button → Avalonia.Controls.Button（API 一致）
    //   2. WPF DependencyProperty.Register → StyledProperty<T>.Register
    //   3. WPF PropertyMetadata(default) → StyledProperty 默认值通过 defaults 参数
    //   4. task spec：继承 Button
    //
    // spike 简化：
    //   - 与 WPF 一致：Title StyledProperty + CLR wrapper
    public class ToolbarButton : global::Avalonia.Controls.Button
    {
        // 对照 WPF: public static readonly DependencyProperty TitleProperty
        //   = DependencyProperty.Register("Title", typeof(string), typeof(ToolbarButton), new PropertyMetadata(null));
        public static readonly StyledProperty<string> TitleProperty =
            AvaloniaProperty.Register<ToolbarButton, string>(nameof(Title));

        // 对照 WPF: public string Title { get; set; }
        public string Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }
    }
}
