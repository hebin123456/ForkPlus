using System;
using System.Windows;
using System.Windows.Controls;

namespace ForkPlus.UI.Controls
{
	public class CenteredDockPanel : DockPanel
	{
		private Size[] _sizes;

		protected override Size MeasureOverride(Size constraint)
		{
			UIElementCollection internalChildren = base.InternalChildren;
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
				UIElement uIElement = internalChildren[i];
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
			UIElementCollection internalChildren = base.InternalChildren;
			int count = internalChildren.Count;
			int num = count - (base.LastChildFill ? 1 : 0);
			double num2 = 0.0;
			double num3 = 0.0;
			double num4 = 0.0;
			double num5 = 0.0;
			for (int i = 0; i < count; i++)
			{
				UIElement uIElement = internalChildren[i];
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
						finalRect.Width = desiredSize.Width;
						break;
					case Dock.Right:
						num4 += desiredSize.Width;
						finalRect.X = Math.Max(0.0, arrangeSize.Width - num4);
						finalRect.Width = desiredSize.Width;
						break;
					case Dock.Top:
						num3 += desiredSize.Height;
						finalRect.Height = desiredSize.Height;
						break;
					case Dock.Bottom:
						num5 += desiredSize.Height;
						finalRect.Y = Math.Max(0.0, arrangeSize.Height - num5);
						finalRect.Height = desiredSize.Height;
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
					finalRect.X = num6;
					finalRect.Width = num6 + desiredSize.Width;
				}
				uIElement.Arrange(finalRect);
			}
			return arrangeSize;
		}
	}
}
