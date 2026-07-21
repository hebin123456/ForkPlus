using System.Collections.Generic;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace ForkPlus.Avalonia.Controls.Editor.Hex
{
    // Phase 2.9：Avalonia 版 HexColorizer（从 WPF 工程迁移）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/Editor/Hex/HexColorizer.cs（128 行）：
    //   - internal class HexColorizer : DocumentColorizingTransformer
    //   - 给 hex 文本三列上色：
    //     offset 列：灰色
    //     hex 列：蓝色（可打印字节）/ 暗红色（不可打印字节，如 00/FF）
    //     ascii 列：绿色
    //   - 支持 Hex Diff 视图：SetHighlightedBytes 标记的字节索引套上橙黄背景
    //   - 依赖 AvalonEdit API：DocumentColorizingTransformer / DocumentLine / ChangeLinePart /
    //     VisualLineElement.TextRunProperties.SetForegroundBrush / SetBackgroundBrush
    //
    // Avalonia 版差异：
    //   1. 基类改为 AvaloniaEdit.Rendering.DocumentColorizingTransformer（API 与 AvalonEdit 一致）
    //   2. Brush / SolidColorBrush / Color 改用 Avalonia.Media（不用 System.Windows.Media）
    //   3. 删除 Brush.Freeze() 调用（Avalonia Brush 本身 immutable，无需 Freeze）
    //   4. namespace 改为 ForkPlus.Avalonia.Controls.Editor.Hex
    //
    // 本 spike 版完整迁移（保留所有着色逻辑），因为 HexColorizer 是 HexEditor 的核心视觉层。
    internal class HexColorizer : DocumentColorizingTransformer
    {
        private readonly HexEditor _editor;
        private HashSet<int> _highlightedBytes;

        // 颜色（用静态字段避免每行 new Brush）
        // Avalonia Brush 本身 immutable，无需 WPF 的 Freeze()
        private static readonly IBrush OffsetBrush = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));
        private static readonly IBrush HexBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x60, 0xB0));
        private static readonly IBrush HexNonPrintableBrush = new SolidColorBrush(Color.FromRgb(0xB0, 0x40, 0x40));
        private static readonly IBrush AsciiBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x80, 0x30));
        // 差异字节背景色（橙黄）
        private static readonly IBrush DiffBackgroundBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00));

        public HexColorizer(HexEditor editor)
        {
            _editor = editor;
        }

        /// <summary>设置需要高亮背景的字节索引集合（Hex Diff 视图用）。</summary>
        public void SetHighlightedBytes(HashSet<int> byteIndices)
        {
            _highlightedBytes = byteIndices;
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            if (_editor == null) return;
            int bytesPerRow = _editor.BytesPerRow;
            bool showOffset = _editor.ShowOffset;
            bool showAscii = _editor.ShowAscii;
            int lineNo = line.LineNumber - 1; // 0-based

            int offsetWidth = showOffset ? 8 + 2 : 0; // "XXXXXXXX  "
            int half = bytesPerRow / 2;
            int hexWidth = bytesPerRow * 3 - 1 + (half > 0 ? 1 : 0);
            int asciiStart = offsetWidth + hexWidth + 2;

            int lineOffset = line.Offset;
            int lineLength = line.Length;

            // offset 列
            if (showOffset && lineLength >= offsetWidth)
            {
                ChangeLinePart(lineOffset, lineOffset + System.Math.Min(offsetWidth, lineLength), v =>
                {
                    v.TextRunProperties.SetForegroundBrush(OffsetBrush);
                });
            }

            // hex 列：逐字节上色 + 差异字节背景
            int fileByteStart = lineNo * bytesPerRow;
            byte[] bytes = _editor.GetBytes();
            HashSet<int> highlighted = _highlightedBytes;
            for (int i = 0; i < bytesPerRow; i++)
            {
                int byteIdx = fileByteStart + i;
                if (byteIdx >= (bytes?.Length ?? 0)) break;
                int colStart = offsetWidth + i * 3 + (i >= half && half > 0 ? 1 : 0);
                int colEnd = colStart + 2;
                if (colEnd > lineLength) break;
                IBrush brush = IsPrintable(bytes[byteIdx]) ? HexBrush : HexNonPrintableBrush;
                bool isDiff = highlighted != null && highlighted.Contains(byteIdx);
                int absStart = lineOffset + colStart;
                int absEnd = lineOffset + colEnd;
                ChangeLinePart(absStart, absEnd, v =>
                {
                    v.TextRunProperties.SetForegroundBrush(brush);
                    if (isDiff)
                    {
                        v.TextRunProperties.SetBackgroundBrush(DiffBackgroundBrush);
                    }
                });

                // ascii 列对应字符也加背景
                if (showAscii && isDiff && lineLength >= asciiStart + i + 1)
                {
                    int asciiCharStart = lineOffset + asciiStart + i;
                    ChangeLinePart(asciiCharStart, asciiCharStart + 1, v =>
                    {
                        v.TextRunProperties.SetBackgroundBrush(DiffBackgroundBrush);
                    });
                }
            }

            // ascii 列前景上色
            if (showAscii && lineLength >= asciiStart)
            {
                int asciiEnd = System.Math.Min(lineOffset + lineLength, lineOffset + asciiStart + bytesPerRow);
                if (asciiEnd > lineOffset + asciiStart)
                {
                    ChangeLinePart(lineOffset + asciiStart, asciiEnd, v =>
                    {
                        v.TextRunProperties.SetForegroundBrush(AsciiBrush);
                    });
                }
            }
        }

        private static bool IsPrintable(byte b)
        {
            return b >= 0x20 && b < 0x7F;
        }
    }
}
