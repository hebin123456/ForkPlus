using ForkPlus.Settings;
using ForkPlus.UI;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace ForkPlus.Avalonia.Controls.Editor
{
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/Editor/DiffTextColorizer.cs（24 行）：
    //   - public class DiffTextColorizer : DocumentColorizingTransformer
    //   - HunkHeaderLines：int[]，需高亮的 hunk header 行号集合
    //   - ColorizeLine：若当前行在 HunkHeaderLines 中，对整行设前景色为
    //     HighlightingType.Service.GetHighlightBrush(theme)
    //
    // Avalonia 版差异：
    //   1. 基类 ICSharpCode.AvalonEdit.Rendering.DocumentColorizingTransformer →
    //      AvaloniaEdit.Rendering.DocumentColorizingTransformer（API 一致）
    //   2. AvalonEdit DocumentLine / ChangeLinePart / VisualLineElement → AvaloniaEdit 同名类型
    //   3. ForkPlusSettings.Default.Theme 来自 Core（可访问）
    //   4. HighlightingType.Service.GetHighlightBrush 来自本工程 HighlightingTypeExtensions
    //   5. namespace 改为 ForkPlus.Avalonia.Controls.Editor
    public class DiffTextColorizer : DocumentColorizingTransformer
    {
        public int[] HunkHeaderLines { get; set; }

        protected override void ColorizeLine(DocumentLine line)
        {
            if (HunkHeaderLines != null && !line.IsDeleted && HunkHeaderLines.ContainsItem(line.LineNumber))
            {
                ChangeLinePart(line.Offset, line.EndOffset, HighlightHunkHeader);
            }
        }

        private static void HighlightHunkHeader(VisualLineElement e)
        {
            ThemeType theme = ForkPlusSettings.Default.Theme;
            e.TextRunProperties.SetForegroundBrush(HighlightingType.Service.GetHighlightBrush(theme));
        }
    }
}
