using System.Globalization;
using Avalonia;
using Avalonia.Media;

// Avalonia spike 版 TextDrawer（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/TextDrawer.cs（128 行）：
//   - WPF: public class TextDrawer
//   - 构造：Typeface + emSize + pixelsPerDip + debugBrush，TryGetGlyphTypeface 取 GlyphTypeface
//   - DrawText(DrawingContext, text, Brush, Rect, TextAlignment, trimming)：
//     逐 codepoint 映射 glyph + 累积 advance width，超宽时手动加 "..." 截断，
//     按 alignment 计算 baseline origin，new GlyphRun(...) + ctx.DrawGlyphRun(brush, run)
//   - ReadCodePoint：UTF-16 代理对解析
//   - 依赖：System.Windows.Media（GlyphTypeface/GlyphRun/Pen/Typeface/DrawingContext/Brush）
//
// Avalonia 版差异（spike 简化策略，task spec：用 FormattedText 或 DrawingContext.DrawText）：
//   1. WPF GlyphTypeface + 手动 GlyphRun → Avalonia FormattedText（封装字形度量与布局）
//   2. WPF ctx.DrawGlyphRun(brush, glyphRun) → Avalonia ctx.DrawText(formattedText, origin)
//      （FormattedText 构造时传入 IBrush，DrawText 不再单独传 brush）
//   3. WPF 手动 "..." 截断 → Avalonia FormattedText.Trimming = CharacterEllipsis +
//      MaxTextWidth = rect.Width（自动省略号）
//   4. WPF alignment 手算 baseline origin → Avalonia FormattedText.TextAlignment（内置水平对齐）
//   5. WPF pixelsPerDip → spike 保留字段（Avalonia FormattedText 内部按 DPI 缩放，spike 不使用）
//   6. WPF debug Pen + DrawRectangle 调试框 → spike 跳过（Avalonia DrawRectangle 无 3 参重载，
//      调试框非关键路径）
//   7. WPF [Null] Attribute → spike 跳过（nullable disable in csproj）
//   8. ReadCodePoint UTF-16 解析 → spike 跳过（FormattedText 自行处理代理对）
//
// spike 简化（task spec 关键 API）：
//   - 构造保留 (Typeface, emSize, pixelsPerDip, debugBrush)
//   - DrawText 用 FormattedText 度量 + 绘制，返回绘制宽度
//   - 对齐 / 截断交由 FormattedText 内置能力
namespace ForkPlus.Avalonia
{
	public class TextDrawer
	{
		private readonly Typeface _typeface;

		private readonly double _emSize;

		private readonly double _pixelsPerDip;

		private readonly IBrush _debugBrush;

		public TextDrawer(Typeface typeface, double emSize, double pixelsPerDip, IBrush debugBrush = null)
		{
			_typeface = typeface;
			_emSize = emSize;
			_pixelsPerDip = pixelsPerDip;
			_debugBrush = debugBrush;
		}

		public double DrawText(DrawingContext ctx, string text, IBrush brush, Rect rect, TextAlignment alignment = TextAlignment.Left, bool trimming = false)
		{
			// spike 版跳过 debug 调试框（WPF 用 Pen + DrawRectangle，Avalonia 无对应 3 参重载）
			if (string.IsNullOrEmpty(text))
			{
				return 0.0;
			}
			FormattedText formattedText = new FormattedText(
				text,
				CultureInfo.CurrentCulture,
				FlowDirection.LeftToRight,
				_typeface,
				_emSize,
				brush);
			formattedText.MaxTextWidth = rect.Width;
			formattedText.TextAlignment = alignment;
			formattedText.Trimming = trimming ? TextTrimming.CharacterEllipsis : TextTrimming.None;
			// 垂直底对齐：origin Y = rect.Bottom - 文本高度（WPF 用 baseline=BottomLeft，spike 近似底对齐）
			double originY = rect.Bottom - formattedText.Height;
			if (originY < rect.Top)
			{
				originY = rect.Top;
			}
			// 水平对齐由 TextAlignment 在 MaxTextWidth 内处理，origin X 取 rect.X
			ctx.DrawText(formattedText, new Point(rect.X, originY));
			return formattedText.Width;
		}
	}
}
