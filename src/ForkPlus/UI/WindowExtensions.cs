// 阶段 4.5：WPF → Avalonia 迁移。System.Windows.Window → Avalonia.Controls.Window。
// 阶段 5：Avalonia Window 无 Left/Top 属性（DIP），改用 Position（PixelPoint，设备像素），
// 需乘以 RenderScaling 做 DIP→像素转换。
using System;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI
{
	public static class WindowExtensions
	{
		// ⚠ 临时桥接扩展 ─ 阶段 4.5 编译过渡用。
		// Avalonia 11 的 WindowBase.Owner 属性 setter 是 protected，无法从外部类设置。
		// WPF 允许从任意上下文设置 Owner。此扩展方法通过反射绕过访问限制，
		// 让原 WPF 代码 someWindow.Owner = this; 改为 someWindow.SetOwner(this); 即可工作。
		/// <summary>
		/// WPF Window.Owner = value 兼容扩展。
		/// Avalonia WindowBase.Owner setter 是 protected，用反射绕过访问限制。
		/// </summary>
		public static void SetOwner(this Window window, Window owner)
		{
			if (window == null) return;
			// Avalonia Window.Show(Window owner) 重载会在内部设置 Owner。
			// 但如果在 Show 之后再设置，需要直接反射赋值。
			PropertyInfo prop = typeof(Window).GetProperty("Owner",
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (prop != null && prop.CanWrite)
			{
				prop.SetValue(window, owner);
			}
		}

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
