using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ForkPlus.Git.Merge;
using ForkPlus.Git.Merge.Presentation;
using ForkPlus.Settings;
using ICSharpCode.AvalonEdit.Rendering;

namespace ForkPlus.UI.Controls.Editor.Merge
{
	internal class MergeLineNumberMargin : ClearTypeLineNumberMargin
	{
		private static readonly Typeface _typeface = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal, new FontFamily("Courier New"));

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

		public MergeLineNumberMargin(MergeCodeEditor editor)
		{
			_editor = editor;
			typeface = _typeface;
			emSize = 11.0;
			RefreshPen();
			WeakEventManager<NotificationCenter, EventArgs<ThemeType>>.AddHandler(NotificationCenter.Current, "ApplicationThemeChanged", ApplicationThemeChanged);
			RenderOptions.SetClearTypeHint(this, ClearTypeHint.Enabled);
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
			streamGeometryContext.BeginFigure(origin, isFilled: true, isClosed: false);
			streamGeometryContext.LineTo(Offset(origin, num - 5.0, 0.0), isStroked: true, isSmoothJoin: false);
			streamGeometryContext.LineTo(Offset(origin, num, num2 / 2.0), isStroked: true, isSmoothJoin: false);
			streamGeometryContext.LineTo(Offset(origin, num - 5.0, num2), isStroked: true, isSmoothJoin: false);
			streamGeometryContext.LineTo(Offset(origin, 0.0, num2), isStroked: true, isSmoothJoin: false);
			streamGeometryContext.LineTo(origin, isStroked: true, isSmoothJoin: false);
			return streamGeometry;
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);
			foreach (VisualLine visualLine in base.TextView.VisualLines)
			{
				Brush brush = _textBrush;
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

		protected override void OnMouseLeave(MouseEventArgs e)
		{
			base.OnMouseLeave(e);
			_mouseOverLine = -1;
			InvalidateVisual();
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
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

		protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			e.Handled = true;
			base.OnMouseLeftButtonDown(e);
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

		private int GetLineUnderCursor(MouseEventArgs e)
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
			if (ForkPlusSettings.Default.Theme.IsDarkBase())
			{
				_separatorPen = _separatorPenDark;
				_mergeConflictMouseOverBrush = _mergeConflictMouseOverBrushDark;
			}
			else
			{
				_separatorPen = _separatorPenLight;
				_mergeConflictMouseOverBrush = _mergeConflictMouseOverBrushLight;
			}
		}

		private FormattedText CreateFormattedText(string text, Brush brush)
		{
			return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.RightToLeft, typeface, emSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
		}
	}
}
