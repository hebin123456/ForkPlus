using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
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

		public static void SetWindowLocationState(this Window window, WindowLocationState state)
		{
			if (DesignTimeHelper.IsInDesignMode() || window == null || state == null)
			{
				return;
			}
			WindowInteropHelper windowInteropHelper = new WindowInteropHelper(window);
			WindowPlacement windowPlacement = ToWindowPlacement(state, window);
			windowPlacement.Length = Marshal.SizeOf(typeof(WindowPlacement));
			windowPlacement.Flags = 0;
			if (window.WindowState != WindowState.Minimized)
			{
				SetWindowPlacement(windowInteropHelper.Handle, ref windowPlacement);
			}
		}

		public static WindowLocationState GetWindowLocationState(this Window window)
		{
			if (DesignTimeHelper.IsInDesignMode() || window == null)
			{
				return new WindowLocationState(100.0, 100.0, 1000.0, 600.0, WindowState.Normal);
			}
			WindowPlacement placement = GetPlacement(new WindowInteropHelper(window).Handle);
			if (placement.ShowCmd == 2)
			{
				return new WindowLocationState(window.Left, window.Top, window.Width, window.Height, WindowState.Minimized);
			}
			TransformFromPixels(window, placement.normalPosition.Left, placement.normalPosition.Top, out var unitX, out var unitY);
			TransformFromPixels(window, placement.normalPosition.Right, placement.normalPosition.Bottom, out var unitX2, out var unitY2);
			return new WindowLocationState(unitX, unitY, unitX2 - unitX, unitY2 - unitY, (WindowState)placement.ShowCmd);
		}

		public static WindowLocationState GetWindowLocationStateX(this Window window)
		{
			if (DesignTimeHelper.IsInDesignMode() || window == null)
			{
				return new WindowLocationState(100.0, 100.0, 1000.0, 600.0, WindowState.Normal);
			}
			WindowPlacement placement = GetPlacement(new WindowInteropHelper(window).Handle);
			TransformFromPixels(window, placement.normalPosition.Left, placement.normalPosition.Top, out var unitX, out var unitY);
			TransformFromPixels(window, placement.normalPosition.Right, placement.normalPosition.Bottom, out var unitX2, out var unitY2);
			return new WindowLocationState(unitX, unitY, unitX2 - unitX, unitY2 - unitY, (WindowState)placement.ShowCmd);
		}

		public static void GetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
		{
			if (DesignTimeHelper.IsInDesignMode())
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
			if (DesignTimeHelper.IsInDesignMode())
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
			PresentationSource presentationSource = PresentationSource.FromVisual(visual);
			if (presentationSource?.CompositionTarget != null)
			{
				Matrix transformToDevice = presentationSource.CompositionTarget.TransformToDevice;
				unitX = (int)(pixelX / transformToDevice.M11);
				unitY = (int)(pixelY / transformToDevice.M22);
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
			PresentationSource presentationSource = PresentationSource.FromVisual(visual);
			if (presentationSource?.CompositionTarget != null)
			{
				Matrix transformToDevice = presentationSource.CompositionTarget.TransformToDevice;
				pixelX = (int)(unitX * transformToDevice.M11);
				pixelY = (int)(unitY * transformToDevice.M22);
			}
			else
			{
				pixelX = (int)unitX;
				pixelY = (int)unitY;
			}
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

		private static int ToShowCmd(WindowState windowState)
		{
			return windowState switch
			{
				WindowState.Minimized => 2,
				WindowState.Maximized => 3,
				_ => 1,
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
