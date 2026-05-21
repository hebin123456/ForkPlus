using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace ForkPlus.UI
{
	public class TextDrawer
	{
		private readonly GlyphTypeface _glyphTypeface;

		private readonly double _emSize;

		private readonly double _pixelsPerDip;

		[Null]
		private readonly Pen _debugPen;

		private Dictionary<ushort, double> _glyphWidthsCache = new Dictionary<ushort, double>();

		public TextDrawer(Typeface typeface, double emSize, double pixelsPerDip, Brush debugBrush = null)
		{
			if (!typeface.TryGetGlyphTypeface(out _glyphTypeface))
			{
				throw new InvalidOperationException("No glyphTypeFace found");
			}
			_emSize = emSize;
			_pixelsPerDip = pixelsPerDip;
			if (debugBrush != null)
			{
				_debugPen = new Pen(debugBrush, 1.0);
				_debugPen.Freeze();
			}
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
			double num = 0.0;
			for (int i = 0; i < text.Length; i++)
			{
				int valueOrDefault = ReadCodePoint(text, ref i).GetValueOrDefault(63);
				if (!_glyphTypeface.CharacterToGlyphMap.TryGetValue(valueOrDefault, out var value))
				{
					value = _glyphTypeface.CharacterToGlyphMap[63];
				}
				if (!_glyphWidthsCache.TryGetValue(value, out var value2))
				{
					value2 = _glyphTypeface.AdvanceWidths[value] * _emSize;
					_glyphWidthsCache.Add(value, value2);
				}
				list.Add(value);
				list2.Add(value2);
				num += value2;
				if (trimming && num > rect.Width)
				{
					double num2 = _glyphTypeface.AdvanceWidths[46] + 2.0;
					ushort item = _glyphTypeface.CharacterToGlyphMap[46];
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
			GlyphRun glyphRun = new GlyphRun(_glyphTypeface, 0, isSideways: false, _emSize, (float)_pixelsPerDip, list, baselineOrigin, list2, null, null, null, null, null, null);
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
