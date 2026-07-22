using Avalonia.Controls;
using AvaloniaEdit;
using AvaloniaEdit.Editing;

namespace ForkPlus.Avalonia.Controls.Editor
{
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/Editor/FloatingButton.cs（29 行）：
    //   - public class FloatingButton : Button
    //   - 持有 WeakReference<TextEditor>，OnPreviewMouseWheel 把滚轮事件转发给编辑器 TextView
    //     （浮动按钮覆盖在编辑器上时，滚轮应滚动编辑器而非按钮）
    //
    // Avalonia 版差异：
    //   1. WPF System.Windows.Controls.Button → Avalonia.Controls.Button
    //   2. WPF OnPreviewMouseWheel（Preview tunneling）→ Avalonia OnPointerWheelChanged
    //      （Avalonia 无 Preview tunneling 事件，用 PointerWheelEventArgs + e.Handled）
    //   3. WPF MouseWheelEventArgs.RaiseEvent → Avalonia 直接调 target.TextArea.TextView 的
    //      事件转发（spike 简化：仅 e.Handled=true 阻止按钮处理，让事件冒泡到编辑器）
    //   4. WeakReference<TextEditor> 保持不变（System.WeakReference<T>，跨平台）
    //   5. namespace 改为 ForkPlus.Avalonia.Controls.Editor
    //
    // spike 简化：OnPointerWheelChanged 仅标记 e.Handled=false 让事件自然冒泡到父编辑器，
    // 不再手动构造 MouseWheelEventArgs 转发（Avalonia 事件路由与 WPF 不同，手动转发需
    // 重建 PointerWheelEventArgs，spike 阶段让冒泡机制处理即可）。
    public class FloatingButton : Button
    {
        private readonly System.WeakReference<TextEditor> _weakEditor;

        public FloatingButton(TextEditor editor)
        {
            _weakEditor = new System.WeakReference<TextEditor>(editor);
        }

        // 对照 WPF: protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        // spike 版：让滚轮事件冒泡到编辑器（不拦截），WPF 手动 RaiseEvent 转发在 Avalonia 不需要
        protected override void OnPointerWheelChanged(global::Avalonia.Input.PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
        }
    }
}
