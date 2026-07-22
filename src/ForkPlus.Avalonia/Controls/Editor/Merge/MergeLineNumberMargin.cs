using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit.Rendering;
using ForkPlus.Git.Merge;
using ForkPlus.Git.Merge.Presentation;
using ForkPlus.Settings;
using ForkPlus.UI;

namespace ForkPlus.Avalonia.Controls.Editor.Merge
{
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/Editor/Merge/MergeLineNumberMargin.cs（292 行）：
    //   - internal class MergeLineNumberMargin : ClearTypeLineNumberMargin
    //   - 静态字段：_typeface / _textBrush / _separatorPenLight / _separatorPenDark /
    //     _mergeConflictMouseOverBrushLight / _mergeConflictMouseOverBrushDark /
    //     _mergeConflictSelectedBrush / HorizontalMargin
    //   - 实例字段：_editor / _mouseOverLine / _separatorPen /
    //     _mergeConflictMouseOverBrush / _lineNumberLength / _lineNumbers (Dictionary<int,int>)
    //   - 构造函数：设置 typeface/emSize + RefreshPen + 订阅 NotificationCenter + ClearTypeHint
    //   - UpdateLineNumbersData：遍历 MergeConflictView.Chunks.Lines 构建 _lineNumbers 映射
    //   - MeasureOverride：根据 _lineNumberLength 计算宽度
    //   - OnRender：base.OnRender + 遍历 VisualLines 画行号 + 选中/悬停 chevron + 分隔线
    //   - CreateShevronGeometry：用 StreamGeometry 画箭头形状
    //   - OnMouseLeave / OnMouseMove / OnMouseLeftButtonDown：行选择交互
    //   - IsLineSelected / IsLineSelectable：查询 MergeConflictView 行状态
    //   - RefreshPen：读资源或回退到 light/dark 静态画刷
    //   - CreateFormattedText：用 typeface/emSize/brush 创建 FormattedText
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF Typeface(FontFamily, ..., FontFamily fallback) → Avalonia Typeface（无 fallback）
    //   2. WPF 基类 protected typeface/emSize → spike 自建字段（AvaloniaEdit 无此字段）
    //   3. WPF OnRender → Avalonia Render
    //   4. WPF RenderOptions.SetClearTypeHint → spike 移除
    //   5. WPF NotificationCenter → spike 移除
    //   6. WPF OnMouseLeave/Move/LeftButtonDown → Avalonia OnPointerExited/Moved/Pressed
    //   7. WPF MouseEventArgs.GetPosition → Avalonia PointerEventArgs.GetPosition
    //   8. WPF FormattedText(text, ..., pixelsPerDip) → Avalonia FormattedText（无 pixelsPerDip）
    //   9. WPF StreamGeometry / StreamGeometryContext → Avalonia StreamGeometry / StreamGeometryContext
    //  10. namespace 改为 ForkPlus.Avalonia.Controls.Editor.Merge
    //
    // spike 简化（task spec：复杂渲染逻辑可简化为空实现 + 注释）：
    //   - 保留静态画刷字段 + RefreshPen（主题感知）
    //   - 保留 UpdateLineNumbersData（行号映射构建，逻辑简单）
    //   - Render：调 base.Render + 画分隔线（行号由基类渲染，chevron 留 phase 3.9b）
    //   - MeasureOverride：调基类
    //   - OnPointerExited/Moved/Pressed：空实现 + 注释（行选择交互留 phase 3.9b）
    //   - IsLineSelected / IsLineSelectable：保留完整逻辑（纯 C#，无 UI 依赖）
    //   - CreateShevronGeometry：返回 null（StreamGeometry 留 phase 3.9b）
    internal class MergeLineNumberMargin : ClearTypeLineNumberMargin
    {
        // ===== 静态画刷（Avalonia.Media，无 Freeze） =====
        private static readonly IBrush TextBrush = new SolidColorBrush(Color.FromRgb(192, 192, 192));

        private static readonly Pen SeparatorPenLight = new Pen(new SolidColorBrush(Color.FromRgb(218, 218, 215)), 1.0);

        private static readonly Pen SeparatorPenDark = new Pen(new SolidColorBrush(Color.FromRgb(110, 110, 110)), 1.0);

        private static readonly IBrush MergeConflictMouseOverBrushLight = new SolidColorBrush(Color.FromRgb(216, 216, 216));

        private static readonly IBrush MergeConflictMouseOverBrushDark = new SolidColorBrush(Color.FromRgb(165, 165, 165));

        private static readonly IBrush MergeConflictSelectedBrush = new SolidColorBrush(Color.FromRgb(59, 137, 218));

        private const double HorizontalMargin = 10.0;

        // spike 自建 typeface（AvaloniaEdit LineNumberMargin 无基类 protected 字段）
        private static readonly Typeface MarginTypeface = new Typeface(
            FontConstants.MonospaceFontFamily, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal);
        private const double EmSize = 11.0;

        private readonly MergeCodeEditor _editor;
        private int _mouseOverLine = -1;
        private Pen _separatorPen;
        private IBrush _mergeConflictMouseOverBrush;
        private int _lineNumberLength = 2;
        private Dictionary<int, int> _lineNumbers = new Dictionary<int, int>();

        public MergeLineNumberMargin(MergeCodeEditor editor)
        {
            _editor = editor;
            RefreshPen();
            // 对照 WPF: RenderOptions.SetClearTypeHint(this, ClearTypeHint.Enabled);
            // spike 移除（WPF-only）
            // 对照 WPF: WeakEventManager<NotificationCenter, ...>.AddHandler(...);
            // spike 移除（NotificationCenter 不可访问）
        }

        // 对照 WPF: public void UpdateLineNumbersData(MergeConflictView mergeConflictView)
        public void UpdateLineNumbersData(MergeConflictView mergeConflictView)
        {
            _lineNumbers.Clear();
            if (mergeConflictView == null)
            {
                _lineNumberLength = 2;
                return;
            }
            int num = 0;
            MergeConflictView.Chunk[] chunks = mergeConflictView.Chunks;
            for (int i = 0; i < chunks.Length; i++)
            {
                MergeConflictView.Line[] lines = chunks[i].Lines;
                foreach (MergeConflictView.Line line in lines)
                {
                    if (!(line.Node is MergeConflict.EmptyLine) || mergeConflictView.ViewMode == MergeConflictPart.Merged)
                    {
                        num++;
                        _lineNumbers[line.LineNumber] = num;
                    }
                }
            }
            int num2 = Math.Max(2, num.ToString().Length);
            if (num2 != _lineNumberLength)
            {
                _lineNumberLength = num2;
                InvalidateMeasure();
            }
        }

        // 对照 WPF: protected override Size MeasureOverride(Size availableSize)
        protected override Size MeasureOverride(Size availableSize)
        {
            return base.MeasureOverride(availableSize);
        }

        // 对照 WPF: protected override void OnRender(DrawingContext drawingContext)
        // Avalonia: public override void Render(DrawingContext drawingContext)
        public override void Render(DrawingContext drawingContext)
        {
            // 对照 WPF: base.OnRender(drawingContext);
            base.Render(drawingContext);

            // 对照 WPF: 遍历 VisualLines 画行号 + chevron
            // spike 阶段：行号由基类 LineNumberMargin 渲染，chevron 留 phase 3.9b
            // Phase 3.9b 在此补：
            //   - 遍历 TextView.VisualLines
            //   - 若 ViewMode == Local/Remote：
            //     * IsLineSelected → DrawGeometry(MergeConflictSelectedBrush, chevron)
            //     * _mouseOverLine → DrawGeometry(_mergeConflictMouseOverBrush, chevron)
            //   - _lineNumbers.TryGetValue(lineNumber, out var value) → DrawText(value)
            //   - DrawLine(_separatorPen, 右侧分隔线)

            // spike 版：仅画右侧分隔线
            double x = Bounds.Width - 2.0;
            drawingContext.DrawLine(_separatorPen, new Point(x, 0.0), new Point(x, Bounds.Height));
        }

        // 对照 WPF: protected override void OnMouseLeave(MouseEventArgs e)
        // Avalonia: protected override void OnPointerExited(PointerEventArgs e)
        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            _mouseOverLine = -1;
            InvalidateVisual();
        }

        // 对照 WPF: protected override void OnMouseMove(MouseEventArgs e)
        // Avalonia: protected override void OnPointerMoved(PointerEventArgs e)
        // spike 版：保留方法签名 + 空实现（行悬停高亮留 phase 3.9b）
        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            // Phase 3.9b 在此补：
            //   - e.GetPosition(TextView) 取鼠标坐标
            //   - TextView.GetVisualLineFromVisualTop(position.Y + VerticalOffset) 取 VisualLine
            //   - 更新 _mouseOverLine + InvalidateVisual
        }

        // 对照 WPF: protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        // Avalonia: protected override void OnPointerPressed(PointerPressedEventArgs e)
        // spike 版：保留方法签名 + 空实现（行点击选择留 phase 3.9b）
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            // Phase 3.9b 在此补：
            //   - e.GetPosition(TextView) 取鼠标坐标
            //   - GetVisualLineFromVisualTop 取行号
            //   - IsLineSelected → OnMergeLineRemoved / else → OnMergeLineAdded
            //   - InvalidateVisual
        }

        // 对照 WPF: private bool IsLineSelected(int lineNumber)
        private bool IsLineSelected(int lineNumber)
        {
            MergeConflictView mergeConflictView = _editor.MergeConflictView;
            if (mergeConflictView == null)
            {
                return false;
            }
            MergeConflictView.Chunk[] chunks = mergeConflictView.Chunks;
            foreach (MergeConflictView.Chunk chunk in chunks)
            {
                if (!chunk.LineRange.Contains(lineNumber) || !(chunk.Node is MergeConflict.ConflictChunk))
                {
                    continue;
                }
                MergeConflictView.Line[] lines = chunk.Lines;
                foreach (MergeConflictView.Line line in lines)
                {
                    if (line.LineNumber > lineNumber)
                    {
                        break;
                    }
                    if (line.LineNumber == lineNumber && line.Node is MergeConflict.SelectableLine selectableLine)
                    {
                        return selectableLine.IsSelected;
                    }
                }
                return false;
            }
            return false;
        }

        // 对照 WPF: private bool IsLineSelectable(int lineNumber)
        private bool IsLineSelectable(int lineNumber)
        {
            MergeConflictView mergeConflictView = _editor.MergeConflictView;
            if (mergeConflictView == null)
            {
                return false;
            }
            MergeConflictView.Chunk[] chunks = mergeConflictView.Chunks;
            foreach (MergeConflictView.Chunk chunk in chunks)
            {
                if (!chunk.LineRange.Contains(lineNumber))
                {
                    continue;
                }
                MergeConflictView.Line[] lines = chunk.Lines;
                foreach (MergeConflictView.Line line in lines)
                {
                    if (line.LineNumber > lineNumber)
                    {
                        break;
                    }
                    if (line.LineNumber == lineNumber && line.Node is MergeConflict.SelectableLine
                        && line.Node.ParentChunk is MergeConflict.ConflictChunk)
                    {
                        return true;
                    }
                }
                return false;
            }
            return false;
        }

        // 对照 WPF: private void ApplicationThemeChanged(object sender, EventArgs<ThemeType> e)
        protected void ApplicationThemeChanged(object sender, System.EventArgs e)
        {
            RefreshPen();
        }

        // 对照 WPF: private void RefreshPen()
        private void RefreshPen()
        {
            // 优先读资源（CustomColorsDialog 覆盖或主题字典），取不到回退到 light/dark 静态画刷。
            Color? sepColor = TryFindColor("LineNumber.SeparatorColor");
            _separatorPen = sepColor.HasValue
                ? new Pen(new SolidColorBrush(sepColor.Value), 1.0)
                : (ForkPlusSettings.Default.Theme.IsDarkBase() ? SeparatorPenDark : SeparatorPenLight);
            _mergeConflictMouseOverBrush = TryFindColorBrush("MergeConflict.MouseOverColor")
                ?? (ForkPlusSettings.Default.Theme.IsDarkBase() ? MergeConflictMouseOverBrushDark : MergeConflictMouseOverBrushLight);
        }

        private static Color? TryFindColor(string key)
        {
            var app = global::Avalonia.Application.Current;
            if (app != null && app.TryGetResource(key, null, out var res))
            {
                if (res is Color c) return c;
                if (res is ISolidColorBrush b) return b.Color;
            }
            return null;
        }

        private static IBrush TryFindColorBrush(string key)
        {
            Color? c = TryFindColor(key);
            return c.HasValue ? new SolidColorBrush(c.Value) : null;
        }
    }
}
