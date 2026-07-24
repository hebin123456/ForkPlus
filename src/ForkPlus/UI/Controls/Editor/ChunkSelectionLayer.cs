using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using ForkPlus.UI;
using ForkPlus.Settings;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Utils;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.Controls.Editor
{
	/// <summary>
	/// 阶段 4 里程碑 4.7-a：WPF→Avalonia 迁移要点：
	/// - FrameworkElement → Avalonia.Controls.Control
	/// - Adorner/AdornerLayer → ButtonsOverlay (自定义 Control) + OverlayLayer (Avalonia.Controls.Primitives)
	/// - IWeakEventListener/WeakEventManagerBase → 直接事件订阅
	/// - VisualTreeHelper.HitTest → InputHitTest
	/// - Mouse.GetPosition → 缓存 PointerMoved 事件位置
	/// - Brush → IBrush, Freeze() 移除
	/// - PointerEventArgs → PointerEventArgs, Mouse* 事件 → Pointer* 事件
	/// </summary>
	public abstract class ChunkSelectionLayer<TChunk> : Control where TChunk : class
	{
		public class ButtonsOverlay : Control
		{
			private Control _child;

			public Control Child
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
						}
						// TODO(4.7-a): WPF 版用 VisualTreeAttachmentHelper.PrepareForNewParent 先 detach。
						// Avalonia 无等价物；假设 child 没有旧 parent（CreateAdornerContent 每次创建新实例）。
						_child = value;
						if (_child != null)
						{
							AddVisualChild(_child);
						}
						InvalidateMeasure();
					}
				}
			}

			protected override int VisualChildrenCount => (_child != null) ? 1 : 0;

			protected override IVisual GetVisualChild(int index)
			{
				if (index != 0 || _child == null)
					throw new ArgumentOutOfRangeException(nameof(index));
				return _child;
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
		private ButtonsOverlay _overlay;

		[Null]
		private OverlayLayer _overlayLayer;

		private readonly CodeEditor _textEditor;

		protected IBrush ChunkBackgroundBrush;

		protected static readonly Pen _chunkBorderPen;

		protected static readonly IBrush _chunkBorderBrush;

		protected static readonly IBrush _chunkBackgroundBrush;

		protected static readonly IBrush _chunkBackgroundBrushDark;

		private Point _lastMousePosition;

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
			// Avalonia 画刷默认不可变，无需 Freeze()
		}

		public ChunkSelectionLayer(CodeEditor textEditor)
		{
			_textEditor = textEditor;
			IsHitTestVisible = false;
			_textEditor.PointerEntered += TextEditor_MouseEnter;
			_textEditor.PointerLeave += TextEditor_MouseLeave;
			_textEditor.PointerMoved += TextEditor_MouseMove;
			_textEditor.TextArea.SelectionChanged += TextArea_SelectionChanged;
			_textEditor.TextChanged += TextEditor_TextChanged;
			_textEditor.IsVisibleChanged += TextEditor_IsVisibleChanged;
			RefreshBrush();
			// 阶段 4 里程碑 4.7-a：WeakEventManagerBase/TextViewWeakEventManager → 直接事件订阅。
			// NotificationCenter 是单例，直接订阅有内存泄漏风险，阶段 6 改用 WeakEvent。
			_textEditor.TextArea.TextView.ScrollOffsetChanged += OnScrollOffsetChanged;
			NotificationCenter.Current.ApplicationThemeChanged += ApplicationThemeChanged;
		}

		private void OnScrollOffsetChanged(object sender, EventArgs e)
		{
			RefreshActiveChunk();
			InvalidateVisual();
		}

		protected abstract void RefreshActiveChunk();

		protected abstract Control CreateAdornerContent(TextEditor textEditor);

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
			if (_overlay == null)
			{
				// 阶段 4 里程碑 4.7-a：WPF AdornerLayer.GetAdornerLayer → Avalonia OverlayLayer.GetOverlayLayer。
				_overlayLayer = OverlayLayer.GetOverlayLayer(_textEditor) ?? OverlayLayer.GetOverlayLayer(_textEditor.TextArea);
				if (_overlayLayer == null)
				{
					return;
				}
				_overlay = new ButtonsOverlay();
				_overlay.Child = CreateAdornerContent(_textEditor);
				_overlayLayer.Children.Add(_overlay);
			}
			_overlay.Child.Measure(new Size(1000.0, 22.0));
			double width = _overlay.Child.DesiredSize.Width;
			_overlay.Margin = new Thickness(num3 - width, top, 0.0, 0.0);
		}

		protected void RemoveChunkAdorner()
		{
			if (_overlay != null)
			{
				_overlay.Child = null;
				_overlayLayer?.Children.Remove(_overlay);
				_overlayLayer = null;
				_overlay = null;
			}
		}

		private void TextEditor_MouseEnter(object sender, PointerEventArgs e)
		{
			_lastMousePosition = e.GetPosition(_textEditor);
			RefreshActiveChunk();
		}

		private void TextEditor_MouseLeave(object sender, PointerEventArgs e)
		{
			ContextMenu contextMenu = _textEditor.ContextMenu;
			if (contextMenu == null || !contextMenu.IsPointerOver)
			{
				ButtonsOverlay overlay = _overlay;
				if (overlay == null || overlay.InputHitTest(_lastMousePosition) == null)
				{
					ActiveChunk = null;
				}
			}
		}

		private void TextEditor_MouseMove(object sender, PointerEventArgs e)
		{
			_lastMousePosition = e.GetPosition(_textEditor);
			RefreshActiveChunk();
			e.Handled = true;
		}

		private void TextEditor_TextChanged(object sender, EventArgs e)
		{
			ActiveChunk = null;
		}

		private void TextEditor_IsVisibleChanged(object sender, EventArgs e)
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
			// 优先读资源（CustomColorsDialog 覆盖或主题字典），取不到回退到 light/dark 静态画刷。
			ChunkBackgroundBrush = TryFindColorBrush("ChunkSelection.BackgroundColor")
				?? (ForkPlusSettings.Default.Theme.IsDarkBase() ? _chunkBackgroundBrushDark : _chunkBackgroundBrush);
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
			Point position = _lastMousePosition;
			if (_textEditor.InputHitTest(position) == null)
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
