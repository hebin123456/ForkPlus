using Avalonia.Media;
using AvaloniaEdit.Rendering;

namespace ForkPlus.Avalonia.Controls.Editor
{
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/Editor/ClearTypeBackgroundRenderer.cs（15 行）：
    //   - public class ClearTypeBackgroundRenderer : IBackgroundRenderer
    //   - Layer => KnownLayer.Background
    //   - Draw(textView, drawingContext)：用 Theme.CodeEditor.BackgroundBrush 填充整个 textView 背景
    //     （WPF ClearType 渲染优化：给编辑器背景画一层纯色，避免 ClearType 字体在透明背景上发糊）
    //
    // Avalonia 版差异：
    //   1. 基类 ICSharpCode.AvalonEdit.Rendering.IBackgroundRenderer →
    //      AvaloniaEdit.Rendering.IBackgroundRenderer（接口一致：Layer + Draw）
    //   2. drawingContext.DrawRectangle(brush, pen, rect) → Avalonia DrawingContext.DrawRectangle
    //      （参数顺序：brush, pen, rect — Avalonia 与 WPF 一致）
    //   3. textView.ActualWidth / ActualHeight → Avalonia textView.Bounds.Width / Bounds.Height
    //   4. Theme.CodeEditor.BackgroundBrush 来自本工程 Theme（ForkPlus.Avalonia）
    //   5. namespace 改为 ForkPlus.Avalonia.Controls.Editor
    //   6. spike 简化：ClearType 优化是 WPF-only 概念，Avalonia 跨平台默认渲染不需要此层，
    //      Draw 保留填充背景实现（与 WPF 行为一致，无副作用）
    public class ClearTypeBackgroundRenderer : IBackgroundRenderer
    {
        public KnownLayer Layer => KnownLayer.Background;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            drawingContext.DrawRectangle(Theme.CodeEditor.BackgroundBrush, null, new global::Avalonia.Rect(0.0, 0.0, textView.Bounds.Width, textView.Bounds.Height));
        }
    }
}
