using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ForkPlus.UI;

namespace ForkPlus.UI.Controls
{
	// 阶段 4.5：WPF Adorner（装饰层）→ Avalonia 无内置 Adorner 等价物。
	// 策略：CustomAdorner 改为继承 ContentControl，作为普通控件添加到父 Panel.Children
	// 或通过 OverlayLayer 挂载。原 WPF Adorner 通过 AdornerLayer.GetAdornerLayer 查找装饰层，
	// Avalonia 中由调用方（EditableTextBlock）直接添加到父容器或 OverlayLayer。
	//
	// WPF Adorner 基类提供：
	// - GetVisualChild / AddVisualChild / RemoveVisualChild / VisualChildrenCount
	// - MeasureOverride / ArrangeOverride
	// Avalonia ContentControl 已内置 Content 子控件管理，无需手动 AddVisualChild。
	public class CustomAdorner : ContentControl
	{
		private bool _centeredHorizontallyInParent;

		public CustomAdorner(Control adornedElement, bool centeredHorizontally = false)
		{
			// 阶段 4.5：原 WPF Adorner 构造接收被装饰元素；Avalonia 无 Adorner 基类，
			// 保留参数以兼容调用方，但不再持有引用（布局由调用方挂载位置决定）。
			_centeredHorizontallyInParent = centeredHorizontally;
			IsHitTestVisible = true;
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			if (Content == null)
			{
				return default;
			}
			// 阶段 4.5：ContentControl.MeasureOverride 已处理 Content 测量，
			// 此处仅补充最小宽度限制（原 WPF 逻辑：宽度 < 40 时扩展到 40）。
			Size result = base.MeasureOverride(availableSize);
			if (result.Width < 40.0)
			{
				result = new Size(40.0, result.Height);
			}
			return result;
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			// 阶段 4.5：原 WPF 逻辑根据 HorizontalAlignment + _centeredHorizontallyInParent
			// 调整 Child 的 Arrange 位置。Avalonia ContentControl 通过 HorizontalAlignment
			// 属性自动处理对齐；此处保留居中偏移逻辑。
			if (_centeredHorizontallyInParent && HorizontalAlignment == HorizontalAlignment.Center)
			{
				// 居中时 Avalonia 自动处理；无需手动偏移。
			}
			return base.ArrangeOverride(finalSize);
		}
	}
}
