using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace ForkPlus.UI.Controls
{
	// 阶段 4.5：WPF DockPanel.InternalChildren (UIElementCollection) → Avalonia DockPanel.Children (IList<Control>)。
	// WPF DockPanel.GetDock(UIElement) → Avalonia DockPanel.GetDock(Control)（API 兼容）。
	// WPF UIElement.Measure/Arrange/DesiredSize → Avalonia Control.Measure/Arrange/DesiredSize。
	// WPF Layoutable.MeasureOverride/ArrangeOverride(Size) → Avalonia 同名签名（一致）。
	// 注意：Avalonia DockPanel 的 LastChildFill 属性存在；Children 返回 IList<IVisual>，
	// 但实际元素是 Control，通过类型转换访问 DesiredSize/Arrange 等。
	public class CenteredDockPanel : DockPanel
	{
		private Size[] _sizes;

		protected override Size MeasureOverride(Size constraint)
		{
			var internalChildren = base.Children;
			double val = 0.0;
			double val2 = 0.0;
			double num = 0.0;
			double num2 = 0.0;
			int count = internalChildren.Count;
			if (_sizes == null || _sizes.Length != count)
			{
				_sizes = new Size[count];
			}
			int i = 0;
			for (; i < count; i++)
			{
				Control uIElement = internalChildren[i] as Control;
				if (uIElement != null)
				{
					Size availableSize = new Size(Math.Max(0.0, constraint.Width - num), Math.Max(0.0, constraint.Height - num2));
					uIElement.Measure(constraint);
					_sizes[i] = uIElement.DesiredSize;
					uIElement.Measure(availableSize);
					Size desiredSize = uIElement.DesiredSize;
					switch (DockPanel.GetDock(uIElement))
					{
					case Dock.Left:
					case Dock.Right:
						val2 = Math.Max(val2, num2 + desiredSize.Height);
						num += desiredSize.Width;
						break;
					case Dock.Top:
					case Dock.Bottom:
						val = Math.Max(val, num + desiredSize.Width);
						num2 += desiredSize.Height;
						break;
					}
				}
			}
			val = Math.Max(val, num);
			val2 = Math.Max(val2, num2);
			return new Size(val, val2);
		}

		protected override Size ArrangeOverride(Size arrangeSize)
		{
			var internalChildren = base.Children;
			int count = internalChildren.Count;
			int num = count - (base.LastChildFill ? 1 : 0);
			double num2 = 0.0;
			double num3 = 0.0;
			double num4 = 0.0;
			double num5 = 0.0;
			for (int i = 0; i < count; i++)
			{
				Control uIElement = internalChildren[i] as Control;
				if (uIElement == null)
				{
					continue;
				}
				Size desiredSize = uIElement.DesiredSize;
				Rect finalRect = new Rect(num2, num3, Math.Max(0.0, arrangeSize.Width - (num2 + num4)), Math.Max(0.0, arrangeSize.Height - (num3 + num5)));
				if (i < num)
				{
					switch (DockPanel.GetDock(uIElement))
					{
					case Dock.Left:
						num2 += desiredSize.Width;
						finalRect = finalRect.WithWidth(desiredSize.Width);
						break;
					case Dock.Right:
						num4 += desiredSize.Width;
						finalRect = finalRect.WithX(Math.Max(0.0, arrangeSize.Width - num4)).WithWidth(desiredSize.Width);
						break;
					case Dock.Top:
						num3 += desiredSize.Height;
						finalRect = finalRect.WithHeight(desiredSize.Height);
						break;
					case Dock.Bottom:
						num5 += desiredSize.Height;
						finalRect = finalRect.WithY(Math.Max(0.0, arrangeSize.Height - num5)).WithHeight(desiredSize.Height);
						break;
					}
				}
				else
				{
					double num6 = (arrangeSize.Width - desiredSize.Width) / 2.0;
					double num7 = num6 + desiredSize.Width;
					num6 = Math.Max(num6, num2);
					if (num7 > arrangeSize.Width - num4)
					{
						double num8 = num7 - (arrangeSize.Width - num4);
						num6 -= num8;
					}
					if (desiredSize.Width < _sizes[i].Width)
					{
						num6 = num2;
					}
					finalRect = finalRect.WithX(num6).WithWidth(desiredSize.Width);
				}
				uIElement.Arrange(finalRect);
			}
			return arrangeSize;
		}
	}
}
