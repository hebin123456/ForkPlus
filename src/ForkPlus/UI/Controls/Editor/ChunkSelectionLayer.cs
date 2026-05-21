using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ForkPlus.UI;
using ForkPlus.Settings;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Utils;

namespace ForkPlus.UI.Controls.Editor
{
	public abstract class ChunkSelectionLayer<TChunk> : FrameworkElement, IWeakEventListener where TChunk : class
	{
		public class ButtonsAdorner : Adorner
		{
			private FrameworkElement _child;

			public FrameworkElement Child
			{
				get
				{
					return _child;
				}
				set
				{
					if (_child != value)
					{
						if (_child != null)
						{
							RemoveVisualChild(_child);
							RemoveLogicalChild(_child);
						}
						if (value != null && !VisualTreeAttachmentHelper.PrepareForNewParent(value, GetType().Name + ".Child"))
						{
							value = null;
						}
						_child = value;
						if (_child != null)
						{
							AddLogicalChild(_child);
							AddVisualChild(_child);
						}
						InvalidateMeasure();
					}
				}
			}

			protected override int VisualChildrenCount => (Child != null) ? 1 : 0;

			public ButtonsAdorner(UIElement adornernedElement)
				: base(adornernedElement)
			{
			}

			protected override Visual GetVisualChild(int index)
			{
				return Child;
			}

			protected override Size MeasureOverride(Size constraint)
			{
				if (Child == null)
				{
					return default(Size);
				}
				Child.Measure(constraint);
				Size result = Child.DesiredSize;
				if (result.Width < 40.0)
				{
					result = new Size(40.0, result.Height);
				}
				return result;
			}

			protected override Size ArrangeOverride(Size finalSize)
			{
				if (Child == null)
				{
					return default(Size);
				}
				Child.Arrange(new Rect(finalSize));
				return finalSize;
			}
		}

		[Null]
		protected TChunk _activeChunk;

		[Null]
		private ButtonsAdorner _adorner;

		[Null]
		private AdornerLayer _adornerLayer;

		private readonly CodeEditor _textEditor;

		protected Brush ChunkBackgroundBrush;

		protected static readonly Pen _chunkBorderPen;

		protected static readonly Brush _chunkBorderBrush;

		protected static readonly Brush _chunkBackgroundBrush;

		protected static readonly Brush _chunkBackgroundBrushDark;

		[Null]
		public virtual TChunk ActiveChunk
		{
			get
			{
				return _activeChunk;
			}
			set
			{
				if (_activeChunk != value)
				{
					_activeChunk = value;
					InvalidateAdornerVisibility();
					InvalidateVisual();
				}
			}
		}

		static ChunkSelectionLayer()
		{
			_chunkBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(65, 155, 249)), 1.0);
			_chunkBorderBrush = new SolidColorBrush(Color.FromRgb(65, 155, 249));
			_chunkBackgroundBrush = new SolidColorBrush(Color.FromArgb(60, 230, 241, byte.MaxValue));
			_chunkBackgroundBrushDark = new SolidColorBrush(Color.FromArgb(20, 53, 140, byte.MaxValue));
			_chunkBorderPen.Freeze();
			_chunkBorderBrush.Freeze();
			_chunkBackgroundBrush.Freeze();
			_chunkBackgroundBrushDark.Freeze();
		}

		public ChunkSelectionLayer(CodeEditor textEditor)
		{
			_textEditor = textEditor;
			base.IsHitTestVisible = false;
			_textEditor.MouseEnter += TextEditor_MouseEnter;
			_textEditor.MouseLeave += TextEditor_MouseLeave;
			_textEditor.MouseMove += TextEditor_MouseMove;
			_textEditor.TextArea.SelectionChanged += TextArea_SelectionChanged;
			_textEditor.TextChanged += TextEditor_TextChanged;
			_textEditor.IsVisibleChanged += TextEditor_IsVisibleChanged;
			RefreshBrush();
			WeakEventManagerBase<TextViewWeakEventManager.ScrollOffsetChanged, TextView>.AddListener(_textEditor.TextArea.TextView, this);
			WeakEventManager<NotificationCenter, EventArgs<ThemeType>>.AddHandler(NotificationCenter.Current, "ApplicationThemeChanged", ApplicationThemeChanged);
		}

		bool IWeakEventListener.ReceiveWeakEvent(Type managerType, object sender, EventArgs e)
		{
			if (managerType == typeof(TextViewWeakEventManager.ScrollOffsetChanged))
			{
				RefreshActiveChunk();
				InvalidateVisual();
				return true;
			}
			return false;
		}

		protected abstract void RefreshActiveChunk();

		protected abstract FrameworkElement CreateAdornerContent(TextEditor textEditor);

		protected abstract Rect? GetRectForChunk(TChunk chunk);

		protected void DrawChunk(DrawingContext drawingContext, TextView textView, TChunk chunk)
		{
			if (!textView.VisualLinesValid)
			{
				return;
			}
			Rect? rectForChunk = GetRectForChunk(chunk);
			if (!rectForChunk.HasValue)
			{
				return;
			}
			Rect valueOrDefault = rectForChunk.GetValueOrDefault();
			DrawBorder(valueOrDefault, drawingContext);
			if (_textEditor.ViewportHeight > _textEditor.ExtentHeight)
			{
				if (_textEditor.TextArea.TextView.VerticalOffset > 0.0)
				{
					return;
				}
			}
			else if (!_textEditor.IsVerticalOffsetWithinDocumentArea(_textEditor.TextArea.TextView.VerticalOffset))
			{
				return;
			}
			ShowAdornerOnMouseOver(valueOrDefault.Top + _textEditor.SearchBarHeight);
		}

		protected virtual void OnTextAreaSelectionChanged()
		{
			RefreshActiveChunk();
			InvalidateVisual();
		}

		protected virtual void InvalidateAdornerVisibility()
		{
			if (_activeChunk != null || _textEditor.TextArea.Selection.Length > 0)
			{
				ShowChunkAdorner(0.0);
			}
			else
			{
				RemoveChunkAdorner();
			}
		}

		protected virtual void ShowAdornerOnMouseOver(double topPosition)
		{
			ShowChunkAdorner(topPosition);
		}

		protected void ShowChunkAdorner(double popupTopPosition)
		{
			double num = 15.0;
			double num2 = 20.0;
			num -= _textEditor.SearchBarHeight;
			double num3 = _textEditor.TextArea.TextView.ActualWidth - num2;
			double top = popupTopPosition + num;
			if (_adorner == null)
			{
				_adornerLayer = AdornerLayer.GetAdornerLayer(this) ?? AdornerLayer.GetAdornerLayer(_textEditor.TextArea);
				if (_adornerLayer == null)
				{
					return;
				}
				_adorner = new ButtonsAdorner(this);
				_adorner.Child = CreateAdornerContent(_textEditor);
				_adornerLayer.Add(_adorner);
			}
			_adorner.Child.Measure(new Size(1000.0, 22.0));
			double width = _adorner.Child.DesiredSize.Width;
			_adorner.Margin = new Thickness(num3 - width, top, 0.0, 0.0);
		}

		protected void RemoveChunkAdorner()
		{
			if (_adorner != null)
			{
				_adorner.Child = null;
				_adornerLayer?.Remove(_adorner);
				_adornerLayer = null;
				_adorner = null;
			}
		}

		private void TextEditor_MouseEnter(object sender, MouseEventArgs e)
		{
			RefreshActiveChunk();
		}

		private void TextEditor_MouseLeave(object sender, MouseEventArgs e)
		{
			ContextMenu contextMenu = _textEditor.ContextMenu;
			if (contextMenu == null || !contextMenu.IsMouseOver)
			{
				ButtonsAdorner adorner = _adorner;
				if (adorner == null || VisualTreeHelper.HitTest(adorner, e.GetPosition(_adorner)) == null)
				{
					ActiveChunk = null;
				}
			}
		}

		private void TextEditor_MouseMove(object sender, MouseEventArgs e)
		{
			RefreshActiveChunk();
			e.Handled = true;
		}

		private void TextEditor_TextChanged(object sender, EventArgs e)
		{
			ActiveChunk = null;
		}

		private void TextEditor_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			if (!_textEditor.IsVisible)
			{
				RemoveChunkAdorner();
			}
		}

		private void TextArea_SelectionChanged(object sender, EventArgs e)
		{
			OnTextAreaSelectionChanged();
		}

		private void ApplicationThemeChanged(object sender, EventArgs<ThemeType> e)
		{
			RefreshBrush();
		}

		private void RefreshBrush()
		{
			if (ForkPlusSettings.Default.Theme == ThemeType.Dark)
			{
				ChunkBackgroundBrush = _chunkBackgroundBrushDark;
			}
			else
			{
				ChunkBackgroundBrush = _chunkBackgroundBrush;
			}
		}

		protected void DrawSelectionBorder(DrawingContext drawingContext, TextArea textArea)
		{
			ISegment surroundingSegment = textArea.Selection.SurroundingSegment;
			double num = 0.0;
			double num2 = 0.0;
			bool flag = true;
			foreach (Rect item in BackgroundGeometryBuilder.GetRectsForSegment(textArea.TextView, surroundingSegment, extendToFullWidthAtLineEnd: true))
			{
				if (flag)
				{
					num = item.Top;
				}
				num2 += item.Height;
				flag = false;
			}
			Rect rect = new Rect(0.0, num, _textEditor.ActualWidth, num2);
			DrawBorder(rect, drawingContext);
			ShowChunkAdorner(num + _textEditor.SearchBarHeight);
		}

		protected virtual void DrawBorder(Rect rect, DrawingContext drawingContext)
		{
			int num = 2;
			int num2 = 2;
			drawingContext.DrawGeometry(geometry: new RectangleGeometry(new Rect(rect.X + (double)num, rect.Y, rect.Width, rect.Height), num2, num2), brush: ChunkBackgroundBrush, pen: _chunkBorderPen);
		}

		protected Geometry CreateSelectionGeometry(TextArea textArea)
		{
			if (!textArea.TextView.VisualLinesValid)
			{
				return null;
			}
			BackgroundGeometryBuilder backgroundGeometryBuilder = CreateBackgroundGeometryBuilder(0.0);
			foreach (SelectionSegment segment in textArea.Selection.Segments)
			{
				backgroundGeometryBuilder.AddSegment(textArea.TextView, segment);
			}
			return backgroundGeometryBuilder.CreateGeometry();
		}

		private static BackgroundGeometryBuilder CreateBackgroundGeometryBuilder(double borderThickness)
		{
			return new BackgroundGeometryBuilder
			{
				BorderThickness = borderThickness,
				AlignToWholePixels = true,
				ExtendToFullWidthAtLineEnd = true
			};
		}

		protected Rect CreateLineBlockRect(VisualLine topVisualLine, int lineCount)
		{
			TextView textView = _textEditor.TextArea.TextView;
			double num = topVisualLine.VisualTop - textView.ScrollOffset.Y;
			double num2 = 0.0;
			int lineNumber = topVisualLine.FirstDocumentLine.LineNumber;
			for (int i = lineNumber; i < lineNumber + lineCount; i++)
			{
				double num3 = textView.GetVisualLine(i)?.Height ?? 0.0;
				num2 += num3;
			}
			return new Rect(0.0, num + 1.0, textView.ActualWidth, num2 - 1.0);
		}

		[Null]
		protected TChunk GetChunkUnderMousePointer()
		{
			Point position = Mouse.GetPosition(_textEditor);
			if (VisualTreeHelper.HitTest(_textEditor, position) == null)
			{
				return null;
			}
			TextViewPosition? positionFromPoint = _textEditor.GetPositionFromPoint(position);
			if (!positionFromPoint.HasValue)
			{
				return null;
			}
			TextLocation location = positionFromPoint.Value.Location;
			int offset = _textEditor.Document.GetOffset(location);
			return GetChunkByOffset(offset);
		}

		[Null]
		protected abstract TChunk GetChunkByOffset(int offset);
	}
}
