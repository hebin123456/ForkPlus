// Avalonia 版 RevisionTimeLine（spike 简化版）。
//
// 对照 WPF 工程 src/ForkPlus/UI/Controls/RevisionTimeLine.cs：
//   - 继承 FrameworkElement，OnRender 绘制 commit 时间线
//   - 显示日期标签 + tick 标记 + active revision 高亮
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF FrameworkElement + OnRender → Avalonia Control + Render
//   2. WPF Typeface/FormattedText → Avalonia FormattedText
//   3. WPF Theme.FindBrush → spike 用固定 Brushes
//   4. 复杂绘制逻辑 → spike 简化为基本线条 + 文本
using System;
using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace ForkPlus.Avalonia.Controls
{
    public class RevisionTimeLine : global::Avalonia.Controls.Control
    {
        private readonly Typeface _typeface;
        private readonly Pen _tickPen;
        private readonly Pen _revisionPen;
        private readonly IBrush _labelBrush;

        public static readonly StyledProperty<object?> ActiveRevisionProperty =
            AvaloniaProperty.Register<RevisionTimeLine, object?>(nameof(ActiveRevision));

        public object? ActiveRevision
        {
            get => GetValue(ActiveRevisionProperty);
            set => SetValue(ActiveRevisionProperty, value);
        }

        public RevisionTimeLine()
        {
            _typeface = new Typeface("Segoe UI");
            _tickPen = new Pen(Brushes.Gray, 1.0);
            _revisionPen = new Pen(Brushes.SteelBlue, 2.0);
            _labelBrush = Brushes.Gray;
        }

        public override void Render(DrawingContext drawingContext)
        {
            // spike: 简化版仅绘制底线 + 日期标签占位
            double height = Bounds.Height;
            double width = Bounds.Width;

            // 底线
            drawingContext.DrawLine(_tickPen, new Point(0, height - 1), new Point(width, height - 1));

            // spike: 完整版需要 RevisionsDataSource 数据来绘制时间线
            // 这里仅绘制占位文本
            var formatted = new FormattedText(
                "Timeline",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                _typeface,
                11,
                _labelBrush);
            drawingContext.DrawText(formatted, new Point(5, 5));
        }
    }
}
