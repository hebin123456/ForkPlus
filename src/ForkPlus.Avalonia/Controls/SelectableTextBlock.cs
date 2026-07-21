using Avalonia.Controls;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 SelectableTextBlock（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/SelectableTextBlock.cs（67 行）：
    //   - WPF SelectableTextBlock : TextBlock
    //   - TextEditorWrapper 私有内嵌类：通过反射访问 WPF 内部 TextEditor
    //     - RegisterCommandHandlers：注册 Ctrl+C/Ctrl+A 等命令
    //     - CreateFor：从 TextBlock.TextContainer 创建 TextEditor
    //   - 静态构造函数：FocusableProperty + FocusVisualStyleProperty 重写 metadata
    //     + RegisterCommandHandlers(typeof(SelectableTextBlock), true, true, true)
    //   - 实例构造函数：_editor = TextEditorWrapper.CreateFor(this)
    //
    // Avalonia 版差异（spike 简化策略，task spec：继承 TextBlock 原生支持选择）：
    //   1. WPF 反射访问内部 TextEditor → spike 完全跳过
    //      （Avalonia 11 TextBlock 原生支持 SelectionStart/SelectionEnd/SelectedText
    //       + Focusable + Ctrl+C/Ctrl+A 命令，无需反射 hack）
    //   2. WPF FocusableProperty.OverrideMetadata(true) → spike 在构造函数中设置 Focusable = true
    //   3. WPF FocusVisualStyleProperty.OverrideMetadata(null) → spike 跳过
    //      （Avalonia FocusAdorner 用不同机制，spike 不覆盖默认行为）
    //   4. WPF RegisterCommandHandlers → spike 跳过（Avalonia 内置命令处理）
    //   5. spike 保留公共 API（仅构造函数，保持与 WPF 一致）
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 TextBlock（Avalonia 原生支持选择）
    //   - 构造函数设置 Focusable = true（启用键盘焦点）
    //   - 公共 API：与 WPF 一致（无显式公共方法，仅继承 TextBlock 文本显示 + 选择）
    public class SelectableTextBlock : TextBlock
    {
        public SelectableTextBlock()
        {
            // 对照 WPF: FocusableProperty.OverrideMetadata(typeof(SelectableTextBlock),
            //   new FrameworkPropertyMetadata(true))
            // spike 版：在构造函数中设置 Focusable = true
            // （Avalonia TextBlock 默认 Focusable = false，启用选择需先设为 true）
            Focusable = true;

            // 对照 WPF: TextEditorWrapper.CreateFor(this)
            // spike 版跳过：Avalonia TextBlock 已内置 SelectionStart/SelectionEnd/SelectedText
            //   + Ctrl+C/Ctrl+A 命令处理（无需反射 hack）
        }
    }
}
