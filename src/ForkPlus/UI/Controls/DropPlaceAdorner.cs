using Avalonia;
using ForkPlus.UI.WpfCompat;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class DropPlaceAdorner : Adorner
	{
		private static readonly Pen _pen = new Pen(global::ForkPlus.UI.Theme.AccentBrush, 2.0);

		private readonly DropPosition _dropPosition;

		private readonly global::Avalonia.Controls.ListBoxItem _listViewItem;

		public DropPlaceAdorner(global::Avalonia.Input.InputElement adornedElement, DropPosition position, global::Avalonia.Controls.ListBoxItem listViewItem)
			: base(adornedElement)
		{
			base.IsHitTestVisible = false;
			_dropPosition = position;
			_listViewItem = listViewItem;
		}

		public override void Render(DrawingContext context)
		{
			Rect rect = new Rect(base.AdornedElement.Bounds.Size);
			if (_dropPosition == DropPosition.Top)
			{
				context.DrawLine(_pen, rect.TopLeft, rect.TopRight);
			}
			else if (_dropPosition == DropPosition.Bottom)
			{
				context.DrawLine(_pen, rect.BottomLeft, rect.BottomRight);
			}
			else if (_dropPosition == DropPosition.Over)
			{
				// 修复（2026-09-10，"列表拖放卡死：Visual was invalidated during the render pass"）：
				// 原实现在这里直接赋 _listViewItem.Background——在 Avalonia 的 Render 里改属性会触发
				// 视觉失效，抛 "Visual was invalidated during the render pass"（UnobservedTaskException
				// 经 finalizer 重抛），UI 卡死。改为不在 Render 里改属性，而是直接用 DrawingContext
				// 画一个填充矩形（选中态背景色）覆盖在 ListBoxItem 上，视觉等价且不触发属性失效。
				IBrush background = global::ForkPlus.UI.Theme.RevisionList.ItemSelectedInactiveBackgroundBrush;
				if (background != null)
				{
					context.DrawRectangle(background, null, rect);
				}
			}
		}

		internal void ClearBackground()
		{
			if (_listViewItem.Background != global::ForkPlus.UI.Theme.RevisionList.ItemBackgroundBrush)
			{
				_listViewItem.Background = global::ForkPlus.UI.Theme.RevisionList.ItemBackgroundBrush;
			}
		}
	}
}
