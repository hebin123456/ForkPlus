using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class CenteredDockPanel : DockPanel
	{
		private Size[] _sizes;

		// Migration note：WPF UIElementCollection/InternalChildren → Avalonia Panel.Children（Controls 集合，元素类型 Control）。
		protected override Size MeasureOverride(Size constraint)
		{
			global::Avalonia.Controls.Controls internalChildren = base.Children; // Migration note：Controls 与命名空间 ForkPlus.UI.Controls 冲突，用全限定名
			double val = 0.0;
			double val2 = 0.0;
			double num = 0.0;
			double num2 = 0.0;
			if (_sizes == null || _sizes.Length != internalChildren.Count)
			{
				_sizes = new Size[internalChildren.Count];
			}
			int i = 0;
			for (int count = internalChildren.Count; i < count; i++)
			{
				global::Avalonia.Controls.Control uIElement = internalChildren[i];
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

		// Migration note：WPF Rect 为可变结构（finalRect.X = ... 直接赋值），
		// Avalonia Rect 不可变，改用局部变量累积后 new Rect(...) 重建。
		protected override Size ArrangeOverride(Size arrangeSize)
		{
			global::Avalonia.Controls.Controls internalChildren = base.Children; // Migration note：Controls 与命名空间 ForkPlus.UI.Controls 冲突，用全限定名
			int count = internalChildren.Count;
			int num = count - (base.LastChildFill ? 1 : 0);
			double num2 = 0.0;
			double num3 = 0.0;
			double num4 = 0.0;
			double num5 = 0.0;
			for (int i = 0; i < count; i++)
			{
				global::Avalonia.Controls.Control uIElement = internalChildren[i];
				if (uIElement == null)
				{
					continue;
				}
				Size desiredSize = uIElement.DesiredSize;
				double rectX = num2;
				double rectY = num3;
				double rectWidth = Math.Max(0.0, arrangeSize.Width - (num2 + num4));
				double rectHeight = Math.Max(0.0, arrangeSize.Height - (num3 + num5));
				if (i < num)
				{
					switch (DockPanel.GetDock(uIElement))
					{
					case Dock.Left:
						num2 += desiredSize.Width;
						rectWidth = desiredSize.Width;
						break;
					case Dock.Right:
						num4 += desiredSize.Width;
						rectX = Math.Max(0.0, arrangeSize.Width - num4);
						rectWidth = desiredSize.Width;
						break;
					case Dock.Top:
						num3 += desiredSize.Height;
						rectHeight = desiredSize.Height;
						break;
					case Dock.Bottom:
						num5 += desiredSize.Height;
						rectY = Math.Max(0.0, arrangeSize.Height - num5);
						rectHeight = desiredSize.Height;
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
					rectX = num6;
					rectWidth = num6 + desiredSize.Width;
				}
				uIElement.Arrange(new Rect(rectX, rectY, rectWidth, rectHeight));
			}
			return arrangeSize;
		}
	}
}
