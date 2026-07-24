// 阶段 4.5：WPF → Avalonia 迁移。System.Windows.Window → Avalonia.Controls.Window。
// 阶段 5：Avalonia Window 无 Left/Top 属性（DIP），改用 Position（PixelPoint，设备像素），
// 需乘以 RenderScaling 做 DIP→像素转换。
using Avalonia;
using Avalonia.Controls;
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
			// 阶段 5：Window.Left/Top → Window.Position（PixelPoint）。
			double scale = window.RenderScaling;
			window.Position = new PixelPoint((int)(left * scale), (int)(top * scale));
			window.Width = num3;
			window.Height = num4;
			window.Show();
		}
	}
}
