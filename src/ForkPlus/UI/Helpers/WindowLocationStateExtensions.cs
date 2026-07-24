// 阶段 4.5：WPF→Avalonia 迁移。
//
// 主要变更：
// - using System.Windows.* → using Avalonia.*
// - System.Windows.Window → Avalonia.Controls.Window（API 兼容）
// - System.Windows.WindowState → Avalonia.Controls.WindowState（已由 WindowLocationState.cs 迁移，枚举值兼容）
// - System.Windows.Interop.WindowInteropHelper(window).Handle → Avalonia Window.TryGetPlatformHandle(out var handle)
// - System.Windows.PresentationSource.FromVisual(visual).CompositionTarget.TransformToDevice → Avalonia Visual.RenderScaling（单一 DPI 因子，无 M11/M22 分别）
// - System.Windows.SystemParameters.WindowResizeBorderThickness → 无 Avalonia 等价物，回退到默认值 8px
// - System.Windows.Media.Visual → Avalonia.Visual
//
// 跨平台策略：
// - 所有 Win32 P/Invoke（MonitorFromWindow/GetWindowPlacement/SetWindowPlacement/SHAppBarMessage/GetSystemMetrics/GetDeviceCaps/GetDC/ReleaseDC）
//   均为 Windows-only，已用 RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 守卫。
// - 非 Windows 平台：GetWindowLocationState/SetWindowLocationState 回退到 Avalonia 原生 window.Position/ClientSize/WindowState。
// - TransformFromPixels/TransformToPixels 在非 Windows 平台使用 window.RenderScaling（单一因子，不区分 X/Y）。
//
// TODO(4.5): macOS/Linux 平台多显示器工作区（含 Dock 遮挡）尚需原生 API 实现；当前仅 Windows 走 AutoHideEnabled/GetMinMaxInfo 路径。
using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using ForkPlus.Settings;

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

		private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

		// 阶段 4.5：Avalonia Window.TryGetPlatformHandle 返回 IPlatformHandle（Handle 为 IntPtr HWND）。
		// 替代 WPF WindowInteropHelper(window).Handle。非 Windows 平台返回 false。
		private static bool TryGetWindowHandle(Window window, out IntPtr handle)
		{
			handle = IntPtr.Zero;
			if (window == null || !IsWindows)
			{
				return false;
			}
			if (window.TryGetPlatformHandle(out IPlatformHandle platformHandle))
			{
				handle = platformHandle.Handle;
				return handle != IntPtr.Zero;
			}
			return false;
		}

		public static void SetWindowLocationState(this Window window, WindowLocationState state)
		{
			if (DesignTimeHelper.IsInDesignMode() || window == null || state == null)
			{
				return;
			}
			// 阶段 4.5：非 Windows 平台回退到 Avalonia 原生 API（Position/ClientSize/WindowState）。
			if (!IsWindows || !TryGetWindowHandle(window, out IntPtr handle))
			{
				window.Position = new PixelPoint((int)state.Left, (int)state.Top);
				window.Width = state.Width;
				window.Height = state.Height;
				if (window.WindowState != state.WindowState)
				{
					window.WindowState = state.WindowState;
				}
				return;
			}
			WindowPlacement windowPlacement = ToWindowPlacement(state, window);
			windowPlacement.Length = Marshal.SizeOf(typeof(WindowPlacement));
			windowPlacement.Flags = 0;
			if (window.WindowState != WindowState.Minimized)
			{
				SetWindowPlacement(handle, ref windowPlacement);
			}
		}

		public static WindowLocationState GetWindowLocationState(this Window window)
		{
			if (DesignTimeHelper.IsInDesignMode() || window == null)
			{
				return new WindowLocationState(100.0, 100.0, 1000.0, 600.0, WindowState.Normal);
			}
			// 阶段 4.5：非 Windows 平台回退到 Avalonia 原生 API。
			if (!IsWindows || !TryGetWindowHandle(window, out IntPtr handle))
			{
				return new WindowLocationState(window.Position.X, window.Position.Y, window.ClientSize.Width, window.ClientSize.Height, window.WindowState);
			}
			// 始终用 Win32 placement.normalPosition（还原矩形），即使最小化也如此。
			// 之前最小化时走特殊分支用 WPF 的 window.Left/Top/Width/Height，而这些值在最小化时是
			// 系统幽灵值（如 -32000），会导致保存错误的位置，下次恢复窗口跑到屏幕外。
			WindowPlacement placement = GetPlacement(handle);
			TransformFromPixels(window, placement.normalPosition.Left, placement.normalPosition.Top, out var unitX, out var unitY);
			TransformFromPixels(window, placement.normalPosition.Right, placement.normalPosition.Bottom, out var unitX2, out var unitY2);
			return new WindowLocationState(unitX, unitY, unitX2 - unitX, unitY2 - unitY, FromShowCmd(placement.ShowCmd));
		}

		public static WindowLocationState GetWindowLocationStateX(this Window window)
		{
			if (DesignTimeHelper.IsInDesignMode() || window == null)
			{
				return new WindowLocationState(100.0, 100.0, 1000.0, 600.0, WindowState.Normal);
			}
			if (!IsWindows || !TryGetWindowHandle(window, out IntPtr handle))
			{
				return new WindowLocationState(window.Position.X, window.Position.Y, window.ClientSize.Width, window.ClientSize.Height, window.WindowState);
			}
			WindowPlacement placement = GetPlacement(handle);
			TransformFromPixels(window, placement.normalPosition.Left, placement.normalPosition.Top, out var unitX, out var unitY);
			TransformFromPixels(window, placement.normalPosition.Right, placement.normalPosition.Bottom, out var unitX2, out var unitY2);
			return new WindowLocationState(unitX, unitY, unitX2 - unitX, unitY2 - unitY, FromShowCmd(placement.ShowCmd));
		}

		public static void GetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
		{
			if (DesignTimeHelper.IsInDesignMode() || !IsWindows)
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
			if (DesignTimeHelper.IsInDesignMode() || !IsWindows)
			{
				return false;
			}
			AppBarData appBarData = default(AppBarData);
			appBarData.cbSize = (uint)Marshal.SizeOf(typeof(AppBarData));
			AppBarData abd = appBarData;
			return 1 == SHAppBarMessage(4u, ref abd);
		}

		// 阶段 4.5：WPF SystemParameters.WindowResizeBorderThickness → 无 Avalonia 等价物。
		// 默认值 8px 与 Windows 默认 NonClientArea 边框一致。Avalonia 11 WindowResizeBorderThickness 在 Mac/Linux 由系统决定。
		// TODO(4.5): 验证 macOS/Linux 下窗口外边框宽度；如需精确值，应通过 window.OffScreenPadding 或原生 API 获取。
		public static Thickness WindowResizeBorderThickness => new Thickness(8.0);

		public static void TransformFromPixels(Visual visual, double pixelX, double pixelY, out int unitX, out int unitY)
		{
			if (DesignTimeHelper.IsInDesignMode() || visual == null)
			{
				unitX = (int)pixelX;
				unitY = (int)pixelY;
				return;
			}
			// 阶段 4.5：WPF PresentationSource.FromVisual(visual).CompositionTarget.TransformToDevice（Matrix M11/M22）
			// → Avalonia Visual.RenderScaling（单一 DPI 因子，Avalonia 假设 X/Y DPI 一致）。
			// 对绝大多数显示器成立；混合 DPI 多显示器场景由 Avalonia 自动按显示器缩放。
			double scaling = visual.RenderScaling;
			unitX = (int)(pixelX / scaling);
			unitY = (int)(pixelY / scaling);
		}

		public static void TransformToPixels(Visual visual, double unitX, double unitY, out int pixelX, out int pixelY)
		{
			if (DesignTimeHelper.IsInDesignMode() || visual == null)
			{
				pixelX = (int)unitX;
				pixelY = (int)unitY;
				return;
			}
			double scaling = visual.RenderScaling;
			pixelX = (int)(unitX * scaling);
			pixelY = (int)(unitY * scaling);
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

		// 改为 internal 以便冒烟测试直接覆盖。Win32 ShowCmd 与 Avalonia WindowState 的枚举值
		// 不能直接强转（见 FromShowCmd 注释），这是历史上窗口最大化状态丢失的根因，必须有测试守卫。
		internal static int ToShowCmd(WindowState windowState)
		{
			return windowState switch
			{
				WindowState.Minimized => 2,
				WindowState.Maximized => 3,
				_ => 1,
			};
		}

		// Win32 ShowCmd 与 Avalonia WindowState 的枚举值不同，不能直接强转：
		//   SW_NORMAL=1, SW_SHOWMINIMIZED=2, SW_SHOWMAXIMIZED=3
		//   WindowState.Normal=0, Minimized=1, Maximized=2
		// 之前用 (WindowState)placement.ShowCmd 导致最大化被存成值 3（无效），
		// 恢复时既不匹配 Minimized 也不匹配 Maximized，最大化状态丢失。
		internal static WindowState FromShowCmd(int showCmd)
		{
			return showCmd switch
			{
				2 => WindowState.Minimized,
				3 => WindowState.Maximized,
				_ => WindowState.Normal,
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
