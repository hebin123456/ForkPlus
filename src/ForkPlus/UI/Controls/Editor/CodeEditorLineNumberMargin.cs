using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ForkPlus.Settings;
using ICSharpCode.AvalonEdit.Rendering;

namespace ForkPlus.UI.Controls.Editor
{
	internal class CodeEditorLineNumberMargin : ClearTypeLineNumberMargin
	{
		private static readonly Typeface _typeface;

		private static readonly Brush _lightBrush;

		private static readonly Brush _darkBrush;

		private static readonly Pen _separatorPenLight;

		private static readonly Pen _separatorPenDark;

		private static readonly double HorizontalMargin;

		private Brush _brush;

		private Pen _separatorPen;

		private int _lineNumberLength = 2;

		static CodeEditorLineNumberMargin()
		{
			_typeface = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal, new FontFamily("Courier New"));
			_lightBrush = new SolidColorBrush(Color.FromRgb(192, 192, 192));
			_darkBrush = new SolidColorBrush(Color.FromRgb(160, 160, 160));
			_separatorPenLight = new Pen(new SolidColorBrush(Color.FromRgb(218, 218, 215)), 1.0);
			_separatorPenDark = new Pen(new SolidColorBrush(Color.FromRgb(110, 110, 110)), 1.0);
			HorizontalMargin = 5.0;
			_lightBrush.Freeze();
			_darkBrush.Freeze();
			_separatorPenLight.Freeze();
			_separatorPenDark.Freeze();
		}

		public CodeEditorLineNumberMargin()
		{
			typeface = _typeface;
			emSize = 11.0;
			RefreshBrushes();
			WeakEventManager<NotificationCenter, EventArgs<ThemeType>>.AddHandler(NotificationCenter.Current, "ApplicationThemeChanged", ApplicationThemeChanged);
			RenderOptions.SetClearTypeHint(this, ClearTypeHint.Enabled);
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
			if (ForkPlusSettings.Default.Theme.IsDarkBase())
			{
				_brush = _darkBrush;
				_separatorPen = _separatorPenDark;
			}
			else
			{
				_brush = _lightBrush;
				_separatorPen = _separatorPenLight;
			}
		}

		private FormattedText CreateFormattedText(string text)
		{
			return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.RightToLeft, typeface, emSize, _brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
		}
	}
}
