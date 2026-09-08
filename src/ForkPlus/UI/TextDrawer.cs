using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI
{
	public class TextDrawer
	{
		private readonly GlyphTypeface _glyphTypeface;

		private readonly double _emSize;

		private readonly double _pixelsPerDip;

		// Migration note：WPF GlyphTypeface.AdvanceWidths 是 em 单位的 advance 表；
		// Avalonia 12 只有 TryGetHorizontalGlyphAdvance（字体设计单位），
		// 这里存 DesignEmHeight 用于"设计单位 / DesignEmHeight = em 单位"换算。
		private readonly double _designEmHeight;

		[Null]
		private readonly Pen _debugPen;

		private Dictionary<ushort, double> _glyphWidthsCache = new Dictionary<ushort, double>();

		public TextDrawer(Typeface typeface, double emSize, double pixelsPerDip, Brush debugBrush = null)
		{
			// Migration note：WPF Typeface.TryGetGlyphTypeface(out glyphTypeface) 在 Avalonia 12 不存在，
			// 直接读 Typeface.GlyphTypeface 属性（解析失败时为 null）。
			_glyphTypeface = typeface.GlyphTypeface;
			if (_glyphTypeface == null)
			{
				throw new InvalidOperationException("No glyphTypeFace found");
			}
			_emSize = emSize;
			// Migration note：Avalonia 12 的 GlyphRun 没有 pixelsPerDip 概念（按控件 DPI 自动处理），
			// 参数保留只为兼容 WPF 调用面。
			_pixelsPerDip = pixelsPerDip;
			_designEmHeight = _glyphTypeface.Metrics.DesignEmHeight;
			if (_designEmHeight <= 0.0)
			{
				_designEmHeight = 1000.0;
			}
			if (debugBrush != null)
			{
				_debugPen = new Pen(debugBrush, 1.0);
			}
		}

		/// <summary>原 WPF GlyphTypeface.AdvanceWidths[glyph]（em 单位 advance）的等价实现。</summary>
		private double GetGlyphAdvanceEm(ushort glyph)
		{
			// Migration note：Avalonia 12 无 AdvanceWidths 表，用 TryGetHorizontalGlyphAdvance（设计单位）
			// 除以 DesignEmHeight 换算回 WPF 的 em 单位语义。
			if (!_glyphTypeface.TryGetHorizontalGlyphAdvance(glyph, out ushort advance))
			{
				return 0.0;
			}
			return (double)advance / _designEmHeight;
		}

		public double DrawText(DrawingContext ctx, string text, Brush brush, Rect rect, TextAlignment alignment = TextAlignment.Left, bool trimming = false)
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
			// Migration note：Avalonia 12 的 GlyphRun 需要 GlyphCluster（字符起始索引），
			// WPF 版由 GlyphRun 内部按 characters 与 glyphIndices 一一对应推导。
			List<int> list3 = new List<int>(text.Length);
			double num = 0.0;
			for (int i = 0; i < text.Length; i++)
			{
				int cluster = i;
				int valueOrDefault = ReadCodePoint(text, ref i).GetValueOrDefault(63);
				// Migration note：WPF CharacterToGlyphMap.TryGetValue(code, out glyph) →
				// Avalonia 的 CharacterToGlyphMap.TryGetGlyph(code, out glyph)。
				if (!_glyphTypeface.CharacterToGlyphMap.TryGetGlyph(valueOrDefault, out var value))
				{
					value = _glyphTypeface.CharacterToGlyphMap.GetGlyph(63);
				}
				if (!_glyphWidthsCache.TryGetValue(value, out var value2))
				{
					value2 = GetGlyphAdvanceEm(value) * _emSize;
					_glyphWidthsCache.Add(value, value2);
				}
				list.Add(value);
				list2.Add(value2);
				list3.Add(cluster);
				num += value2;
				if (trimming && num > rect.Width)
				{
					// Migration note：WPF AdvanceWidths[46]（'.' 的 em advance）→ GetGlyphAdvanceEm(46)。
					double num2 = GetGlyphAdvanceEm(46) + 2.0;
					ushort item = _glyphTypeface.CharacterToGlyphMap.GetGlyph(46);
					while (num + num2 * 3.0 > rect.Width && list.Count > 0)
					{
						list.RemoveAt(list.Count - 1);
						num -= list2[list2.Count - 1];
						list2.RemoveAt(list2.Count - 1);
						list3.RemoveAt(list3.Count - 1);
					}
					if (num + num2 * 3.0 <= rect.Width)
					{
						int dotCluster = (list3.Count > 0) ? list3[list3.Count - 1] : 0;
						list.Add(item);
						list.Add(item);
						list.Add(item);
						list2.Add(num2);
						list2.Add(num2);
						list2.Add(num2);
						list3.Add(dotCluster);
						list3.Add(dotCluster);
						list3.Add(dotCluster);
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
			// Migration note：WPF GlyphRun 14 参构造（bidiLevel / isSideways / pixelsPerDip /
			// 显式 glyphAdvances 列表）在 Avalonia 12 不存在，改用 GlyphInfo 列表构造：
			// GlyphAdvance 承接原 glyphAdvances（DIP 单位），GlyphCluster 承接 code point
			// 起始字符索引（兼容代理对），biDiLevel 固定 0（与原 WPF 传 0 一致）。
			List<global::Avalonia.Media.TextFormatting.GlyphInfo> glyphInfos = new List<global::Avalonia.Media.TextFormatting.GlyphInfo>(list.Count);
			for (int j = 0; j < list.Count; j++)
			{
				glyphInfos.Add(new global::Avalonia.Media.TextFormatting.GlyphInfo(list[j], list3[j], list2[j], default(Vector)));
			}
			GlyphRun glyphRun = new GlyphRun(_glyphTypeface, _emSize, text.AsMemory(), glyphInfos, baselineOrigin, 0);
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
