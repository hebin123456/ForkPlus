using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ForkPlus.UI;

namespace ForkPlus
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
		private struct WindowPlacement
		{
			public int length;

			public int flags;

			public int showCmd;

			public Point minPosition;

			public Point maxPosition;

			public Rect normalPosition;
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

		private struct MonitorInfo
		{
			public uint cbSize;

			public Rect rcMonitor;

			public Rect rcWork;

			public uint dwFlags;
		}

		[StructLayout(LayoutKind.Sequential)]
		private class MinMaxInfo
		{
			public Point ptReserved;

			public Point ptMaxSize;

			public Point ptMaxPosition;

			public Point ptMinTrackSize;

			public Point ptMaxTrackSize;
		}

		private struct AppBarData
		{
			public int cbSize;

			public IntPtr hWnd;

			public uint uCallbackMessage;

			public uint uEdge;

			public Rect rc;

			public int lParam;
		}

		private enum GetSystemMetricsIndex
		{
			CXFRAME = 32,
			CYFRAME = 33,
			SM_CXPADDEDBORDER = 92
		}

		private enum GetDeviceCapsIndex
		{
			LOGPIXELSX = 88,
			LOGPIXELSY = 90
		}

		internal const int WM_GETMINMAXINFO = 36;

		internal const int WM_WINDOWPOSCHANGED = 71;

		private const int MONITOR_DEFAULTTONEAREST = 2;

		private const int SW_SHOWNORMAL = 1;

		private const int SW_SHOWMINIMIZED = 2;

		private const int SW_SHOWMAXIMIZED = 3;

		public static Thickness WindowResizeBorderThickness
		{
			get
			{
				if (DesignTimeHelper.IsInDesignMode())
				{
					return new Thickness(6.0);
				}
				int systemMetrics = GetSystemMetrics(GetSystemMetricsIndex.CXFRAME);
				int systemMetrics2 = GetSystemMetrics(GetSystemMetricsIndex.CYFRAME);
				int systemMetrics3 = GetSystemMetrics(GetSystemMetricsIndex.SM_CXPADDEDBORDER);
				int num = systemMetrics + systemMetrics3;
				systemMetrics2 += systemMetrics3;
				DisplayScale displayScale = GetDisplayScale(IntPtr.Zero);
				float num2 = (float)num / displayScale.X;
				float num3 = (float)systemMetrics2 / displayScale.Y;
				return new Thickness(num2, num3, num2, num3);
			}
		}

		[DllImport("user32.dll")]
		private static extern IntPtr MonitorFromRect([In] ref Rect lprc, uint dwFlags);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

		[DllImport("user32.dll")]
		private static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WindowPlacement lpwndpl);

		[DllImport("user32.dll")]
		private static extern bool GetWindowPlacement(IntPtr hWnd, out WindowPlacement lpwndpl);

		[DllImport("shell32.dll", CallingConvention = CallingConvention.StdCall)]
		private static extern uint SHAppBarMessage(int dwMessage, ref AppBarData pData);

		[DllImport("user32.dll")]
		private static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

		[DllImport("user32.dll")]
		private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

		[DllImport("user32.dll")]
		private static extern IntPtr GetDC(IntPtr hwnd);

		[DllImport("gdi32.dll")]
		private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

		[DllImport("user32.dll")]
		private static extern int GetSystemMetrics(GetSystemMetricsIndex nIndex);

		public static void SetWindowLocationState(this Window window, WindowLocationState state)
		{
			if (DesignTimeHelper.IsInDesignMode() || window == null || state == null)
			{
				return;
			}
			IntPtr handle = new WindowInteropHelper(window).Handle;
			DisplayScale displayScale = GetDisplayScale(IntPtr.Zero);
			WindowPlacement lpwndpl = ToWindowPlacement(state, displayScale.X, displayScale.Y);
			IntPtr hMonitor = MonitorFromRect(ref lpwndpl.normalPosition, 2u);
			MonitorInfo lpmi = new MonitorInfo
			{
				cbSize = (uint)Marshal.SizeOf(typeof(MonitorInfo))
			};
			if (GetMonitorInfo(hMonitor, ref lpmi) && !RectanglesIntersect(lpwndpl.normalPosition, lpmi.rcMonitor))
			{
				lpwndpl.normalPosition = PlaceOnScreen(lpmi.rcMonitor, lpwndpl.normalPosition);
			}
			SetWindowPlacement(handle, ref lpwndpl);
		}

		public static WindowLocationState GetWindowLocationState(this Window window)
		{
			if (DesignTimeHelper.IsInDesignMode() || window == null)
			{
				return new WindowLocationState(100.0, 100.0, 1000.0, 600.0, WindowState.Normal);
			}
			WindowPlacement placement = GetPlacement(new WindowInteropHelper(window).Handle);
			int left = placement.normalPosition.Left;
			int top = placement.normalPosition.Top;
			int num = placement.normalPosition.Bottom - placement.normalPosition.Top;
			int num2 = placement.normalPosition.Right - placement.normalPosition.Left;
			DisplayScale displayScale = GetDisplayScale(IntPtr.Zero);
			return new WindowLocationState((float)left / displayScale.X, (float)top / displayScale.Y, (float)num2 / displayScale.X, (float)num / displayScale.Y, window.WindowState);
		}

		public static WindowLocationState GetWindowLocationStateX(this Window window)
		{
			if (DesignTimeHelper.IsInDesignMode() || window == null)
			{
				return new WindowLocationState(100.0, 100.0, 1000.0, 600.0, WindowState.Normal);
			}
			WindowPlacement placement = GetPlacement(new WindowInteropHelper(window).Handle);
			int unitX = 0;
			int unitY = 0;
			if (window.WindowState != WindowState.Maximized)
			{
				TransformFromPixels(window, placement.normalPosition.Left, placement.normalPosition.Top, out unitX, out unitY);
			}
			double actualWidth = window.ActualWidth;
			double actualHeight = window.ActualHeight;
			return new WindowLocationState(unitX, unitY, actualWidth, actualHeight, window.WindowState);
		}

		public static void GetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
		{
			if (DesignTimeHelper.IsInDesignMode())
			{
				return;
			}
			MinMaxInfo minMaxInfo = (MinMaxInfo)Marshal.PtrToStructure(lParam, typeof(MinMaxInfo));
			IntPtr hMonitor = MonitorFromWindow(hwnd, 2);
			if (minMaxInfo != null && AutoHideEnabled())
			{
				MonitorInfo lpmi = default(MonitorInfo);
				lpmi.cbSize = (uint)Marshal.SizeOf(typeof(MonitorInfo));
				GetMonitorInfo(hMonitor, ref lpmi);
				Rect rcWork = lpmi.rcWork;
				Rect rcMonitor = lpmi.rcMonitor;
				minMaxInfo.ptMaxPosition.X = Math.Abs(rcWork.Left - rcMonitor.Left);
				minMaxInfo.ptMaxPosition.Y = Math.Abs(rcWork.Top - rcMonitor.Top);
				minMaxInfo.ptMaxSize.X = Math.Abs(rcWork.Right - rcWork.Left);
				minMaxInfo.ptMaxSize.Y = Math.Abs(rcWork.Bottom - rcWork.Top - 1);
			}
			Marshal.StructureToPtr(minMaxInfo, lParam, fDeleteOld: true);
		}

		public static bool AutoHideEnabled()
		{
			if (DesignTimeHelper.IsInDesignMode())
			{
				return false;
			}
			AppBarData pData = default(AppBarData);
			return SHAppBarMessage(4, ref pData) != 0;
		}

		public static void TransformFromPixels(Visual visual, double pixelX, double pixelY, out int unitX, out int unitY)
		{
			if (DesignTimeHelper.IsInDesignMode() || visual == null)
			{
				unitX = (int)pixelX;
				unitY = (int)pixelY;
				return;
			}
			PresentationSource presentationSource = PresentationSource.FromVisual(visual);
			Matrix transformToDevice;
			if (presentationSource != null)
			{
				transformToDevice = presentationSource.CompositionTarget.TransformToDevice;
			}
			else
			{
				using HwndSource hwndSource = new HwndSource(default(HwndSourceParameters));
				transformToDevice = hwndSource.CompositionTarget.TransformToDevice;
			}
			unitX = (int)(pixelX / transformToDevice.M11);
			unitY = (int)(pixelY / transformToDevice.M22);
		}

		public static void TransformToPixels(Visual visual, double unitX, double unitY, out int pixelX, out int pixelY)
		{
			PresentationSource presentationSource = PresentationSource.FromVisual(visual);
			Matrix transformToDevice;
			if (presentationSource != null)
			{
				transformToDevice = presentationSource.CompositionTarget.TransformToDevice;
			}
			else
			{
				using HwndSource hwndSource = new HwndSource(default(HwndSourceParameters));
				transformToDevice = hwndSource.CompositionTarget.TransformToDevice;
			}
			pixelX = (int)(transformToDevice.M11 * unitX);
			pixelY = (int)(transformToDevice.M22 * unitY);
		}

		private static WindowPlacement ToWindowPlacement(WindowLocationState state, float scaleX, float scaleY)
		{
			WindowPlacement result = default(WindowPlacement);
			result.minPosition = new Point(-1, -1);
			result.maxPosition = new Point(-1, -1);
			result.normalPosition = new Rect((int)(state.Left * (double)scaleX), (int)(state.Top * (double)scaleY), (int)((state.Left + state.Width) * (double)scaleX), (int)((state.Top + state.Height) * (double)scaleY));
			result.length = Marshal.SizeOf(typeof(WindowPlacement));
			result.flags = 0;
			result.showCmd = ToShowCmd(state.WindowState);
			return result;
		}

		private static int ToShowCmd(WindowState windowState)
		{
			return windowState switch
			{
				WindowState.Maximized => 3, 
				WindowState.Minimized => 2, 
				_ => 1, 
			};
		}

		private static bool RectanglesIntersect(Rect a, Rect b)
		{
			if (a.Left > b.Right || a.Right < b.Left)
			{
				return false;
			}
			if (a.Top > b.Bottom || a.Bottom < b.Top)
			{
				return false;
			}
			return true;
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
			WindowPlacement lpwndpl = default(WindowPlacement);
			GetWindowPlacement(windowHandle, out lpwndpl);
			return lpwndpl;
		}

		private static DisplayScale GetDisplayScale(IntPtr hwnd)
		{
			IntPtr dC = GetDC(hwnd);
			float num = 96f;
			float num2 = 96f;
			try
			{
				num = GetDeviceCaps(dC, 88);
				num2 = GetDeviceCaps(dC, 90);
			}
			finally
			{
				ReleaseDC(hwnd, dC);
			}
			return new DisplayScale(num / 96f, num2 / 96f);
		}
	}
}
