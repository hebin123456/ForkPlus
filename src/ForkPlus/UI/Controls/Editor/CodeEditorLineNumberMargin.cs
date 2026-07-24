using System;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using ForkPlus.Settings;
using AvaloniaEdit.Rendering;

namespace ForkPlus.UI.Controls.Editor
{
	internal class CodeEditorLineNumberMargin : ClearTypeLineNumberMargin
	{
		// 阶段 4 里程碑 4.7-a：WPF Typeface(主字体, 样式, 字重, 字距, 回退字体) →
		// Avalonia Typeface(FontFamily, FontStyle, FontWeight, FontStretch)。Avalonia 用逗号
		// 分隔的 FontFamily 表达回退；Freeze() 移除（Avalonia 画刷默认不可变）。
		private static readonly Typeface _typeface;

		private static readonly IBrush _lightBrush;

		private static readonly IBrush _darkBrush;

		private static readonly Pen _separatorPenLight;

		private static readonly Pen _separatorPenDark;

		private static readonly double HorizontalMargin;

		private IBrush _brush;

		private Pen _separatorPen;

		private int _lineNumberLength = 2;

		static CodeEditorLineNumberMargin()
		{
			_typeface = new Typeface(new FontFamily("Consolas, Courier New"), FontStyle.Normal, FontWeight.Normal, FontStretch.Normal);
			_lightBrush = new SolidColorBrush(Color.FromRgb(192, 192, 192));
			_darkBrush = new SolidColorBrush(Color.FromRgb(160, 160, 160));
			_separatorPenLight = new Pen(new SolidColorBrush(Color.FromRgb(218, 218, 215)), 1.0);
			_separatorPenDark = new Pen(new SolidColorBrush(Color.FromRgb(110, 110, 110)), 1.0);
			HorizontalMargin = 5.0;
		}

		public CodeEditorLineNumberMargin()
		{
			typeface = _typeface;
			emSize = 11.0;
			RefreshBrushes();
			// 阶段 4 里程碑 4.7-a：WeakEventManager → 直接事件订阅。阶段 6 改用 WeakEvent。
			NotificationCenter.Current.ApplicationThemeChanged += ApplicationThemeChanged;
		}

		public void UpdateLineNumbersData()
		{
			int num = Math.Max(_lineNumberLength, base.Document.LineCount.ToString().Length);
			if (num != _lineNumberLength)
			{
				_lineNumberLength = num;
				InvalidateMeasure();
			}
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			return new Size(CreateFormattedText(new string('9', _lineNumberLength)).Width + HorizontalMargin * 3.0, 0.0);
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);
			foreach (VisualLine visualLine in base.TextView.VisualLines)
			{
				drawingContext.DrawText(CreateFormattedText(visualLine.FirstDocumentLine.LineNumber.ToString()), new Point(base.RenderSize.Width - HorizontalMargin * 2.0, visualLine.VisualTop - base.TextView.VerticalOffset));
			}
			drawingContext.DrawLine(_separatorPen, new Point(base.RenderSize.Width - HorizontalMargin, 0.0), new Point(base.RenderSize.Width - HorizontalMargin, base.RenderSize.Height));
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
		object res = Theme.FindResource(key);
		if (res is Color c) return c;
		if (res is SolidColorBrush b) return b.Color;
		return null;
	}

	private static IBrush TryFindColorBrush(string key)
	{
		Color? c = TryFindColor(key);
		return c.HasValue ? new SolidColorBrush(c.Value) : null;
	}

		private FormattedText CreateFormattedText(string text)
		{
			// 阶段 4 里程碑 4.7-a：WPF FormattedText(..., pixelsPerDip) → Avalonia FormattedText
			// （无 pixelsPerDip 参数，Avalonia 内部按渲染缩放处理）。
			return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.RightToLeft, typeface, emSize, _brush);
		}
	}
}
