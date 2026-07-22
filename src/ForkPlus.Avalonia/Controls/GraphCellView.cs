// Avalonia 版 GraphCellView（spike 简化版）。
//
// 对照 WPF 工程 src/ForkPlus/UI/Controls/GraphCellView.cs（377 行）：
//   - 继承 FrameworkElement，OnRender 绘制 commit graph
//   - 13 色分支线 + commit 点 + merge commit 圆圈 + chevron
//   - 鼠标悬浮显示 RevisionGraphTooltipUserControl Popup
//   - ExpandToggle 事件
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF FrameworkElement + OnRender → Avalonia Control + Render
//   2. WPF DrawingContext.DrawLine/DrawEllipse/DrawGeometry → Avalonia DrawingContext 同名 API
//   3. WPF StreamGeometry/StreamGeometryContext → Avalonia StreamGeometry/StreamGeometryContext
//   4. WPF Pen.Freeze() → Avalonia 无此方法（跳过）
//   5. WPF Popup + DispatcherTimer → spike 省略 tooltip Popup（保留注释）
//   6. WPF ColorConverter.ConvertFromString → Avalonia Color.Parse
//   7. WPF DecoratedRevision/GraphInfo/GraphLine → spike 内联简化（用 dynamic）
//   8. WPF Theme.RevisionList.ItemBackgroundBrush → spike 用 Brushes.Transparent
using System;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Input;
using global::Avalonia.Media;

namespace ForkPlus.Avalonia.Controls
{
    public class GraphCellView : Control
    {
        private static readonly double _defaultCellHeight = 22.0;
        private static readonly double _defaultCellWidth = 12.0;
        private static readonly double _commitPointRadius = 1.7;
        private static readonly double _commitMergePointRadius = 5.75;
        private static readonly double _chevronSize = 3.5;
        private static readonly double _penThickness = 1.5;

        private static readonly string[] _branchColors = new string[13]
        {
            "#FF9502", "#FFCC00", "#FF3B30", "#A2845E", "#64DA38", "#1CADF8", "#CB73E1",
            "#8E8E91", "#FF2968", "#30D5C8", "#5856D6", "#B4D435", "#FF6F61"
        };

        private static readonly Pen[] _branchPens;
        private static readonly Pen _mouseOverPen;

        private bool _isMouseOver;

        public static readonly StyledProperty<double> CellHeightProperty =
            AvaloniaProperty.Register<GraphCellView, double>(nameof(CellHeight), _defaultCellHeight);

        public static readonly StyledProperty<bool> ShowGraphToolTipProperty =
            AvaloniaProperty.Register<GraphCellView, bool>(nameof(ShowGraphToolTip), true);

        public double CellHeight
        {
            get => GetValue(CellHeightProperty);
            set => SetValue(CellHeightProperty, value);
        }

        public bool ShowGraphToolTip
        {
            get => GetValue(ShowGraphToolTipProperty);
            set => SetValue(ShowGraphToolTipProperty, value);
        }

        public event EventHandler? ExpandToggle;

        static GraphCellView()
        {
            _branchPens = new Pen[_branchColors.Length];
            for (int i = 0; i < _branchColors.Length; i++)
            {
                _branchPens[i] = new Pen(Brush.Parse(_branchColors[i]), _penThickness);
            }
            _mouseOverPen = new Pen(Brush.Parse("#0092FF"), 2.0);
        }

        public GraphCellView()
        {
            // spike: 省略 DispatcherTimer tooltip 弹窗逻辑
        }

        protected override void OnPointerEntered(PointerEventArgs e)
        {
            base.OnPointerEntered(e);
            _isMouseOver = true;
            InvalidateVisual();
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            _isMouseOver = false;
            InvalidateVisual();
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (_isMouseOver)
            {
                ExpandToggle?.Invoke(this, EventArgs.Empty);
            }
        }

        public override void Render(DrawingContext drawingContext)
        {
            // spike: GraphCellView 的完整绘制逻辑需要 DecoratedRevision/GraphInfo/GraphLine 类型
            // 这些类型在 spike 版中通过 DataContext 动态访问。
            // 简化版：仅绘制 commit 点和直线，不处理曲线/chevron
            if (DataContext is { } dataContext)
            {
                // 尝试通过反射获取 GraphInfo（spike 兼容）
                var graphInfo = dataContext.GetType().GetProperty("GraphInfo")?.GetValue(dataContext);
                if (graphInfo != null)
                {
                    var lines = graphInfo.GetType().GetProperty("Lines")?.GetValue(graphInfo) as Array;
                    if (lines != null)
                    {
                        Width = _defaultCellWidth * lines.Length;
                        foreach (var line in lines)
                        {
                            DrawLineSpike(drawingContext, line);
                        }
                    }

                    // 绘制 commit 点
                    var currentCommitColumn = graphInfo.GetType().GetProperty("CurrentCommitColumn")?.GetValue(graphInfo);
                    var currentCommitLineId = graphInfo.GetType().GetProperty("CurrentCommitLineId")?.GetValue(graphInfo);
                    if (currentCommitColumn is int col && currentCommitLineId is int lineId && lineId >= 0)
                    {
                        var pen = _branchPens[lineId % _branchPens.Length];
                        var center = new Point(_defaultCellWidth * col, CellHeight / 2.0);
                        drawingContext.DrawEllipse(pen.Brush, pen, center, _commitPointRadius, _commitPointRadius);
                    }
                }
            }
            base.Render(drawingContext);
        }

        private void DrawLineSpike(DrawingContext drawingContext, object? line)
        {
            if (line == null) return;
            // spike: 通过反射获取 Column/TopColumn/BottomColumn/Id
            var type = line.GetType();
            var column = type.GetProperty("Column")?.GetValue(line);
            var topColumn = type.GetProperty("TopColumn")?.GetValue(line);
            var bottomColumn = type.GetProperty("BottomColumn")?.GetValue(line);
            var id = type.GetProperty("Id")?.GetValue(line);

            if (column is byte col && id is byte lineId)
            {
                var pen = _branchPens[lineId % _branchPens.Length];
                double x = _defaultCellWidth * col;
                var point = new Point(x, CellHeight / 2.0);

                if (topColumn is byte top && top != 255)
                {
                    var topPoint = new Point(_defaultCellWidth * top, 0);
                    drawingContext.DrawLine(pen, topPoint, point);
                }
                if (bottomColumn is byte bot && bot != 255)
                {
                    var bottomPoint = new Point(_defaultCellWidth * bot, CellHeight);
                    drawingContext.DrawLine(pen, point, bottomPoint);
                }
            }
        }
    }
}
