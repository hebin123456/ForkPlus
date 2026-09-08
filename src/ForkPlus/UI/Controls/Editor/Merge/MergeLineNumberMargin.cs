using System;
using ForkPlus.UI.WpfCompat;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using ForkPlus.Git.Merge;
using ForkPlus.Git.Merge.Presentation;
using ForkPlus.Settings;
using AvaloniaEdit.Rendering;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls.Editor.Merge
{
	internal class MergeLineNumberMargin : ClearTypeLineNumberMargin
	{
		// v3.13 修复（v3.12 同款）：WPF 原版 Typeface 第 5 参 fallback FontFamily("Courier New")
		// 迁移时丢失——非 Windows 无 Consolas 时回退到等宽字体，避免行号测量与实际渲染宽度不一致。
		private static readonly Typeface _typeface = new Typeface(new FontFamily("Consolas, Courier New, monospace"), FontStyles.Normal, FontWeights.Normal);

		private static readonly Brush _textBrush = new SolidColorBrush(Color.FromRgb(192, 192, 192));

		private static readonly Pen _separatorPenLight = new Pen(new SolidColorBrush(Color.FromRgb(218, 218, 215)), 1.0);

		private static readonly Pen _separatorPenDark = new Pen(new SolidColorBrush(Color.FromRgb(110, 110, 110)), 1.0);

		private static readonly Brush _mergeConflictMouseOverBrushLight = new SolidColorBrush(Color.FromRgb(216, 216, 216));

		private static readonly Brush _mergeConflictMouseOverBrushDark = new SolidColorBrush(Color.FromRgb(165, 165, 165));

		private static readonly Brush _mergeConflictSelectedBrush = new SolidColorBrush(Color.FromRgb(59, 137, 218));

		private static readonly double HorizontalMargin = 10.0;

		private readonly MergeCodeEditor _editor;

		private int _mouseOverLine = -1;

		private Pen _separatorPen;

		private Brush _mergeConflictMouseOverBrush;

		private int _lineNumberLength = 2;

		private Dictionary<int, int> _lineNumbers = new Dictionary<int, int>();

		// Migration note：WPF 原基类（AvalonEdit TextEditorMargin 系）的 typeface/emSize 字段补声明

		private Typeface typeface = _typeface;

		private double emSize = 11.0;


		public MergeLineNumberMargin(MergeCodeEditor editor)
		{
			_editor = editor;
			// Migration note：WPF 原基类的 typeface/emSize 字段在此补声明
			this.typeface = _typeface;
			emSize = 11.0;
			RefreshPen();
			WeakEventManager<NotificationCenter, EventArgs<ThemeType>>.AddHandler(NotificationCenter.Current, "ApplicationThemeChanged", ApplicationThemeChanged);
			RenderOptionsShim.SetClearTypeHint(this, ClearTypeHint.Enabled);
		}

		public void UpdateLineNumbersData(MergeConflictView mergeConflictView)
		{
			_lineNumbers.Clear();
			if (mergeConflictView == null)
			{
				_lineNumberLength = 2;
				return;
			}
			int num = 0;
			MergeConflictView.Chunk[] chunks = mergeConflictView.Chunks;
			for (int i = 0; i < chunks.Length; i++)
			{
				MergeConflictView.Line[] lines = chunks[i].Lines;
				foreach (MergeConflictView.Line line in lines)
				{
					if (!(line.Node is MergeConflict.EmptyLine) || mergeConflictView.ViewMode == MergeConflictPart.Merged)
					{
						num++;
						_lineNumbers[line.LineNumber] = num;
					}
				}
			}
			int num2 = Math.Max(2, num.ToString().Length);
			if (num2 != _lineNumberLength)
			{
				_lineNumberLength = num2;
				InvalidateMeasure();
			}
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			return new Size(CreateFormattedText(new string('9', _lineNumberLength), _textBrush).Width + HorizontalMargin * 2.0, 0.0);
		}

		private Point Offset(Point start, double xOffset, double yOffset)
		{
			return new Point(start.X + xOffset, start.Y + yOffset);
		}

		private Geometry CreateShevronGeometry(Point origin)
		{
			StreamGeometry streamGeometry = new StreamGeometry();
			using StreamGeometryContext streamGeometryContext = streamGeometry.Open();
			double num = base.Bounds.Size.Width - 3.0;
			double num2 = 16.0;
			streamGeometryContext.BeginFigure(origin,true);
			streamGeometryContext.LineTo(Offset(origin, num - 5.0, 0.0),true);
			streamGeometryContext.LineTo(Offset(origin, num, num2 / 2.0),true);
			streamGeometryContext.LineTo(Offset(origin, num - 5.0, num2),true);
			streamGeometryContext.LineTo(Offset(origin, 0.0, num2),true);
			streamGeometryContext.LineTo(origin,true);
			return streamGeometry;
		}

		public override void Render(DrawingContext drawingContext)
		{
			base.Render(drawingContext);
			foreach (VisualLine visualLine in base.TextView.VisualLines)
			{
				IBrush brush = _textBrush;
				if (_editor.ViewMode == MergeConflictPart.Local || _editor.ViewMode == MergeConflictPart.Remote)
				{
					if (IsLineSelected(visualLine.FirstDocumentLine.LineNumber - 1))
					{
						Geometry geometry = CreateShevronGeometry(new Point(0.0, visualLine.VisualTop - base.TextView.ScrollOffset.Y));
						drawingContext.DrawGeometry(_mergeConflictSelectedBrush, null, geometry);
						brush = Brushes.White;
					}
					else if (visualLine.FirstDocumentLine.LineNumber == _mouseOverLine)
					{
						Geometry geometry2 = CreateShevronGeometry(new Point(0.0, visualLine.VisualTop - base.TextView.ScrollOffset.Y));
						drawingContext.DrawGeometry(_mergeConflictMouseOverBrush, null, geometry2);
						brush = Brushes.White;
					}
				}
				if (_lineNumbers.TryGetValue(visualLine.FirstDocumentLine.LineNumber - 1, out var value))
				{
					// v3.13 修复（行号被代码区遮挡）：WPF 的 RTL FormattedText DrawText(origin) 以 origin
					// 为右上角向左绘制；Avalonia 的 origin 恒为左上角，须显式减去文本宽度防右缘溢出。
					FormattedText text = CreateFormattedText(value.ToString(), brush);
					drawingContext.DrawText(text, new Point(base.Bounds.Size.Width - HorizontalMargin - text.Width, visualLine.VisualTop - base.TextView.ScrollOffset.Y + 1.0));
				}
			}
			drawingContext.DrawLine(_separatorPen, new Point(base.Bounds.Size.Width - 2.0, 0.0), new Point(base.Bounds.Size.Width - 2.0, base.Bounds.Size.Height));
		}

		protected override void OnPointerExited(global::Avalonia.Input.PointerEventArgs e)
		{
			base.OnPointerExited(e);
			_mouseOverLine = -1;
			InvalidateVisual();
		}

		protected override void OnPointerMoved(global::Avalonia.Input.PointerEventArgs e)
		{
			base.OnPointerMoved(e);
			Point position = e.GetPosition(base.TextView);
			VisualLine visualLineFromVisualTop = base.TextView.GetVisualLineFromVisualTop(position.Y + base.TextView.ScrollOffset.Y);
			if (visualLineFromVisualTop == null)
			{
				return;
			}
			int lineNumber = visualLineFromVisualTop.FirstDocumentLine.LineNumber;
			if (_mouseOverLine != lineNumber)
			{
				if (!IsLineSelectable(lineNumber - 1))
				{
					_mouseOverLine = -1;
				}
				else
				{
					_mouseOverLine = visualLineFromVisualTop.FirstDocumentLine.LineNumber;
				}
				InvalidateVisual();
			}
		}

		protected override void OnPointerPressed(global::Avalonia.Input.PointerPressedEventArgs e)
		{
			e.Handled = true;
			base.OnPointerPressed(e);
			int lineUnderCursor = GetLineUnderCursor(e);
			if (lineUnderCursor != -1)
			{
				if (IsLineSelected(lineUnderCursor - 1))
				{
					_editor.OnMergeLineRemoved(lineUnderCursor - 1);
					InvalidateVisual();
				}
				else
				{
					_editor.OnMergeLineAdded(lineUnderCursor - 1);
					InvalidateVisual();
				}
			}
		}

		private int GetLineUnderCursor(global::Avalonia.Input.PointerEventArgs e)
		{
			Point position = e.GetPosition(base.TextView);
			return base.TextView.GetVisualLineFromVisualTop(position.Y + base.TextView.ScrollOffset.Y)?.FirstDocumentLine.LineNumber ?? (-1);
		}

		private bool IsLineSelected(int lineNumber)
		{
			MergeConflictView mergeConflictView = _editor.MergeConflictView;
			if (mergeConflictView == null)
			{
				return false;
			}
			MergeConflictView.Chunk[] chunks = mergeConflictView.Chunks;
			foreach (MergeConflictView.Chunk chunk in chunks)
			{
				if (!chunk.LineRange.Contains(lineNumber) || !(chunk.Node is MergeConflict.ConflictChunk))
				{
					continue;
				}
				MergeConflictView.Line[] lines = chunk.Lines;
				foreach (MergeConflictView.Line line in lines)
				{
					if (line.LineNumber > lineNumber)
					{
						break;
					}
					if (line.LineNumber == lineNumber && line.Node is MergeConflict.SelectableLine selectableLine)
					{
						return selectableLine.IsSelected;
					}
				}
				return false;
			}
			return false;
		}

		private bool IsLineSelectable(int lineNumber)
		{
			MergeConflictView mergeConflictView = _editor.MergeConflictView;
			if (mergeConflictView == null)
			{
				return false;
			}
			MergeConflictView.Chunk[] chunks = mergeConflictView.Chunks;
			foreach (MergeConflictView.Chunk chunk in chunks)
			{
				if (!chunk.LineRange.Contains(lineNumber))
				{
					continue;
				}
				MergeConflictView.Line[] lines = chunk.Lines;
				foreach (MergeConflictView.Line line in lines)
				{
					if (line.LineNumber > lineNumber)
					{
						break;
					}
					if (line.LineNumber == lineNumber && line.Node is MergeConflict.SelectableLine && line.Node.ParentChunk is MergeConflict.ConflictChunk)
					{
						return true;
					}
				}
				return false;
			}
			return false;
		}

		private void ApplicationThemeChanged(object sender, EventArgs<ThemeType> e)
		{
			RefreshPen();
		}

		private void RefreshPen()
	{
		// 优先读资源（CustomColorsDialog 覆盖或主题字典），取不到回退到 light/dark 静态画刷。
		Color? sepColor = TryFindColor("LineNumber.SeparatorColor");
		_separatorPen = sepColor.HasValue
			? new Pen(new SolidColorBrush(sepColor.Value), 1.0)
			: (ForkPlusSettings.Default.Theme.IsDarkBase() ? _separatorPenDark : _separatorPenLight);
		_mergeConflictMouseOverBrush = TryFindColorBrush("MergeConflict.MouseOverColor")
			?? (ForkPlusSettings.Default.Theme.IsDarkBase() ? _mergeConflictMouseOverBrushDark : _mergeConflictMouseOverBrushLight);
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

		// Migration note：WPF Brush → Avalonia IBrush（FormattedText.Foreground 接受 IBrush）。
                private FormattedText CreateFormattedText(string text, IBrush brush)
		{
			return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.RightToLeft, typeface, emSize, brush);
		}
	}
}
