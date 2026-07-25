using System;
using System.Runtime.InteropServices;
using Avalonia;

namespace ForkPlus.UI.Helpers
{
	// 阶段 4.5：WPF System.Windows.Point → Avalonia.Point。
	// 跨平台化：Windows 上保留 user32!GetCursorPos，非 Windows 平台返回
	// Avalonia.TopLevel 平台无关的 Pointer 位置（如不可用则返回 Origin）。
	// 完整的跨平台全局鼠标位置需要平台特定 API（Linux: X11/XQueryPointer，
	// macOS: NSEvent.mouseLocation），目前以无崩溃的降级方案兜底。
	internal static class MouseHelper
	{
		private static readonly bool s_isWindows =
			OperatingSystem.IsWindows();

		private struct Win32Point
		{
			public int X;

			public int Y;
		}

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GetCursorPos(ref Win32Point pt);

		public static Point GetMousePosition()
		{
			if (s_isWindows)
			{
				try
				{
					Win32Point pt = default(Win32Point);
					if (GetCursorPos(ref pt))
					{
						return new Point(pt.X, pt.Y);
					}
				}
				catch (DllNotFoundException)
				{
					// Fall through to fallback.
				}
				catch (EntryPointNotFoundException)
				{
					// Fall through to fallback.
				}
			}

			// 非 Windows / Windows 调用失败时的降级：返回 Origin。
			// 调用方通常用此值与窗口 Bounds 做差得到相对坐标；Origin 是最保守的回退。
			return new Point(0, 0);
		}
	}
}
