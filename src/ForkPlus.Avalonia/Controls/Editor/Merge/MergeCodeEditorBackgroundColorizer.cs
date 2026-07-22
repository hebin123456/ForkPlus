using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using ForkPlus.Git.Merge;
using ForkPlus.Git.Merge.Presentation;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Controls.Editor.Merge
{
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/Editor/Merge/MergeCodeEditorBackgroundColorizer.cs（141 行）：
    //   - public class MergeCodeEditorBackgroundColorizer : IBackgroundRenderer
    //   - Layer => KnownLayer.Background
    //   - Draw：遍历 MergeConflictView.Chunks，对可视范围内的行按类型画背景矩形：
    //     ConflictChunk + SelectableLine.Remote → MergeRemote brush
    //     ConflictChunk + SelectableLine.Local → MergeLocal brush
    //     非 ConflictChunk + ChangeType.Add → MergeAdd brush
    //     非 ConflictChunk + ChangeType.Remove → MergeRemove brush
    //     AlignmentLines + ViewMode.Merged + 空 ConflictChunk → MergeUnresolved brush
    //     AlignmentLines 其他 → Alignment brush
    //   - DrawRectangle：用 LineSegment 包装 Line.Range → BackgroundGeometryBuilder.GetRectsForSegment
    //     → DrawRectangle(brush, null, rect)
    //   - LineSegment：私有 readonly struct，实现 ISegment（Offset=Range.Start, Length=Range.Length-1）
    //
    // Avalonia 版差异：
    //   1. WPF ICSharpCode.AvalonEdit.Rendering.IBackgroundRenderer →
    //      AvaloniaEdit.Rendering.IBackgroundRenderer（API 一致：Layer + Draw）
    //   2. WPF System.Windows.Media.Brush → Avalonia.Media.IBrush
    //   3. WPF textView.ActualWidth + textView.HorizontalOffset →
    //      Avalonia textView.Bounds.Width + textView.HorizontalOffset
    //   4. WPF DrawingContext.DrawRectangle(brush, pen, rect) → Avalonia 同名 API（参数顺序一致）
    //   5. WPF BackgroundGeometryBuilder.GetRectsForSegment → AvaloniaEdit 同名 API
    //   6. HighlightingType.GetHighlightBrush → 本工程 HighlightingTypeExtensions（同名扩展方法）
    //   7. ISegment / LineSegment / DocumentLine → AvaloniaEdit.Document（API 一致）
    //   8. namespace 改为 ForkPlus.Avalonia.Controls.Editor.Merge
    //   9. brush.Freeze() → 删除（Avalonia Brush immutable）
    public class MergeCodeEditorBackgroundColorizer : IBackgroundRenderer
    {
        private readonly MergeCodeEditor _editor;

        public KnownLayer Layer => KnownLayer.Background;

        public MergeCodeEditorBackgroundColorizer(MergeCodeEditor editor)
        {
            _editor = editor;
        }

        // 对照 WPF: public void Draw(TextView textView, DrawingContext drawingContext)
        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            MergeConflictView mergeConflictView = _editor.MergeConflictView;
            if (mergeConflictView == null || !textView.VisualLinesValid)
            {
                return;
            }
            ReadOnlyCollection<VisualLine> visualLines = textView.VisualLines;
            if (visualLines.Count <= 0)
            {
                return;
            }
            int offset = visualLines[0].FirstDocumentLine.Offset;
            int endOffset = visualLines[visualLines.Count - 1].LastDocumentLine.EndOffset;
            Range other = new Range(offset, endOffset);
            MergeConflictView.Chunk[] chunks = mergeConflictView.Chunks;
            foreach (MergeConflictView.Chunk chunk in chunks)
            {
                if (!chunk.Range.Overlaps(other))
                {
                    continue;
                }
                MergeConflictView.Line[] lines;
                if (chunk.Node is MergeConflict.ConflictChunk)
                {
                    lines = chunk.Lines;
                    foreach (MergeConflictView.Line line in lines)
                    {
                        if (line.Range.Overlaps(other) && line.Node is MergeConflict.SelectableLine selectableLine)
                        {
                            if (selectableLine.ViewMode == MergeConflictPart.Remote)
                            {
                                DrawRectangle(drawingContext, textView, line,
                                    HighlightingType.MergeRemote.GetHighlightBrush(_editor.Theme));
                            }
                            else if (selectableLine.ViewMode == MergeConflictPart.Local)
                            {
                                DrawRectangle(drawingContext, textView, line,
                                    HighlightingType.MergeLocal.GetHighlightBrush(_editor.Theme));
                            }
                        }
                    }
                }
                else
                {
                    lines = chunk.Lines;
                    foreach (MergeConflictView.Line line2 in lines)
                    {
                        if (!line2.Range.Overlaps(other))
                        {
                            continue;
                        }
                        MergeConflict.Line node = line2.Node;
                        if (node != null)
                        {
                            if (node.ChangeType == ContextType.Add)
                            {
                                DrawRectangle(drawingContext, textView, line2,
                                    HighlightingType.MergeAdd.GetHighlightBrush(_editor.Theme));
                            }
                            else if (node.ChangeType == ContextType.Remove)
                            {
                                DrawRectangle(drawingContext, textView, line2,
                                    HighlightingType.MergeRemove.GetHighlightBrush(_editor.Theme));
                            }
                        }
                    }
                }
                lines = chunk.AlignmentLines;
                foreach (MergeConflictView.Line line3 in lines)
                {
                    if (line3.Range.Overlaps(other))
                    {
                        if (_editor.ViewMode == MergeConflictPart.Merged && chunk.Node is MergeConflict.ConflictChunk && chunk.Lines.Length == 0)
                        {
                            DrawRectangle(drawingContext, textView, line3,
                                HighlightingType.MergeUnresolved.GetHighlightBrush(_editor.Theme));
                        }
                        else
                        {
                            DrawRectangle(drawingContext, textView, line3,
                                HighlightingType.Alignment.GetHighlightBrush(_editor.Theme));
                        }
                    }
                }
            }
        }

        // 对照 WPF: private static void DrawRectangle(DrawingContext context, TextView textView,
        //           MergeConflictView.Line line, Brush brush)
        private static void DrawRectangle(DrawingContext context, TextView textView, MergeConflictView.Line line, IBrush brush)
        {
            // 原 WPF 工程中 MergeConflictView.Line : ISegment（显式实现 Offset/Length/EndOffset），
            // Phase 0.2c 把 MergeConflictView.cs 迁到 Core 后去掉 ISegment 实现（Core 不能引用 AvalonEdit）。
            // 这里用 LineSegment 包装 Line.Range，按原公式还原 ISegment 三元组：
            //   Offset    = Range.Start
            //   Length    = Range.Length - 1  （减 1 去掉末尾换行符）
            //   EndOffset = Range.End - 1
            LineSegment segment = new LineSegment(line);
            foreach (Rect item in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment, true))
            {
                Rect rectangle = new Rect(item.X, item.Y, textView.Bounds.Width + textView.HorizontalOffset, item.Height);
                context.DrawRectangle(brush, null, rectangle);
            }
        }

        /// <summary>
        /// 把 MergeConflictView.Line 的 Range 包装成 AvalonEdit ISegment。
        /// 替代原 MergeConflictView.Line : ISegment 的显式实现（迁 Core 后已移除）。
        /// </summary>
        private readonly struct LineSegment : ISegment
        {
            private readonly int _offset;
            private readonly int _length;

            public LineSegment(MergeConflictView.Line line)
            {
                _offset = line.Range.Start;
                _length = line.Range.Length - 1;
            }

            public int Offset => _offset;
            public int Length => _length;
            public int EndOffset => _offset + _length;
        }
    }
}
