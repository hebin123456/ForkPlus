using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.Settings;
using ICSharpCode.AvalonEdit.Rendering;

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

		private static readonly Brush _lightBrush;

		private static readonly Brush _darkBrush;

		private static readonly Pen _separatorPenLight;

		private static readonly Pen _separatorPenDark;

		private static readonly double HorizontalMargin;

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
			_typeface = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal, new FontFamily("Courier New"));
			_lightBrush = new SolidColorBrush(Color.FromRgb(192, 192, 192));
			_darkBrush = new SolidColorBrush(Color.FromRgb(160, 160, 160));
			_separatorPenLight = new Pen(new SolidColorBrush(Color.FromRgb(218, 218, 215)), 1.0);
			_separatorPenDark = new Pen(new SolidColorBrush(Color.FromRgb(110, 110, 110)), 1.0);
			HorizontalMargin = 7.0;
			_lightBrush.Freeze();
			_darkBrush.Freeze();
			_separatorPenLight.Freeze();
			_separatorPenDark.Freeze();
		}

		public DiffLineNumberMargin(DiffViewMode diffViewMode)
		{
			typeface = _typeface;
			emSize = 11.0;
			RefreshBrushes();
			_minusText = new FormattedText("-", CultureInfo.InvariantCulture, FlowDirection.RightToLeft, _typeface, 15.0, _brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
			_plusText = new FormattedText("+", CultureInfo.InvariantCulture, FlowDirection.RightToLeft, _typeface, 13.0, _brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
			_diffViewMode = diffViewMode;
			_showDiffMarks = ForkPlusSettings.Default.DiffShowChangeMarks;
			WeakEventManager<NotificationCenter, EventArgs<ThemeType>>.AddHandler(NotificationCenter.Current, "ApplicationThemeChanged", ApplicationThemeChanged);
			WeakEventManager<NotificationCenter, EventArgs<bool>>.AddHandler(NotificationCenter.Current, "DiffShowChangeMarksChanged", DiffShowChangeMarksChanged);
			RenderOptions.SetClearTypeHint(this, ClearTypeHint.Enabled);
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
				return new Size(CreateFormattedText(new string('9', _lineNumberLength * 2)).Width + HorizontalMargin * 3.0 + DiffMarksColumnWidth, 0.0);
			}
			return new Size(CreateFormattedText(new string('9', _lineNumberLength)).Width + HorizontalMargin * 2.0 + DiffMarksColumnWidth, 0.0);
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);
			if (_diffViewMode == DiffViewMode.Split)
			{
				foreach (ICSharpCode.AvalonEdit.Rendering.VisualLine visualLine in base.TextView.VisualLines)
				{
					if (!_lineNumbers.TryGetValue(visualLine.FirstDocumentLine.LineNumber - 1, out var value))
					{
						continue;
					}
					int? from = value.From;
					if (from.HasValue)
					{
						drawingContext.DrawText(CreateFormattedText(from.GetValueOrDefault().ToString()), new Point((base.RenderSize.Width - HorizontalMargin - DiffMarksColumnWidth) / 2.0, visualLine.VisualTop - base.TextView.VerticalOffset));
						if (_showDiffMarks && !value.To.HasValue)
						{
							drawingContext.DrawText(_minusText, new Point(base.RenderSize.Width - 1.0, visualLine.VisualTop - 2.0 - base.TextView.VerticalOffset));
						}
					}
					from = value.To;
					if (from.HasValue)
					{
						drawingContext.DrawText(CreateFormattedText(from.GetValueOrDefault().ToString()), new Point(base.RenderSize.Width - HorizontalMargin - DiffMarksColumnWidth, visualLine.VisualTop - base.TextView.VerticalOffset));
						if (_showDiffMarks && !value.From.HasValue)
						{
							drawingContext.DrawText(_plusText, new Point(base.RenderSize.Width - 1.0, visualLine.VisualTop - 2.0 - base.TextView.VerticalOffset));
						}
					}
				}
			}
			else if (_diffViewMode == DiffViewMode.SideBySideOld)
			{
				foreach (ICSharpCode.AvalonEdit.Rendering.VisualLine visualLine2 in base.TextView.VisualLines)
				{
					if (!_lineNumbers.TryGetValue(visualLine2.FirstDocumentLine.LineNumber - 1, out var value2))
					{
						continue;
					}
					int? from = value2.From;
					if (from.HasValue)
					{
						drawingContext.DrawText(CreateFormattedText(from.GetValueOrDefault().ToString()), new Point(base.RenderSize.Width - HorizontalMargin - DiffMarksColumnWidth, visualLine2.VisualTop - base.TextView.VerticalOffset));
						if (_showDiffMarks && !value2.To.HasValue)
						{
							drawingContext.DrawText(_minusText, new Point(base.RenderSize.Width - 1.0, visualLine2.VisualTop - 2.0 - base.TextView.VerticalOffset));
						}
					}
				}
			}
			else if (_diffViewMode == DiffViewMode.SideBySideNew)
			{
				foreach (ICSharpCode.AvalonEdit.Rendering.VisualLine visualLine3 in base.TextView.VisualLines)
				{
					if (!_lineNumbers.TryGetValue(visualLine3.FirstDocumentLine.LineNumber - 1, out var value3))
					{
						continue;
					}
					int? from = value3.To;
					if (from.HasValue)
					{
						drawingContext.DrawText(CreateFormattedText(from.GetValueOrDefault().ToString()), new Point(base.RenderSize.Width - HorizontalMargin - DiffMarksColumnWidth, visualLine3.VisualTop - base.TextView.VerticalOffset));
						if (_showDiffMarks && !value3.From.HasValue)
						{
							drawingContext.DrawText(_plusText, new Point(base.RenderSize.Width - 1.0, visualLine3.VisualTop - 2.0 - base.TextView.VerticalOffset));
						}
					}
				}
				drawingContext.DrawLine(_separatorPen, new Point(0.0, 0.0), new Point(0.0, base.RenderSize.Height));
			}
			drawingContext.DrawLine(_separatorPen, new Point(base.RenderSize.Width - DiffMarksColumnWidth - 2.0, 0.0), new Point(base.RenderSize.Width - DiffMarksColumnWidth - 2.0, base.RenderSize.Height));
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
			return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.RightToLeft, typeface, emSize, _brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
		}
	}
}
