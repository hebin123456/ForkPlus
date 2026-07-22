using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Editing;

namespace ForkPlus.Avalonia.Controls.Editor
{
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/Editor/ClearTypeLineNumberMargin.cs（13 行）：
    //   - public class ClearTypeLineNumberMargin : LineNumberMargin
    //   - override OnRender：用 Theme.CodeEditor.BackgroundBrush 填充整个 margin 背景
    //     （WPF ClearType 渲染优化：给行号边距画一层纯色背景，避免 ClearType 字体在
    //      透明背景上发糊）
    //
    // Avalonia 版差异：
    //   1. WPF ICSharpCode.AvalonEdit.Editing.LineNumberMargin →
    //      AvaloniaEdit.Editing.LineNumberMargin（API 一致，public 可继承）
    //   2. WPF OnRender(DrawingContext) → Avalonia Render(DrawingContext)
    //      （Avalonia Control 用 Render 替代 WPF OnRender，签名一致）
    //   3. WPF base.RenderSize.Width/Height → Avalonia Bounds.Width/Bounds.Height
    //   4. Theme.CodeEditor.BackgroundBrush 来自本工程 Theme（ForkPlus.Avalonia）
    //   5. namespace 改为 ForkPlus.Avalonia.Controls.Editor
    //   6. spike 简化：ClearType 优化是 WPF-only 概念，Avalonia 跨平台默认渲染不需要，
    //      但保留填充背景实现（与 WPF 行为一致，无副作用）
    public class ClearTypeLineNumberMargin : LineNumberMargin
    {
        // 对照 WPF: protected override void OnRender(DrawingContext drawingContext)
        // Avalonia: public override void Render(DrawingContext drawingContext)
        public override void Render(DrawingContext drawingContext)
        {
            drawingContext.DrawRectangle(global::ForkPlus.Avalonia.Theme.CodeEditor.BackgroundBrush, null, new Rect(0.0, 0.0, Bounds.Width, Bounds.Height));
        }
    }
}
