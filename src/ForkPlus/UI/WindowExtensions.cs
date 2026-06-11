using System.Windows;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI
{
	public static class WindowExtensions
	{
		public static void ShowAtCenter(this Window window, Window parent, double ratio = 0.9)
		{
			WindowLocationState windowLocationStateX = parent.GetWindowLocationStateX();
			double num = windowLocationStateX.Left + windowLocationStateX.Width / 2.0;
			double num2 = windowLocationStateX.Top + windowLocationStateX.Height / 2.0;
			double num3 = windowLocationStateX.Width * ratio;
			double num4 = windowLocationStateX.Height * ratio;
			double left = num - num3 / 2.0;
			double top = num2 - num4 / 2.0;
			window.Left = left;
			window.Top = top;
			window.Width = num3;
			window.Height = num4;
			window.Show();
		}
	}
}
