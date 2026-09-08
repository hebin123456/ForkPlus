using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using ForkPlus.Settings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Helpers
{
	public static class WindowLocationStateExtensions
	{
		private struct DisplayScale
		{
			public float X;

			public float Y;

			public DisplayScale(float x, float y)
			{
				X = x;
				Y = y;
			}
		}

		[Serializable]
		private struct Point
		{
			public int X;

			public int Y;

			public Point(int x, int y)
			{
				X = x;
				Y = y;
			}
		}

		[Serializable]
		private struct Rect
		{
			public int Left;

			public int Top;

			public int Right;

			public int Bottom;

			public Rect(int left, int top, int right, int bottom)
			{
				Left = left;
				Top = top;
				Right = right;
				Bottom = bottom;
			}
		}

		[Serializable]
		private struct WindowPlacement
		{
			public int Length;

			public int Flags;

			public int ShowCmd;

			public Point MinPosition;

			public Point MaxPosition;

			public Rect normalPosition;
		}

		private struct MonitorInfo
		{
			public uint cbSize;

			public Rect rcMonitor;

			public Rect rcWork;

			public uint dwFlags;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct MinMaxInfo
		{
			public Point ptReserved;

			public Point ptMaxSize;

			public Point ptMaxPosition;

			public Point ptMinTrackSize;

			public Point ptMaxTrackSize;
		}

		private struct AppBarData
		{
			public uint cbSize;

			public IntPtr hWnd;

			public uint uCallbackMessage;

			public uint uEdge;

			public Rect rc;

			public int lParam;
		}

		private enum GetSystemMetricsIndex
		{
			SM_CXSCREEN = 0,
			SM_CYSCREEN = 1,
			SM_CXVIRTUALSCREEN = 78,
			SM_CYVIRTUALSCREEN = 79,
			SM_CMONITORS = 80,
			SM_XVIRTUALSCREEN = 76,
			SM_YVIRTUALSCREEN = 77
		}

		private enum GetDeviceCapsIndex
		{
			LOGPIXELSX = 88,
			LOGPIXELSY = 90
		}

		[DllImport("user32.dll")]
		private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GetWindowPlacement(IntPtr hWnd, ref WindowPlacement lpwndpl);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WindowPlacement lpwndpl);

		[DllImport("shell32.dll", CallingConvention = CallingConvention.StdCall)]
		private static extern uint SHAppBarMessage(uint dwMessage, ref AppBarData pData);

		[DllImport("user32.dll")]
		private static extern int GetSystemMetrics(GetSystemMetricsIndex nIndex);

		[DllImport("gdi32.dll")]
		private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

		[DllImport("user32.dll")]
		private static extern IntPtr GetDC(IntPtr hWnd);

		[DllImport("user32.dll")]
		private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

		// Migration note：Win32 GetWindowPlacement/SetWindowPlacement 在 Linux/macOS 抛 DllNotFoundException。
		// Unix 路径改用 Avalonia 原生 API（Position 物理像素 + Width/Height/Bounds DIP）。
		// Avalonia 无 RestoreBounds API（12.1.1 实证），用 ConditionalWeakTable 缓存"正常态"边界：
		// 最大化/最小化时取缓存（等价 Win32 placement.normalPosition 还原矩形）。
		private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Window, WindowLocationState> LastNormalBounds = new System.Runtime.CompilerServices.ConditionalWeakTable<Window, WindowLocationState>();

		public static void SetWindowLocationState(this Window window, WindowLocationState state)
		{
			if (DesignTimeHelper.IsInDesignMode() || window == null || state == null)
			{
				return;
			}
			// Migration note：Unix 上 Win32 SetWindowPlacement 不可用；Width/Height 是 DIP 可直接赋值，
			// Position 是物理像素需乘 RenderScaling。先设几何再切最大化（WPF 同序）。
			if (!OperatingSystem.IsWindows())
			{
				window.Width = state.Width;
				window.Height = state.Height;
				double num = (window.RenderScaling > 0.0) ? window.RenderScaling : 1.0;
				window.Position = new global::Avalonia.PixelPoint((int)(state.Left * num), (int)(state.Top * num));
				if (state.WindowState == global::Avalonia.Controls.WindowState.Maximized)
				{
					window.WindowState = global::Avalonia.Controls.WindowState.Maximized;
				}
				return;
			}
			WindowInteropHelper windowInteropHelper = new WindowInteropHelper(window);
			WindowPlacement windowPlacement = ToWindowPlacement(state, window);
			windowPlacement.Length = Marshal.SizeOf(typeof(WindowPlacement));
			windowPlacement.Flags = 0;
			if (window.WindowState != global::Avalonia.Controls.WindowState.Minimized)
			{
				SetWindowPlacement(windowInteropHelper.Handle, ref windowPlacement);
			}
		}

		public static WindowLocationState GetWindowLocationState(this Window window)
		{
			if (DesignTimeHelper.IsInDesignMode() || window == null)
			{
				return new WindowLocationState(100.0, 100.0, 1000.0, 600.0, global::Avalonia.Controls.WindowState.Normal);
			}
			if (!OperatingSystem.IsWindows())
			{
				return GetWindowLocationStateAvalonia(window);
			}
			// 始终用 Win32 placement.normalPosition（还原矩形），即使最小化也如此。
			// 之前最小化时走特殊分支用 WPF 的 window.Left/Top/Width/Height，而这些值在最小化时是
			// 系统幽灵值（如 -32000），会导致保存错误的位置，下次恢复窗口跑到屏幕外。
			WindowPlacement placement = GetPlacement(new WindowInteropHelper(window).Handle);
			TransformFromPixels(window, placement.normalPosition.Left, placement.normalPosition.Top, out var unitX, out var unitY);
			TransformFromPixels(window, placement.normalPosition.Right, placement.normalPosition.Bottom, out var unitX2, out var unitY2);
			return new WindowLocationState(unitX, unitY, unitX2 - unitX, unitY2 - unitY, FromShowCmd(placement.ShowCmd));
		}

		public static WindowLocationState GetWindowLocationStateX(this Window window)
		{
			if (DesignTimeHelper.IsInDesignMode() || window == null)
			{
				return new WindowLocationState(100.0, 100.0, 1000.0, 600.0, global::Avalonia.Controls.WindowState.Normal);
			}
			if (!OperatingSystem.IsWindows())
			{
				return GetWindowLocationStateAvalonia(window);
			}
			WindowPlacement placement = GetPlacement(new WindowInteropHelper(window).Handle);
			TransformFromPixels(window, placement.normalPosition.Left, placement.normalPosition.Top, out var unitX, out var unitY);
			TransformFromPixels(window, placement.normalPosition.Right, placement.normalPosition.Bottom, out var unitX2, out var unitY2);
			return new WindowLocationState(unitX, unitY, unitX2 - unitX, unitY2 - unitY, FromShowCmd(placement.ShowCmd));
		}

		/// <summary>
		/// Migration note：Unix 路径的窗口几何读取。最大化/最小化时返回缓存的正常态边界（等价
		/// Win32 placement.normalPosition 还原矩形语义）；正常态实时读取并刷新缓存。
		/// </summary>
		private static WindowLocationState GetWindowLocationStateAvalonia(Window window)
		{
			global::Avalonia.Controls.WindowState windowState = window.WindowState;
			if (windowState != global::Avalonia.Controls.WindowState.Normal && LastNormalBounds.TryGetValue(window, out var value))
			{
				return value;
			}
			double num = (window.RenderScaling > 0.0) ? window.RenderScaling : 1.0;
			global::Avalonia.PixelPoint position = window.Position;
			global::Avalonia.Rect bounds = window.Bounds;
			double width = bounds.Width;
			double height = bounds.Height;
			if (width <= 0.0 || double.IsNaN(width))
			{
				width = window.Width;
			}
			if (height <= 0.0 || double.IsNaN(height))
			{
				height = window.Height;
			}
			WindowLocationState windowLocationState = new WindowLocationState((double)position.X / num, (double)position.Y / num, width, height, windowState);
			if (windowState == global::Avalonia.Controls.WindowState.Normal)
			{
				LastNormalBounds.Remove(window);
				LastNormalBounds.Add(window, windowLocationState);
			}
			return windowLocationState;
		}

		public static void GetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
		{
			if (DesignTimeHelper.IsInDesignMode() || !OperatingSystem.IsWindows())
			{
				return;
			}
			MinMaxInfo minMaxInfo = (MinMaxInfo)Marshal.PtrToStructure(lParam, typeof(MinMaxInfo));
			IntPtr hMonitor = MonitorFromWindow(hwnd, 2);
			if (AutoHideEnabled())
			{
				MonitorInfo lpmi = default(MonitorInfo);
				lpmi.cbSize = (uint)Marshal.SizeOf(typeof(MonitorInfo));
				GetMonitorInfo(hMonitor, ref lpmi);
				Rect rcWork = lpmi.rcWork;
				Rect rcMonitor = lpmi.rcMonitor;
				minMaxInfo.ptMaxPosition.X = Math.Abs(rcWork.Left - rcMonitor.Left);
				minMaxInfo.ptMaxPosition.Y = Math.Abs(rcWork.Top - rcMonitor.Top);
				minMaxInfo.ptMaxSize.X = Math.Abs(rcWork.Right - rcWork.Left);
				minMaxInfo.ptMaxSize.Y = Math.Abs(rcWork.Bottom - rcMonitor.Top - 1);
			}
			Marshal.StructureToPtr(minMaxInfo, lParam, fDeleteOld: true);
		}

		public static bool AutoHideEnabled()
		{
			// Migration note：任务栏自动隐藏探测（SHAppBarMessage）是 Windows 专属，Unix 恒 false。
			if (DesignTimeHelper.IsInDesignMode() || !OperatingSystem.IsWindows())
			{
				return false;
			}
			AppBarData appBarData = default(AppBarData);
			appBarData.cbSize = (uint)Marshal.SizeOf(typeof(AppBarData));
			AppBarData abd = appBarData;
			return 1 == SHAppBarMessage(4u, ref abd);
		}

		public static Thickness WindowResizeBorderThickness => new Thickness(SystemParameters.WindowResizeBorderThickness.Left, SystemParameters.WindowResizeBorderThickness.Top, SystemParameters.WindowResizeBorderThickness.Left, SystemParameters.WindowResizeBorderThickness.Top);

		public static void TransformFromPixels(Visual visual, double pixelX, double pixelY, out int unitX, out int unitY)
		{
			if (DesignTimeHelper.IsInDesignMode() || visual == null)
			{
				unitX = (int)pixelX;
				unitY = (int)pixelY;
				return;
			}
			// Migration note：WPF PresentationSource.FromVisual(visual).CompositionTarget.TransformToDevice
			// 提供 DIP→像素矩阵（PresentationSource 在 Avalonia 是内部类，CS0122）；
			// Avalonia 等价物是 TopLevel.RenderScaling（设备缩放比，对应矩阵的 M11/M22）。
			double transformToDevice = GetVisualScaling(visual);
			if (transformToDevice > 0.0)
			{
				unitX = (int)(pixelX / transformToDevice);
				unitY = (int)(pixelY / transformToDevice);
			}
			else
			{
				unitX = (int)pixelX;
				unitY = (int)pixelY;
			}
		}

		public static void TransformToPixels(Visual visual, double unitX, double unitY, out int pixelX, out int pixelY)
		{
			if (DesignTimeHelper.IsInDesignMode() || visual == null)
			{
				pixelX = (int)unitX;
				pixelY = (int)unitY;
				return;
			}
			// Migration note：同 TransformFromPixels——PresentationSource（Avalonia 内部类，CS0122）
			// 改用 TopLevel.RenderScaling 做 DIP↔像素换算。
			double transformToDevice = GetVisualScaling(visual);
			if (transformToDevice > 0.0)
			{
				pixelX = (int)(unitX * transformToDevice);
				pixelY = (int)(unitY * transformToDevice);
			}
			else
			{
				pixelX = (int)unitX;
				pixelY = (int)unitY;
			}
		}

		/// <summary>取 visual 所在 TopLevel 的设备缩放比（无 TopLevel 时按 1.0 处理）。</summary>
		private static double GetVisualScaling(Visual visual)
		{
			global::Avalonia.Controls.TopLevel topLevel = global::Avalonia.Controls.TopLevel.GetTopLevel(visual); // Migration note：TopLevel 在 Controls 命名空间。
			return topLevel?.RenderScaling ?? 1.0;
		}

		private static WindowPlacement ToWindowPlacement(WindowLocationState state, Window window)
		{
			WindowPlacement result = default(WindowPlacement);
			result.ShowCmd = ToShowCmd(state.WindowState);
			TransformToPixels(window, state.Left, state.Top, out var pixelX, out var pixelY);
			TransformToPixels(window, state.Left + state.Width, state.Top + state.Height, out var pixelX2, out var pixelY2);
			result.normalPosition = new Rect(pixelX, pixelY, pixelX2, pixelY2);
			result.MinPosition = default(Point);
			result.MaxPosition = default(Point);
			return result;
		}

		// 改为 internal 以便冒烟测试直接覆盖。Win32 ShowCmd 与 WPF WindowState 的枚举值
		// 不能直接强转（见 FromShowCmd 注释），这是历史上窗口最大化状态丢失的根因，必须有测试守卫。
		internal static int ToShowCmd(global::Avalonia.Controls.WindowState windowState)
		{
			return windowState switch
			{
				global::Avalonia.Controls.WindowState.Minimized => 2,
				global::Avalonia.Controls.WindowState.Maximized => 3,
				_ => 1,
			};
		}

		// Win32 ShowCmd 与 WPF WindowState 的枚举值不同，不能直接强转：
		//   SW_NORMAL=1, SW_SHOWMINIMIZED=2, SW_SHOWMAXIMIZED=3
		//   WindowState.Normal=0, Minimized=1, Maximized=2
		// 之前用 (WindowState)placement.ShowCmd 导致最大化被存成值 3（无效），
		// 恢复时既不匹配 Minimized 也不匹配 Maximized，最大化状态丢失。
		internal static global::Avalonia.Controls.WindowState FromShowCmd(int showCmd)
		{
			return showCmd switch
			{
				2 => global::Avalonia.Controls.WindowState.Minimized,
				3 => global::Avalonia.Controls.WindowState.Maximized,
				_ => global::Avalonia.Controls.WindowState.Normal,
			};
		}

		private static bool RectanglesIntersect(Rect a, Rect b)
		{
			if (a.Left < b.Right && a.Right > b.Left)
			{
				if (a.Top >= b.Bottom)
				{
					return a.Bottom > b.Top;
				}
				return true;
			}
			return false;
		}

		private static Rect PlaceOnScreen(Rect monitorRect, Rect windowRect)
		{
			int num = monitorRect.Right - monitorRect.Left;
			int num2 = monitorRect.Bottom - monitorRect.Top;
			if (windowRect.Right < monitorRect.Left)
			{
				int num3 = windowRect.Right - windowRect.Left;
				if (num3 > num)
				{
					num3 = num;
				}
				windowRect.Left = monitorRect.Left;
				windowRect.Right = windowRect.Left + num3;
			}
			else if (windowRect.Left > monitorRect.Right)
			{
				int num4 = windowRect.Right - windowRect.Left;
				if (num4 > num)
				{
					num4 = num;
				}
				windowRect.Right = monitorRect.Right;
				windowRect.Left = windowRect.Right - num4;
			}
			if (windowRect.Bottom < monitorRect.Top)
			{
				int num5 = windowRect.Bottom - windowRect.Top;
				if (num5 > num2)
				{
					num5 = num2;
				}
				windowRect.Top = monitorRect.Top;
				windowRect.Bottom = windowRect.Top + num5;
			}
			else if (windowRect.Top > monitorRect.Bottom)
			{
				int num6 = windowRect.Bottom - windowRect.Top;
				if (num6 > num2)
				{
					num6 = num2;
				}
				windowRect.Bottom = monitorRect.Bottom;
				windowRect.Top = windowRect.Bottom - num6;
			}
			return windowRect;
		}

		private static WindowPlacement GetPlacement(IntPtr windowHandle)
		{
			WindowPlacement result = default(WindowPlacement);
			result.Length = Marshal.SizeOf(typeof(WindowPlacement));
			GetWindowPlacement(windowHandle, ref result);
			return result;
		}

		private static DisplayScale GetDisplayScale(IntPtr hwnd)
		{
			IntPtr hdc = GetDC(hwnd);
			int deviceCaps = GetDeviceCaps(hdc, 88);
			int deviceCaps2 = GetDeviceCaps(hdc, 90);
			ReleaseDC(hwnd, hdc);
			return new DisplayScale((float)deviceCaps / 96f, (float)deviceCaps2 / 96f);
		}
	}
}
