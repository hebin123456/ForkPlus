using System;
using ForkPlus.UI.WpfCompat;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.Settings;
using AvaloniaEdit.Rendering;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls.Editor.Diff
{
	internal class DiffLineNumberMargin : ClearTypeLineNumberMargin
	{
		private struct LineNumber
		{
			public int? From;

			public int? To;

			public LineNumber(int? from, int? to)
			{
				From = from;
				To = to;
			}
		}

		private static readonly Typeface _typeface;

		// Migration note：WPF 原基类（AvalonEdit TextEditorMargin 系）的 typeface/emSize 字段在此补声明
		private Typeface typeface = _typeface;

		private double emSize = 11.0;

		private static readonly Brush _lightBrush;

		private static readonly Brush _darkBrush;

		private static readonly Pen _separatorPenLight;

		private static readonly Pen _separatorPenDark;

		private static readonly double HorizontalMargin;

		private static readonly double ExtraMeasurePadding;

		private readonly FormattedText _minusText;

		private readonly FormattedText _plusText;

		private readonly DiffViewMode _diffViewMode;

		private Brush _brush;

		private Pen _separatorPen;

		private int _lineNumberLength = 2;

		private Dictionary<int, LineNumber> _lineNumbers = new Dictionary<int, LineNumber>();

		private bool _showDiffMarks;

		private double DiffMarksColumnWidth
		{
			get
			{
				if (!_showDiffMarks)
				{
					return 0.0;
				}
				return 8.0;
			}
		}

		static DiffLineNumberMargin()
		{
			// v3.12 修复（行号/±标记被代码区遮挡）：WPF 原版 Typeface 第 5 参指定 fallback
			// FontFamily("Courier New")，迁移时丢失。Avalonia 无该构造重载，用内联 fallback
			// 列表等价表达：非 Windows 平台无 Consolas 时回退到等宽字体（Courier New →
			// monospace），避免回退到比例字体导致行号实际宽度与测量宽度（'9'×N）不一致、
			// 右缘被代码区遮住一点。
			_typeface = new Typeface(new FontFamily("Consolas, Courier New, monospace"), FontStyles.Normal, FontWeights.Normal);
			_lightBrush = new SolidColorBrush(Color.FromRgb(192, 192, 192));
			_darkBrush = new SolidColorBrush(Color.FromRgb(160, 160, 160));
			_separatorPenLight = new Pen(new SolidColorBrush(Color.FromRgb(218, 218, 215)), 1.0);
			_separatorPenDark = new Pen(new SolidColorBrush(Color.FromRgb(110, 110, 110)), 1.0);
			HorizontalMargin = 7.0;
			ExtraMeasurePadding = 6.0;
		}

		public DiffLineNumberMargin(DiffViewMode diffViewMode)
		{
			// Migration note：WPF 原基类的 typeface/emSize 字段在此补声明
			this.typeface = _typeface;
			emSize = 11.0;
			RefreshBrushes();
			_minusText = new FormattedText("-", CultureInfo.InvariantCulture, FlowDirection.RightToLeft, _typeface, 15.0, _brush);
			_plusText = new FormattedText("+", CultureInfo.InvariantCulture, FlowDirection.RightToLeft, _typeface, 13.0, _brush);
			_diffViewMode = diffViewMode;
			_showDiffMarks = ForkPlusSettings.Default.DiffShowChangeMarks;
			WeakEventManager<NotificationCenter, EventArgs<ThemeType>>.AddHandler(NotificationCenter.Current, "ApplicationThemeChanged", ApplicationThemeChanged);
			WeakEventManager<NotificationCenter, EventArgs<bool>>.AddHandler(NotificationCenter.Current, "DiffShowChangeMarksChanged", DiffShowChangeMarksChanged);
			RenderOptionsShim.SetClearTypeHint(this, ClearTypeHint.Enabled);
		}

		public void UpdateLineNumbersData([Null] VisualPatch visualPatch)
		{
			_lineNumbers.Clear();
			if (visualPatch == null)
			{
				_lineNumberLength = 2;
				return;
			}
			int val = 0;
			VisualChunk[] visualChunks = visualPatch.VisualDiff.VisualChunks;
			foreach (VisualChunk obj in visualChunks)
			{
				int num = obj.Node.FromStart;
				int num2 = obj.Node.ToStart;
				ForkPlus.Git.Diff.Presentation.VisualLine[] visualLines = obj.VisualLines;
				foreach (ForkPlus.Git.Diff.Presentation.VisualLine visualLine in visualLines)
				{
					switch (visualLine.Type)
					{
					case LineType.Context:
						_lineNumbers[visualLine.LineNumber] = new LineNumber(num, num2);
						num++;
						num2++;
						break;
					case LineType.Deleted:
						_lineNumbers[visualLine.LineNumber] = new LineNumber(num, null);
						num++;
						break;
					case LineType.Added:
						_lineNumbers[visualLine.LineNumber] = new LineNumber(null, num2);
						num2++;
						break;
					}
				}
				val = Math.Max(num, val);
				val = Math.Max(num2, val);
			}
			int num3 = Math.Max(2, val.ToString().Length);
			if (num3 != _lineNumberLength)
			{
				_lineNumberLength = num3;
				InvalidateMeasure();
			}
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			if (_diffViewMode == DiffViewMode.Split)
			{
				return new Size(CreateFormattedText(new string('9', _lineNumberLength * 2)).Width + HorizontalMargin * 3.0 + DiffMarksColumnWidth + ExtraMeasurePadding, 0.0);
			}
			return new Size(CreateFormattedText(new string('9', _lineNumberLength)).Width + HorizontalMargin * 2.0 + DiffMarksColumnWidth + ExtraMeasurePadding, 0.0);
		}

		public override void Render(DrawingContext drawingContext)
		{
			base.Render(drawingContext);
			if (_diffViewMode == DiffViewMode.Split)
			{
				foreach (global::AvaloniaEdit.Rendering.VisualLine visualLine in base.TextView.VisualLines)
				{
					if (!_lineNumbers.TryGetValue(visualLine.FirstDocumentLine.LineNumber - 1, out var value))
					{
						continue;
					}
					int? from = value.From;
					if (from.HasValue)
					{
						DrawRightAlignedText(drawingContext, from.GetValueOrDefault().ToString(), (base.Bounds.Size.Width - HorizontalMargin - DiffMarksColumnWidth) / 2.0, visualLine.VisualTop - base.TextView.ScrollOffset.Y);
						if (_showDiffMarks && !value.To.HasValue)
						{
							// v3.12 修复：WPF 的 RTL FormattedText DrawText(origin) 以 origin 为右上角向左绘制，
							// 原坐标 (Width-1) 即"右缘贴 Width-1"；Avalonia 的 origin 恒为左上角，须显式减宽度，
							// 否则 ±标记溢出 margin 右边界 ~7px 被代码区遮住。
							drawingContext.DrawText(_minusText, new Point(base.Bounds.Size.Width - 1.0 - _minusText.Width, visualLine.VisualTop - 2.0 - base.TextView.ScrollOffset.Y));
						}
					}
					from = value.To;
					if (from.HasValue)
					{
						DrawRightAlignedText(drawingContext, from.GetValueOrDefault().ToString(), base.Bounds.Size.Width - HorizontalMargin - DiffMarksColumnWidth, visualLine.VisualTop - base.TextView.ScrollOffset.Y);
						if (_showDiffMarks && !value.From.HasValue)
						{
							drawingContext.DrawText(_plusText, new Point(base.Bounds.Size.Width - 1.0 - _plusText.Width, visualLine.VisualTop - 2.0 - base.TextView.ScrollOffset.Y));
						}
					}
				}
			}
			else if (_diffViewMode == DiffViewMode.SideBySideOld)
			{
				foreach (global::AvaloniaEdit.Rendering.VisualLine visualLine2 in base.TextView.VisualLines)
				{
					if (!_lineNumbers.TryGetValue(visualLine2.FirstDocumentLine.LineNumber - 1, out var value2))
					{
						continue;
					}
					int? from = value2.From;
					if (from.HasValue)
					{
						DrawRightAlignedText(drawingContext, from.GetValueOrDefault().ToString(), base.Bounds.Size.Width - HorizontalMargin - DiffMarksColumnWidth, visualLine2.VisualTop - base.TextView.ScrollOffset.Y);
						if (_showDiffMarks && !value2.To.HasValue)
						{
							drawingContext.DrawText(_minusText, new Point(base.Bounds.Size.Width - 1.0 - _minusText.Width, visualLine2.VisualTop - 2.0 - base.TextView.ScrollOffset.Y));
						}
					}
				}
			}
			else if (_diffViewMode == DiffViewMode.SideBySideNew)
			{
				foreach (global::AvaloniaEdit.Rendering.VisualLine visualLine3 in base.TextView.VisualLines)
				{
					if (!_lineNumbers.TryGetValue(visualLine3.FirstDocumentLine.LineNumber - 1, out var value3))
					{
						continue;
					}
					int? from = value3.To;
					if (from.HasValue)
					{
						DrawRightAlignedText(drawingContext, from.GetValueOrDefault().ToString(), base.Bounds.Size.Width - HorizontalMargin - DiffMarksColumnWidth, visualLine3.VisualTop - base.TextView.ScrollOffset.Y);
						if (_showDiffMarks && !value3.From.HasValue)
						{
							drawingContext.DrawText(_plusText, new Point(base.Bounds.Size.Width - 1.0 - _plusText.Width, visualLine3.VisualTop - 2.0 - base.TextView.ScrollOffset.Y));
						}
					}
				}
				drawingContext.DrawLine(_separatorPen, new Point(0.0, 0.0), new Point(0.0, base.Bounds.Size.Height));
			}
			drawingContext.DrawLine(_separatorPen, new Point(base.Bounds.Size.Width - DiffMarksColumnWidth - 2.0, 0.0), new Point(base.Bounds.Size.Width - DiffMarksColumnWidth - 2.0, base.Bounds.Size.Height));
		}

		private void DiffShowChangeMarksChanged(object sender, EventArgs<bool> e)
		{
			_showDiffMarks = ForkPlusSettings.Default.DiffShowChangeMarks;
			InvalidateMeasure();
			InvalidateVisual();
		}

		private void ApplicationThemeChanged(object sender, EventArgs<ThemeType> e)
		{
			RefreshBrushes();
		}

		private void RefreshBrushes()
	{
		// 优先读资源（CustomColorsDialog 覆盖或主题字典），取不到回退到 light/dark 静态画刷。
		_brush = TryFindColorBrush("LineNumber.ForegroundColor")
			?? (ForkPlusSettings.Default.Theme.IsDarkBase() ? _darkBrush : _lightBrush);
		Color? sepColor = TryFindColor("LineNumber.SeparatorColor");
		_separatorPen = sepColor.HasValue
			? new Pen(new SolidColorBrush(sepColor.Value), 1.0)
			: (ForkPlusSettings.Default.Theme.IsDarkBase() ? _separatorPenDark : _separatorPenLight);
	}

	private static Color? TryFindColor(string key)
	{
		object res = Application.Current?.TryFindResource(key);
		if (res is Color c) return c;
		if (res is SolidColorBrush b) return b.Color;
		return null;
	}

	private static Brush TryFindColorBrush(string key)
	{
		Color? c = TryFindColor(key);
		return c.HasValue ? new SolidColorBrush(c.Value) : null;
	}

		private FormattedText CreateFormattedText(string text)
		{
			return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.RightToLeft, typeface, emSize, _brush);
		}

		private void DrawRightAlignedText(DrawingContext drawingContext, string text, double right, double top)
		{
			FormattedText formattedText = CreateFormattedText(text);
			drawingContext.DrawText(formattedText, new Point(right - formattedText.Width, top));
		}
	}
}
