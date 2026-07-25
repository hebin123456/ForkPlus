// 阶段 5：Avalonia 11.3 标准包无 Hyperlink 类（WPF System.Windows.Documents.Hyperlink）。
// Avalonia 的等价控件是 HyperlinkButton，但它继承 Button 而非 Span，不能用于 InlineCollection。
// 富文本内的可点击超链接需要 Avalonia Pro（付费）的 RichHyperlink。
//
// 本兼容类继承 Avalonia Span（Inline），提供 NavigateUri 属性 + Click 事件，
// 让 WPF 迁移代码中 `new Hyperlink()` / `<Hyperlink>` / `hyperlink.Click += ...`
// 等写法无需修改即可通过编译并运行。
//
// 放在命名空间 Avalonia.Controls.Documents：与 WPF System.Windows.Documents.Hyperlink
// 命名空间风格一致，现有 `using Avalonia.Controls.Documents;` 直接解析到本类型。
//
// 点击实现说明（阶段 5 补丁）：
// Avalonia 11.3 的 Inline/Span 不继承 InputElement（见 Avalonia issue #10186，计划 13.0 解决），
// 没有 OnPointerPressed/OnPointerReleased/OnPointerEntered/OnPointerExited/Cursor 可用，
// 也无法直接 hit-test。本类在 OnAttachedToLogicalTree 中沿逻辑树找到最近的宿主 InputElement
// （通常是 TextBlock，也可能是 Label 等 ContentControl），订阅其指针事件来模拟：
//   - 按下时若命中本 Hyperlink 文本区域则记录起点并捕获指针
//   - 释放时若无大幅移动（非拖拽）则触发 RaiseClick()
//   - PointerMoved/PointerExited 维护 :pointerover 伪类与 Hand 光标
// 命中测试优先使用宿主 TextBlock.TextLayout.HitTestTextRange 精确判断（支持同一 TextBlock 多 Hyperlink）；
// 宿主非 TextBlock 时退化为命中宿主 bounds（适用于 Label + Hyperlink + TextBlock 等模式）。
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using ForkPlus.UI;

namespace Avalonia.Controls.Documents
{
	/// <summary>
	/// WPF Hyperlink 的 Avalonia 兼容占位。继承 Span 使其可作为 Inline 加入 InlineCollection。
	/// 点击时通过 Click 事件通知宿主，由宿主调用 OpenInBrowser 等完成导航。
	/// </summary>
	public class Hyperlink : Span
	{
		/// <summary>导航目标 URI。点击时由宿主读取并打开。</summary>
		public static readonly StyledProperty<Uri> NavigateUriProperty =
			AvaloniaProperty.Register<Hyperlink, Uri>(nameof(NavigateUri));

		/// <summary>导航目标 URI。点击时由宿主读取并打开。</summary>
		public Uri NavigateUri
		{
			get => GetValue(NavigateUriProperty);
			set => SetValue(NavigateUriProperty, value);
		}

		/// <summary>WPF Hyperlink.RequestNavigate 的兼容事件。点击时触发。</summary>
		public event EventHandler<RequestNavigateEventArgs> RequestNavigate;

		/// <summary>WPF Hyperlink.Click 的兼容事件（Avalonia Span 无 Click，本类模拟）。</summary>
		public event EventHandler<RoutedEventArgs> Click;

		// 阶段 5：宿主 InputElement（通常是 TextBlock），订阅其指针事件以模拟点击/悬停。
		private InputElement _host;
		// 鼠标按下时的位置（相对于 _host），用于判断释放时是否构成点击（非拖拽）。
		private Point _pressedPosition;
		private bool _hasPressedPosition;
		// 当前指针是否悬停在本 Hyperlink 文本区域上方，用于驱动 :pointerover 伪类与 Hand 光标。
		private bool _isPointerOver;
		// 进入悬停前宿主的原始 Cursor，离开时恢复。
		private Cursor _previousHostCursor;

		public Hyperlink()
		{
		}

		public Hyperlink(Inline inline)
		{
			if (inline != null)
			{
				Inlines.Add(inline);
			}
		}

		protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
		{
			base.OnAttachedToLogicalTree(e);
			AttachHost();
		}

		protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
		{
			DetachHost();
			base.OnDetachedFromLogicalTree(e);
		}

		private void AttachHost()
		{
			DetachHost();
			// 沿逻辑树向上找到最近的 InputElement（通常是 TextBlock，也可能是 Label 等 ContentControl）。
			// LogicalParent 是 ILogical 的显式接口实现，需要先把 this 转为 ILogical。
			ILogical ancestor = ((ILogical)this).LogicalParent;
			while (ancestor != null)
			{
				if (ancestor is InputElement ie)
				{
					_host = ie;
					_host.PointerPressed += Host_PointerPressed;
					_host.PointerReleased += Host_PointerReleased;
					_host.PointerMoved += Host_PointerMoved;
					_host.PointerExited += Host_PointerExited;
					break;
				}
				ancestor = ancestor.LogicalParent;
			}
		}

		private void DetachHost()
		{
			if (_host != null)
			{
				_host.PointerPressed -= Host_PointerPressed;
				_host.PointerReleased -= Host_PointerReleased;
				_host.PointerMoved -= Host_PointerMoved;
				_host.PointerExited -= Host_PointerExited;
				if (_isPointerOver)
				{
					_host.Cursor = _previousHostCursor;
				}
				_host = null;
			}
			_hasPressedPosition = false;
			_isPointerOver = false;
			_previousHostCursor = null;
			PseudoClasses.Set(":pointerover", false);
		}

		private void Host_PointerPressed(object sender, PointerPressedEventArgs e)
		{
			if (!IsPointerOverThis(e))
			{
				return;
			}
			_pressedPosition = e.GetPosition(_host);
			_hasPressedPosition = true;
			// 捕获指针，确保即使指针离开 _host 也能收到 release 事件，便于完成点击判定。
			e.Pointer.Capture(_host);
		}

		private void Host_PointerReleased(object sender, PointerReleasedEventArgs e)
		{
			try
			{
				if (_hasPressedPosition && _host != null && e.Pointer.Captured == _host)
				{
					var released = e.GetPosition(_host);
					var delta = released - _pressedPosition;
					// 阈值内视为点击而非拖拽，触发 RaiseClick。
					const double dragThreshold = 3.0;
					if (Math.Abs(delta.X) <= dragThreshold && Math.Abs(delta.Y) <= dragThreshold)
					{
						RaiseClick();
					}
				}
			}
			finally
			{
				_hasPressedPosition = false;
				if (_host != null && e.Pointer.Captured == _host)
				{
					e.Pointer.Capture(null);
				}
			}
		}

		private void Host_PointerMoved(object sender, PointerEventArgs e)
		{
			UpdateHoverState(IsPointerOverThis(e));
		}

		private void Host_PointerExited(object sender, PointerEventArgs e)
		{
			UpdateHoverState(false);
		}

		private void UpdateHoverState(bool isOver)
		{
			if (isOver == _isPointerOver || _host == null)
			{
				return;
			}
			_isPointerOver = isOver;
			// Span 不是 InputElement，:pointerover 伪类无法由 Avalonia 输入系统自动维护，
			// 这里手动驱动，让 XAML 中 Hyperlink:pointerover 样式选择器生效。
			PseudoClasses.Set(":pointerover", isOver);
			if (isOver)
			{
				_previousHostCursor = _host.Cursor;
				_host.Cursor = new Cursor(StandardCursorType.Hand);
			}
			else
			{
				_host.Cursor = _previousHostCursor;
				_previousHostCursor = null;
			}
		}

		/// <summary>
		/// 判断指针是否位于本 Hyperlink 文本区域上方。
		/// 宿主为 TextBlock 时使用 TextLayout 精确命中（支持同一 TextBlock 内多个 Hyperlink）；
		/// 否则退化为命中宿主 bounds（适用于 Label + Hyperlink + TextBlock 等模式）。
		/// </summary>
		private bool IsPointerOverThis(PointerEventArgs e)
		{
			if (_host == null)
			{
				return false;
			}
			if (_host is TextBlock textBlock && textBlock.Inlines != null)
			{
				var (start, length) = GetCharacterRangeInHost(textBlock);
				if (length <= 0)
				{
					return false;
				}
				var point = e.GetPosition(textBlock);
				var layoutPoint = ToTextLayoutCoordinates(textBlock, point);
				foreach (var rect in textBlock.TextLayout.HitTestTextRange(start, length))
				{
					if (rect.Contains(layoutPoint))
					{
						return true;
					}
				}
				return false;
			}
			// 退化命中：宿主不是 TextBlock，整个宿主范围视为命中区域。
			var localPoint = e.GetPosition(_host);
			return new Rect(_host.Bounds.Size).Contains(localPoint);
		}

		// 与 TextBlock.RenderCore 的渲染原点对齐：TextLayout 在 TextBlock 局部坐标系下的偏移为
		// (padding.Left, padding.Top + VerticalAlignment 调整)。HitTestTextRange 返回的 Rect 在
		// TextLayout 坐标系下，需要把指针位置转换到同一坐标系。
		private static Point ToTextLayoutCoordinates(TextBlock textBlock, Point point)
		{
			var padding = textBlock.Padding;
			double top = padding.Top;
			double textHeight = textBlock.TextLayout.Height;
			if (textBlock.Bounds.Height < textHeight)
			{
				switch (textBlock.VerticalAlignment)
				{
					case VerticalAlignment.Center:
						top += (textBlock.Bounds.Height - textHeight) / 2;
						break;
					case VerticalAlignment.Bottom:
						top += (textBlock.Bounds.Height - textHeight);
						break;
				}
			}
			return new Point(point.X - padding.Left, point.Y - top);
		}

		/// <summary>计算本 Hyperlink 在宿主 TextBlock 完整文本中的字符范围 [start, start+length)。</summary>
		private (int start, int length) GetCharacterRangeInHost(TextBlock textBlock)
		{
			int start = 0;
			foreach (var inline in textBlock.Inlines)
			{
				if (ReferenceEquals(inline, this))
				{
					return (start, GetInlineTextLength(inline));
				}
				start += GetInlineTextLength(inline);
			}
			return (0, 0);
		}

		private static int GetInlineTextLength(Inline inline)
		{
			if (inline is Run run && run.Text != null)
			{
				return run.Text.Length;
			}
			if (inline is Span span)
			{
				int total = 0;
				foreach (var child in span.Inlines)
				{
					total += GetInlineTextLength(child);
				}
				return total;
			}
			// InlineUIContainer 等不含纯文本字符，长度记 0。
			return 0;
		}

		/// <summary>模拟点击：触发 Click 与 RequestNavigate 事件。</summary>
		public void RaiseClick()
		{
			var clickArgs = new RoutedEventArgs();
			Click?.Invoke(this, clickArgs);
			if (!clickArgs.Handled && NavigateUri != null)
			{
				var navArgs = new RequestNavigateEventArgs { Uri = NavigateUri };
				RequestNavigate?.Invoke(this, navArgs);
			}
		}
	}
}
