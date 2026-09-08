using ForkPlus.UI.Helpers;
using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Helpers
{
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

		// Migration note：GetCursorPos（user32.dll）是 Windows 专属，Unix 抛 DllNotFoundException
		// （活路径实证：Treemap.UpdateTooltipPosition 鼠标悬停提示、DragAndDropListViewItem.
		// OnGiveFeedback 拖放跟随）。曾尝试订阅 Avalonia InputManager.Process 跟踪指针事件，
		// 但反射实证（ref 程序集）：InputManager 类 internal、RawInputEventArgs.Root 与
		// RawPointerEventArgs.Position 均 protected，公开面拿不到。
		// 改用 X11 XQueryPointer（libX11）：与 GetCursorPos 语义完全等价的硬件级光标查询，
		// 独立连接 Display（XOpenDisplay(null) 读 $DISPLAY），不依赖 Avalonia 内部状态，
		// 拖放进行中（指针事件被拖放循环接管）时依然有效——订阅方案覆盖不了的场景。
		// rootX/rootY 即根窗口坐标 = 屏幕像素坐标，与 PixelPoint 语义一致（调用点 Treemap
		// 已按 1:1 缩放转换）。macOS 暂返回 (0,0)（后续可补 NSEvent.mouseLocation）。
		[DllImport("libX11.so.6", EntryPoint = "XOpenDisplay")]
		private static extern IntPtr XOpenDisplay(IntPtr display);

		[DllImport("libX11.so.6", EntryPoint = "XDefaultRootWindow")]
		private static extern IntPtr XDefaultRootWindow(IntPtr display);

		[DllImport("libX11.so.6", EntryPoint = "XQueryPointer")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool XQueryPointer(IntPtr display, IntPtr window, out IntPtr root, out IntPtr child, out int rootX, out int rootY, out int winX, out int winY, out uint mask);

		private static bool _x11Initialized;

		private static IntPtr _x11Display;

		private static IntPtr _x11RootWindow;

		public static Point GetMousePosition()
		{
			if (!OperatingSystem.IsWindows())
			{
				if (TryGetX11MousePosition(out Point point))
				{
					return point;
				}
				return default(Point);
			}
			Win32Point pt = default(Win32Point);
			GetCursorPos(ref pt);
			return new Point(pt.X, pt.Y);
		}

		private static bool TryGetX11MousePosition(out Point point)
		{
			point = default(Point);
			if (!_x11Initialized)
			{
				_x11Initialized = true;
				try
				{
					_x11Display = XOpenDisplay(IntPtr.Zero);
					if (_x11Display != IntPtr.Zero)
					{
						_x11RootWindow = XDefaultRootWindow(_x11Display);
					}
				}
				catch (DllNotFoundException)
				{
					_x11Display = IntPtr.Zero;
				}
				catch (Exception)
				{
					_x11Display = IntPtr.Zero;
				}
			}
			if (_x11Display == IntPtr.Zero || _x11RootWindow == IntPtr.Zero)
			{
				return false;
			}
			try
			{
				if (XQueryPointer(_x11Display, _x11RootWindow, out _, out _, out int rootX, out int rootY, out _, out _, out _))
				{
					point = new Point(rootX, rootY);
					return true;
				}
			}
			catch (DllNotFoundException)
			{
			}
			catch (Exception)
			{
			}
			return false;
		}
	}
}
