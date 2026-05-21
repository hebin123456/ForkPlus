using System;
using System.Windows;

namespace ForkPlus.UI.Controls
{
	internal static class RectExtensions
	{
		public static Rect Inset(this Rect rect, double dx, double dy)
		{
			return new Rect(rect.X + dx, rect.Y + dy, Math.Max(rect.Width - 2.0 * dx, 0.0), Math.Max(rect.Height - 2.0 * dy, 0.0));
		}

		public static Tuple<Rect, Rect> DivideFromTop(this Rect rect, double distance)
		{
			double num = distance;
			if (num > rect.Height)
			{
				num = rect.Height;
			}
			Rect item = new Rect(rect.X, rect.Y, rect.Width, num);
			double bottom = item.Bottom;
			double num2 = rect.Height - distance;
			if (num2 < 0.0)
			{
				num2 = 0.0;
			}
			return new Tuple<Rect, Rect>(item2: new Rect(rect.X, bottom, rect.Width, num2), item1: item);
		}

		public static Tuple<Rect, Rect> DivideFromLeft(this Rect rect, double separatorX)
		{
			double num = separatorX;
			if (num > rect.Width)
			{
				num = rect.Width;
			}
			Rect item = new Rect(rect.X, rect.Y, num, rect.Height);
			double right = item.Right;
			double num2 = rect.Width - separatorX;
			if (num2 < 0.0)
			{
				num2 = 0.0;
			}
			return new Tuple<Rect, Rect>(item2: new Rect(right, rect.Y, num2, rect.Height), item1: item);
		}
	}
}
