using System;
using ForkPlus.UI.WpfCompat;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using ForkPlus.Settings;
using AvaloniaEdit.Rendering;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

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

		// Migration note：WPF 原基类（AvalonEdit TextEditorMargin 系）的 typeface/emSize 实例字段，
		// AvaloniaEdit 侧无此基类，在此补声明（CreateFormattedText 使用）。
		private Typeface typeface = _typeface;
		private double emSize = 11.0;

		static CodeEditorLineNumberMargin()
		{
			// v3.13 修复（v3.12 同款，DiffLineNumberMargin 已修此处漏掉）：WPF 原版 Typeface
			// 第 5 参指定 fallback FontFamily("Courier New")，迁移时丢失。Avalonia 无该构造
			// 重载，用内联 fallback 列表等价表达：非 Windows 平台无 Consolas 时回退到等宽
			// 字体（Courier New → monospace），避免回退到比例字体导致行号实际宽度与测量
			// 宽度（'9'×N）不一致、右缘被代码区遮住。
			_typeface = new Typeface(new FontFamily("Consolas, Courier New, monospace"), FontStyles.Normal, FontWeights.Normal);
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
			WeakEventManager<NotificationCenter, EventArgs<ThemeType>>.AddHandler(NotificationCenter.Current, "ApplicationThemeChanged", ApplicationThemeChanged);
			RenderOptionsShim.SetClearTypeHint(this, ClearTypeHint.Enabled);
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

		public override void Render(DrawingContext drawingContext)
		{
			base.Render(drawingContext);
			foreach (VisualLine visualLine in base.TextView.VisualLines)
			{
				// v3.13 修复（行号两位数以上被代码区遮挡）：WPF 的 RTL FormattedText DrawText(origin)
				// 以 origin 为右上角向左绘制（原版行号右缘贴 Width-10）；Avalonia 的 origin 恒为
				// 左上角，须显式减去文本宽度，否则文本从 Width-10 向右溢出 margin 边界——
				// 一位数勉强在界内，两位数右缘溢出 ~4px、三位数更多，被代码区遮住。
				FormattedText text = CreateFormattedText(visualLine.FirstDocumentLine.LineNumber.ToString());
				drawingContext.DrawText(text, new Point(base.Bounds.Size.Width - HorizontalMargin * 2.0 - text.Width, visualLine.VisualTop - base.TextView.ScrollOffset.Y));
			}
			drawingContext.DrawLine(_separatorPen, new Point(base.Bounds.Size.Width - HorizontalMargin, 0.0), new Point(base.Bounds.Size.Width - HorizontalMargin, base.Bounds.Size.Height));
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
			// Migration note：Avalonia FormattedText 无 pixelsPerDip 参数（6 参构造）
			return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.RightToLeft, typeface, emSize, _brush);
		}
	}
}
