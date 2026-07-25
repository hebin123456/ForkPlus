using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

namespace ForkPlus.UI
{
	// 阶段 4.5：WPF GlyphRun（14 参数构造函数）→ Avalonia GlyphRun（属性初始化或简化构造）。
	// WPF GlyphTypeface.TryGetGlyphTypeface(out) → Avalonia GlyphTypeface 直接构造（无 TryGet 变体）。
	// WPF Typeface.TryGetGlyphTypeface → Avalonia 通过 Typeface.Typeface.GetGlyphTypeface() 获取。
	// WPF Pen.Freeze() → 移除（Avalonia 默认不可变）。
	// WPF DrawingContext.DrawGlyphRun(Brush, GlyphRun) → Avalonia DrawingContext.DrawGlyphRun(IBrush, GlyphRun)。
	// NOTE(4.5-l): Avalonia GlyphRun 构造签名与 WPF 不同，需运行时验证字形渲染效果。
	// WPF GlyphRun 构造参数：glyphTypeface, biDiLevel, isSideways, emSize, pixelsPerDip, glyphIndices,
	//   baselineOrigin, advanceWidths, glyphOffsets, characters, deviceFontName, clusterMap, caretStops,
	//   language。
	// Avalonia GlyphRun 通过属性设置，关键属性：GlyphTypeface, FontRenderingEmSize, GlyphIndices,
	//   GlyphAdvances, GlyphOffsets, BaselineOrigin。
	public class TextDrawer
	{
		// 阶段 5：Avalonia 11.3 仅有 IGlyphTypeface 接口，具体 GlyphTypeface 类在 12.0 才引入。
		// Typeface.GlyphTypeface 返回 IGlyphTypeface；GlyphRun 构造函数接受 IGlyphTypeface。
		private readonly IGlyphTypeface _glyphTypeface;

		private readonly double _emSize;

		private readonly double _pixelsPerDip;

		[Null]
		private readonly Pen _debugPen;

		private Dictionary<ushort, double> _glyphWidthsCache = new Dictionary<ushort, double>();

		public TextDrawer(Typeface typeface, double emSize, double pixelsPerDip, IBrush debugBrush = null)
		{
			// 阶段 4.5：WPF Typeface.TryGetGlyphTypeface(out GlyphTypeface)
			// → Avalonia 通过 Typeface.GlyphTypeface 属性获取（Avalonia 11 中 Typeface 持有 GlyphTypeface）。
			// 如 GlyphTypeface 不可用，抛出与原代码一致的异常。
			_glyphTypeface = typeface.GlyphTypeface;
			if (_glyphTypeface == null)
			{
				throw new InvalidOperationException("No glyphTypeFace found");
			}
			_emSize = emSize;
			_pixelsPerDip = pixelsPerDip;
			if (debugBrush != null)
			{
				_debugPen = new Pen(debugBrush, 1.0);
				// 阶段 4.5：Avalonia Pen 默认不可变，无需 WPF Freeze()。
			}
		}

		public double DrawText(DrawingContext ctx, string text, IBrush brush, Rect rect, TextAlignment alignment = TextAlignment.Left, bool trimming = false)
		{
			if (_debugPen != null)
			{
				ctx.DrawRectangle(null, _debugPen, rect);
			}
			if (string.IsNullOrEmpty(text))
			{
				return 0.0;
			}
			List<ushort> list = new List<ushort>(text.Length);
			List<double> list2 = new List<double>(text.Length);
			double num = 0.0;
			for (int i = 0; i < text.Length; i++)
			{
				int valueOrDefault = ReadCodePoint(text, ref i).GetValueOrDefault(63);
				// 阶段 5：Avalonia 11.3 IGlyphTypeface 无 CharacterToGlyphMap 字典，
				// 改用 TryGetGlyph(uint codepoint, out ushort glyph) / GetGlyph(uint)。
				ushort value;
				if (!_glyphTypeface.TryGetGlyph((uint)valueOrDefault, out value))
				{
					value = _glyphTypeface.GetGlyph(63);
				}
				if (!_glyphWidthsCache.TryGetValue(value, out var value2))
				{
					// 阶段 5：IGlyphTypeface.GetGlyphAdvance(ushort) 返回 int（design units），
					// 乘以 emSize 得到像素宽度（与 WPF AdvanceWidths[value] * emSize 等价）。
					value2 = _glyphTypeface.GetGlyphAdvance(value) * _emSize;
					_glyphWidthsCache.Add(value, value2);
				}
				list.Add(value);
				list2.Add(value2);
				num += value2;
				if (trimming && num > rect.Width)
				{
					double num2 = _glyphTypeface.GetGlyphAdvance(46) + 2.0;
					ushort item = _glyphTypeface.GetGlyph(46);
					while (num + num2 * 3.0 > rect.Width && list.Count > 0)
					{
						list.RemoveAt(list.Count - 1);
						num -= list2[list2.Count - 1];
						list2.RemoveAt(list2.Count - 1);
					}
					if (num + num2 * 3.0 <= rect.Width)
					{
						list.Add(item);
						list.Add(item);
						list.Add(item);
						list2.Add(num2);
						list2.Add(num2);
						list2.Add(num2);
					}
					break;
				}
			}
			if (list.Count == 0)
			{
				return 0.0;
			}
			Point baselineOrigin = rect.BottomLeft;
			if (alignment == TextAlignment.Center && num < rect.Width)
			{
				double num3 = (rect.Width - num) / 2.0;
				baselineOrigin = new Point(rect.X + num3, rect.Bottom);
			}
			// 阶段 5：Avalonia 11.3 GlyphRun 构造签名：
			// (IGlyphTypeface, double fontRenderingEmSize, ReadOnlyMemory<char> characters,
			//  IReadOnlyList<ushort> glyphIndices, Point? baselineOrigin, int biDiLevel)
			// pixelsPerDip 在 Avalonia 中无对应属性（由 RenderOptions 处理 DPI）。
			GlyphRun glyphRun = new GlyphRun(
				_glyphTypeface,
				_emSize,
				text.AsMemory(),
				list,
				(Point?)baselineOrigin,
				0);
			ctx.DrawGlyphRun(brush, glyphRun);
			return num;
		}

		private static int? ReadCodePoint(string text, ref int index)
		{
			ushort num = text[index];
			if (num < 55296)
			{
				return num;
			}
			if (num < 56320)
			{
				if (index + 1 > text.Length)
				{
					return null;
				}
				ushort num2 = num;
				ushort num3 = text[++index];
				if (num3 < 56320 || num3 >= 57344)
				{
					return null;
				}
				return 65536 + (num2 - 55296) * 1024 + (num3 - 56320);
			}
			if (num < 57344)
			{
				return null;
			}
			return num;
		}
	}
}
