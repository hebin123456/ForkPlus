using System;
using ForkPlus.UI.WpfCompat;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using ForkPlus.UI;
using ForkPlus.Settings;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Utils;
using ForkPlus.UI.Helpers;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls.Editor
{
	public abstract class ChunkSelectionLayer<TChunk> : global::Avalonia.Controls.Control, IWeakEventListener where TChunk : class
	{
		public class ButtonsAdorner : Adorner
		{
			private global::Avalonia.Controls.Control _child;

			public global::Avalonia.Controls.Control Child
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

			public ButtonsAdorner(global::Avalonia.Input.InputElement adornernedElement)
				: base(adornernedElement)
			{
				IsHitTestVisible = true;
				Tag = new Point();
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

		private Point? _lastPointerPosition;

		private Point? _lastAdornerOffset;

		private bool _adornerUpdatePending;

		private bool _adornerShowPending;

		private double _pendingAdornerTopPosition;

		// 修复（2026-09-05，"浮窗抖动 / 反复显示消失"）：
		// 跟踪鼠标是否在浮窗上，避免编辑器 MouseLeave 时误删浮窗。
		private bool _isPointerOverAdorner;

		// 延迟隐藏的取消令牌：鼠标离开编辑器后延迟隐藏，
		// 若期间鼠标进入浮窗则取消隐藏，消除抖动。
		private global::System.Threading.CancellationTokenSource _hideDelayCts;

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
		}

		public ChunkSelectionLayer(CodeEditor textEditor)
		{
			_textEditor = textEditor;
			base.IsHitTestVisible = false;
			_textEditor.PointerEntered += TextEditor_MouseEnter;
			_textEditor.PointerExited += TextEditor_MouseLeave;
			_textEditor.PointerMoved += TextEditor_MouseMove;
			_textEditor.TextArea.SelectionChanged += TextArea_SelectionChanged;
			_textEditor.TextChanged += TextEditor_TextChanged;
			// Migration note：WPF IsVisibleChanged 事件 → Avalonia 用 Visual.IsVisibleProperty 属性变更可观察流。
			_textEditor.GetPropertyChangedObservable(global::Avalonia.Visual.IsVisibleProperty).Subscribe(delegate(global::Avalonia.AvaloniaPropertyChangedEventArgs e) { TextEditor_IsVisibleChanged(_textEditor, e); });
			// 修复（2026-09-05，"浮窗消失不掉 / 切换界面后残留"）：
			// 监听自身从可视树剥离事件（layer 随 TextView 一同卸载时触发），
			// 彻底清理浮窗，避免 AdornerLayer 上残留"僵尸"装饰器。
			// 注意：不能监听 _textEditor.DetachedFromVisualTree——在某些场景
			// （如 headless 测试）下编辑器可能先短暂 attach/detach，导致误判。
			this.DetachedFromVisualTree += ChunkSelectionLayer_DetachedFromVisualTree;
			RefreshBrush();
			// Migration note：WPF WeakEventManagerBase<TMgr,TSrc>.AddListener → AvaloniaEdit 12 的 4 泛型 AddHandler(source, handler)。
			AvaloniaEdit.Rendering.TextViewWeakEventManager.ScrollOffsetChanged.AddHandler(_textEditor.TextArea.TextView, TextView_ScrollOffsetChanged);
			WeakEventManager<NotificationCenter, EventArgs<ThemeType>>.AddHandler(NotificationCenter.Current, "ApplicationThemeChanged", ApplicationThemeChanged);
		}

		private void TextView_ScrollOffsetChanged(object sender, EventArgs e)
		{
			RefreshActiveChunk();
			InvalidateVisual();
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

		protected abstract global::Avalonia.Controls.Control CreateAdornerContent(TextEditor textEditor);

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
			// Migration note：WPF Viewport.Height → AvaloniaEdit TextEditor.ViewportHeight（double 属性）。
			if (_textEditor.ViewportHeight > _textEditor.ExtentHeight)
			{
				if (_textEditor.TextArea.TextView.ScrollOffset.Y > 0.0)
				{
					return;
				}
			}
			else if (!_textEditor.IsVerticalOffsetWithinDocumentArea(_textEditor.TextArea.TextView.ScrollOffset.Y))
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
			// Bug 修复（2026-09-04，"选中一小块变更区域应该出来悬浮的变更和丢弃"）：
			// 原版 WPF 条件是 `_activeChunk != null || Selection.Length > 0`，迁移时把
			// 选区分支丢了——鼠标不在 hunk 上（键盘 Shift 选行 / 选完手移开）时选区
			// 存在也只走 RemoveChunkAdorner，悬浮 Stage/Discard 永远不出现。
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
			// 渲染期间（DrawChunk 调用链）不可同步操作 AdornerLayer/Measure，否则会
			// 与布局循环竞争甚至替换窗口内容；此处只记录最新位置并 Post 到 Render
			// 优先级执行（防抖：高频调用只渲染最后一次位置）。
			if (_adornerShowPending)
			{
				_pendingAdornerTopPosition = popupTopPosition;
				return;
			}
			_adornerShowPending = true;
			_pendingAdornerTopPosition = popupTopPosition;
			global::Avalonia.Threading.Dispatcher.UIThread.Post(delegate
			{
				if (!_adornerShowPending)
				{
					return;
				}
				_adornerShowPending = false;
				ShowChunkAdornerCore(_pendingAdornerTopPosition);
			}, global::Avalonia.Threading.DispatcherPriority.Render);
		}

		private void ShowChunkAdornerCore(double popupTopPosition)
		{
			double num = 15.0;
			double num2 = 20.0;
			num -= _textEditor.SearchBarHeight;
			double num3 = _textEditor.TextArea.TextView.Bounds.Width - num2;
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
				// 修复（2026-09-05，"浮窗抖动 / 反复显示消失"）：
				// 监听浮窗自身的鼠标进入/离开，与编辑器的鼠标状态配合：
				// 只要鼠标在编辑器或浮窗任一上，浮窗就保持显示；
				// 两者都离开后才延迟隐藏，消除"离开编辑器→消失→又出现"的抖动。
				_adorner.PointerEntered += Adorner_PointerEntered;
				_adorner.PointerExited += Adorner_PointerExited;
				_adornerLayer.Add(_adorner);
			}
			_adorner.Child.Measure(new Size(1000.0, 22.0));
			double width = _adorner.Child.DesiredSize.Width;
			Point offset = new Point(num3 - width, top);
			if (_lastAdornerOffset != offset)
			{
				_lastAdornerOffset = offset;
				_adorner.Tag = offset;
				QueueAdornerLayerUpdate();
			}
		}

		protected void RemoveChunkAdorner()
		{
			// 无条件取消 pending 显示请求：否则 Remove 后队列里的回调会重建已移除的 Adorner
			_adornerShowPending = false;
			// 取消延迟隐藏任务
			_hideDelayCts?.Cancel();
			_hideDelayCts?.Dispose();
			_hideDelayCts = null;
			_isPointerOverAdorner = false;
			if (_adorner != null)
			{
				// 解绑事件，避免内存泄漏
				_adorner.PointerEntered -= Adorner_PointerEntered;
				_adorner.PointerExited -= Adorner_PointerExited;
				_adorner.Child = null;
				_adornerLayer?.Remove(_adorner);
				_adornerLayer = null;
				_adorner = null;
				_lastAdornerOffset = null;
				_adornerUpdatePending = false;
			}
		}

		private void QueueAdornerLayerUpdate()
		{
			if (_adornerLayer == null || _adornerUpdatePending)
			{
				return;
			}
			_adornerUpdatePending = true;
			global::Avalonia.Threading.Dispatcher.UIThread.Post(delegate
			{
				_adornerUpdatePending = false;
				_adornerLayer?.Update();
			}, Avalonia.Threading.DispatcherPriority.Background);
		}

		private void TextEditor_MouseEnter(object sender, global::Avalonia.Input.PointerEventArgs e)
		{
			// 鼠标回到编辑器 → 取消待执行的隐藏
			_hideDelayCts?.Cancel();
			_lastPointerPosition = e.GetPosition(_textEditor);
			RefreshActiveChunk();
		}

		private void TextEditor_MouseLeave(object sender, global::Avalonia.Input.PointerEventArgs e)
		{
			ContextMenu contextMenu = _textEditor.ContextMenu;
			if (contextMenu == null || !contextMenu.IsPointerOver)
			{
				// 修复（2026-09-05，"浮窗抖动 / 反复显示消失"）：
				// 原代码直接判断鼠标不在 Adorner 上就 ActiveChunk=null，
				// 但 PointerExited 触发时鼠标可能刚离开编辑器、还没进入浮窗
				// （中间有 1~2 像素间隙或事件时序问题），导致浮窗立即消失，
				// 消失后鼠标又"落回"编辑器 → MouseEnter → 重新显示 → 抖动。
				//
				// 改为：鼠标在浮窗上 → 绝不隐藏；不在浮窗上 → 延迟 150ms 再隐藏，
				// 给鼠标从编辑器滑入浮窗预留时间，期间若进入浮窗则取消隐藏。
				if (_isPointerOverAdorner)
				{
					return;
				}
				ScheduleDelayedHide();
			}
			_lastPointerPosition = null;
		}

		/// <summary>
		/// 延迟隐藏浮窗：150ms 内若鼠标进入浮窗或回到编辑器则取消。
		/// </summary>
		private async void ScheduleDelayedHide()
		{
			_hideDelayCts?.Cancel();
			_hideDelayCts?.Dispose();
			var cts = new global::System.Threading.CancellationTokenSource();
			_hideDelayCts = cts;
			try
			{
				await global::System.Threading.Tasks.Task.Delay(150, cts.Token);
				// 延迟结束后再检查一次：鼠标在浮窗上则不隐藏
				if (!_isPointerOverAdorner)
				{
					ActiveChunk = null;
				}
			}
			catch (global::System.Threading.Tasks.TaskCanceledException)
			{
				// 被取消：什么都不做（浮窗保持显示）
			}
			finally
			{
				if (_hideDelayCts == cts)
				{
					_hideDelayCts = null;
				}
				cts.Dispose();
			}
		}

		private void Adorner_PointerEntered(object sender, global::Avalonia.Input.PointerEventArgs e)
		{
			_isPointerOverAdorner = true;
			// 鼠标进入浮窗 → 取消待执行的隐藏
			_hideDelayCts?.Cancel();
		}

		private void Adorner_PointerExited(object sender, global::Avalonia.Input.PointerEventArgs e)
		{
			_isPointerOverAdorner = false;
			// 鼠标离开浮窗 → 检查是否还在编辑器上，不在则延迟隐藏
			if (_lastPointerPosition == null)
			{
				ScheduleDelayedHide();
			}
		}

		private void TextEditor_MouseMove(object sender, global::Avalonia.Input.PointerEventArgs e)
		{
			_lastPointerPosition = e.GetPosition(_textEditor);
			RefreshActiveChunk();
		}

		private void TextEditor_TextChanged(object sender, EventArgs e)
		{
			ActiveChunk = null;
		}

		private void TextEditor_IsVisibleChanged(object sender, global::Avalonia.AvaloniaPropertyChangedEventArgs e)
		{
			if (!_textEditor.IsVisible)
			{
				RemoveChunkAdorner();
			}
		}

		private void ChunkSelectionLayer_DetachedFromVisualTree(object sender, global::Avalonia.VisualTreeAttachmentEventArgs e)
		{
			// 修复（2026-09-05，"浮窗消失不掉 / 切换界面后残留"）：
			// layer 从可视树剥离（如切换页面、关闭 Tab）时，必须彻底清理浮窗，
			// 否则 AdornerLayer 上的装饰器会成为"僵尸"——对应控件已不在但浮窗还在。
			// 注：ShowChunkAdornerCore 中 GetAdornerLayer 返回 null 会自然阻止重建，
			// 无需额外 _detached 标志守卫。
			RemoveChunkAdorner();
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
			Rect rect = new Rect(0.0, num, _textEditor.Bounds.Width, num2);
			DrawBorder(rect, drawingContext);
			// Bug 修复（2026-09-04，"选中一小块变更区域应该出来悬浮的变更和丢弃"）：
			// 原版 WPF 画完选区边框后把悬浮按钮定位到选区顶部（num=选区顶 + SearchBarHeight
			// 补偿，ShowChunkAdorner 内部再 -SearchBarHeight +15），迁移时该调用丢失，
			// 选区悬浮按钮既不出现也不跟随选区位置。
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
			return new Rect(0.0, num + 1.0, textView.Bounds.Width, num2 - 1.0);
		}

		[Null]
		protected TChunk GetChunkUnderMousePointer()
		{
			if (!_lastPointerPosition.HasValue)
			{
				return null;
			}
			Point position = _lastPointerPosition.GetValueOrDefault();
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
