using System.Runtime.InteropServices;
using Avalonia;

namespace ForkPlus.UI.Helpers
{
	// 阶段 4.5：WPF System.Windows.Point → Avalonia.Point。
	// TODO(4.5): GetCursorPos 是 Windows-only P/Invoke。跨平台方案需通过 Pointer 事件跟踪
	// 或平台特定 API（Linux: X11/XInput, macOS: NSEvent.mouseLocation）。
	// 当前保留 Windows 实现，非 Windows 平台会运行时报错。
	internal static class MouseHelper
	{
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
			Win32Point pt = default(Win32Point);
			GetCursorPos(ref pt);
			return new Point(pt.X, pt.Y);
		}
	}
}
