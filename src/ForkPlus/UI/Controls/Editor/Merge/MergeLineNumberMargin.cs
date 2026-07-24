using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using ForkPlus.Git.Merge;
using ForkPlus.Git.Merge.Presentation;
using ForkPlus.Settings;
using AvaloniaEdit.Rendering;

namespace ForkPlus.UI.Controls.Editor.Merge
{
	internal class MergeLineNumberMargin : ClearTypeLineNumberMargin
	{
		// 阶段 4 里程碑 4.7-a：WPF Typeface → Avalonia Typeface（逗号分隔回退字体）；
		// WPF Brush → Avalonia IBrush；Freeze() 移除（Avalonia 画刷默认不可变）。
		private static readonly Typeface _typeface = new Typeface(new FontFamily("Consolas, Courier New"), FontStyle.Normal, FontWeight.Normal, FontStretch.Normal);

		private static readonly IBrush _textBrush = new SolidColorBrush(Color.FromRgb(192, 192, 192));

		private static readonly Pen _separatorPenLight = new Pen(new SolidColorBrush(Color.FromRgb(218, 218, 215)), 1.0);

		private static readonly Pen _separatorPenDark = new Pen(new SolidColorBrush(Color.FromRgb(110, 110, 110)), 1.0);

		private static readonly IBrush _mergeConflictMouseOverBrushLight = new SolidColorBrush(Color.FromRgb(216, 216, 216));

		private static readonly IBrush _mergeConflictMouseOverBrushDark = new SolidColorBrush(Color.FromRgb(165, 165, 165));

		private static readonly IBrush _mergeConflictSelectedBrush = new SolidColorBrush(Color.FromRgb(59, 137, 218));

		private static readonly double HorizontalMargin = 10.0;

		private readonly MergeCodeEditor _editor;

		private int _mouseOverLine = -1;

		private Pen _separatorPen;

		private IBrush _mergeConflictMouseOverBrush;

		private int _lineNumberLength = 2;

		private Dictionary<int, int> _lineNumbers = new Dictionary<int, int>();

		public MergeLineNumberMargin(MergeCodeEditor editor)
		{
			_editor = editor;
			typeface = _typeface;
			emSize = 11.0;
			RefreshPen();
			// 阶段 4 里程碑 4.7-a：WeakEventManager → 直接事件订阅。阶段 6 改用 WeakEvent。
			NotificationCenter.Current.ApplicationThemeChanged += ApplicationThemeChanged;
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
			double num = base.RenderSize.Width - 3.0;
			double num2 = 16.0;
			// 阶段 4 里程碑 4.7-a：WPF BeginFigure(p, isFilled, isClosed) / LineTo(p, isStroked, isSmoothJoin) →
			// Avalonia BeginFigure(p, isFilled) / LineTo(p, isStroked) + EndFigure(isClosed)。原 isClosed:false，
			// 视觉上最后一条 LineTo 已回到 origin，逻辑上保持 open 以匹配 WPF 行为。
			streamGeometryContext.BeginFigure(origin, isFilled: true);
			streamGeometryContext.LineTo(Offset(origin, num - 5.0, 0.0), isStroked: true);
			streamGeometryContext.LineTo(Offset(origin, num, num2 / 2.0), isStroked: true);
			streamGeometryContext.LineTo(Offset(origin, num - 5.0, num2), isStroked: true);
			streamGeometryContext.LineTo(Offset(origin, 0.0, num2), isStroked: true);
			streamGeometryContext.LineTo(origin, isStroked: true);
			streamGeometryContext.EndFigure(isClosed: false);
			return streamGeometry;
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);
			foreach (VisualLine visualLine in base.TextView.VisualLines)
			{
				IBrush brush = _textBrush;
				if (_editor.ViewMode == MergeConflictPart.Local || _editor.ViewMode == MergeConflictPart.Remote)
				{
					if (IsLineSelected(visualLine.FirstDocumentLine.LineNumber - 1))
					{
						Geometry geometry = CreateShevronGeometry(new Point(0.0, visualLine.VisualTop - base.TextView.VerticalOffset));
						drawingContext.DrawGeometry(_mergeConflictSelectedBrush, null, geometry);
						brush = Brushes.White;
					}
					else if (visualLine.FirstDocumentLine.LineNumber == _mouseOverLine)
					{
						Geometry geometry2 = CreateShevronGeometry(new Point(0.0, visualLine.VisualTop - base.TextView.VerticalOffset));
						drawingContext.DrawGeometry(_mergeConflictMouseOverBrush, null, geometry2);
						brush = Brushes.White;
					}
				}
				if (_lineNumbers.TryGetValue(visualLine.FirstDocumentLine.LineNumber - 1, out var value))
				{
					drawingContext.DrawText(CreateFormattedText(value.ToString(), brush), new Point(base.RenderSize.Width - HorizontalMargin, visualLine.VisualTop - base.TextView.VerticalOffset + 1.0));
				}
			}
			drawingContext.DrawLine(_separatorPen, new Point(base.RenderSize.Width - 2.0, 0.0), new Point(base.RenderSize.Width - 2.0, base.RenderSize.Height));
		}

		// 阶段 4 里程碑 4.7-a：WPF Mouse* 事件 → Avalonia Pointer* 事件。
		// OnMouseLeave → OnPointerLeave, OnMouseMove → OnPointerMoved,
		// OnMouseLeftButtonDown → OnPointerPressed。e.GetPosition API 一致。
		protected override void OnPointerLeave(PointerEventArgs e)
		{
			base.OnPointerLeave(e);
			_mouseOverLine = -1;
			InvalidateVisual();
		}

		protected override void OnPointerMoved(PointerEventArgs e)
		{
			base.OnPointerMoved(e);
			Point position = e.GetPosition(base.TextView);
			VisualLine visualLineFromVisualTop = base.TextView.GetVisualLineFromVisualTop(position.Y + base.TextView.VerticalOffset);
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

		protected override void OnPointerPressed(PointerPressedEventArgs e)
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

		private int GetLineUnderCursor(PointerEventArgs e)
		{
			Point position = e.GetPosition(base.TextView);
			return base.TextView.GetVisualLineFromVisualTop(position.Y + base.TextView.VerticalOffset)?.FirstDocumentLine.LineNumber ?? (-1);
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

		private FormattedText CreateFormattedText(string text, IBrush brush)
		{
			// 阶段 4 里程碑 4.7-a：WPF FormattedText(..., pixelsPerDip) → Avalonia FormattedText（无 pixelsPerDip）。
			return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.RightToLeft, typeface, emSize, brush);
		}
	}
}
