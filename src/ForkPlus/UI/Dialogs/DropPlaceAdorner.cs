// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows.* → using Avalonia.*
// - WPF Adorner → Avalonia Control（参考 DragAdorner/DropPlaceAdorner 在 Controls/ 下同名实现）
// - OnRender(DrawingContext) → Render(DrawingContext)
// - AdornedElement.RenderSize → 缓存 _adornedSize（Bounds.Size）
// - IsHitTestVisible = false（API 兼容）
// - 通过 AttachTo/DetachFrom 封装 OverlayLayer 挂载/卸载
// 注意：UI/Controls/DropPlaceAdorner.cs 已存在迁移后的同名实现，本类为 Dialogs 命名空间下的旧副本。
// 保留以维持编译兼容性；阶段 5 应统一删除并改用 Controls.DropPlaceAdorner。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace ForkPlus.UI.Dialogs
{
	public class DropPlaceAdorner : Control
	{
		private static readonly Pen _pen = new Pen(Theme.AccentBrush, 2.0);

		private DropPosition _dropPosition;

		private Size _adornedSize;

		public DropPlaceAdorner(Control adornedElement, DropPosition position)
		{
			_adornedSize = adornedElement.Bounds.Size;
			IsHitTestVisible = false;
			_dropPosition = position;
		}

		public override void Render(DrawingContext context)
		{
			Rect rect = new Rect(_adornedSize);
			if (_dropPosition == DropPosition.Top)
			{
				context.DrawLine(_pen, rect.TopLeft, rect.TopRight);
			}
			else if (_dropPosition == DropPosition.Bottom)
			{
				context.DrawLine(_pen, rect.BottomLeft, rect.BottomRight);
			}
		}

		// 阶段 4.5：封装 OverlayLayer 挂载/卸载，替代 WPF AdornerLayer.Add/Remove。
		public void AttachTo(Control parent)
		{
			OverlayLayer overlay = OverlayLayer.GetOverlayLayer(parent);
			overlay?.Children.Add(this);
		}

		public void DetachFrom(Control parent)
		{
			OverlayLayer overlay = OverlayLayer.GetOverlayLayer(parent);
			overlay?.Children.Remove(this);
		}
	}
}
