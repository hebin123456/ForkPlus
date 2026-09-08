using Avalonia;
using ForkPlus.UI.Helpers;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

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
			// Migration note：WPF Window.Left/Top（DIP）→ Avalonia Window.Position（物理像素），按父窗口 RenderScaling 换算。
			double scale = parent.RenderScaling;
			window.Position = new global::Avalonia.PixelPoint((int)(left * scale), (int)(top * scale));
			window.Width = num3;
			window.Height = num4;
			window.Show();
		}
	}
}
