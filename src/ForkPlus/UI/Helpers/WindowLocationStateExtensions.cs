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

	// 修复（2026-09-17，"每次启动主窗口缩成极小窗，settings.json 里 Width/Height=0.0"）：
	// 尺寸有效性判定。0/NaN/∞ 一律视为无效，读取端兜底回退、写入端（Set）替换为默认值，
	// 保证 WindowLocationState 永远不会携带 0×0 几何进出持久化链路。
	internal static bool IsValidDimension(double value)
	{
		return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0.0;
	}

	// 修复（2026-09-17）：几何消毒——宽/高无效时回退默认 1000×600（与各窗口的
	// 默认 WindowLocationState 一致），Left/Top 不动（贴屏幕左上角 (0,0) 合法）。
	private static WindowLocationState SanitizeGeometry(WindowLocationState state)
	{
		if (IsValidDimension(state.Width) && IsValidDimension(state.Height))
		{
			return state;
		}
		return new WindowLocationState(state.Left, state.Top,
			IsValidDimension(state.Width) ? state.Width : 1000.0,
			IsValidDimension(state.Height) ? state.Height : 600.0,
			state.WindowState);
	}

	public static void SetWindowLocationState(this Window window, WindowLocationState state)
	{
		if (DesignTimeHelper.IsInDesignMode() || window == null || state == null)
		{
			return;
		}
		// 修复（2026-09-17）：恢复端兜底——被污染的 0×0 配置（含历史已落盘的
		// Width/Height=0.0）不得直接应用到窗口，否则窗口被初始化为 0×0 极小窗；
		// 无效尺寸回退默认 1000×600，下次正常关闭即可写回正确尺寸，自愈闭环。
		state = SanitizeGeometry(state);
			// 修复（2026-09-10，"重启后窗口位置/大小/最大化状态没完全恢复"）：
			// 原先 Windows 走 Win32 SetWindowPlacement（ShowCmd=SW_SHOWMAXIMIZED 切最大化），
			// 但 Avalonia 的 WindowState 属性不随 Win32 状态变化而更新——仍认为 Normal，
			// 后续布局/渲染按 Normal 处理，或用户点还原时与 Win32 已最大化冲突，最大化丢失。
			// 实测即便补 window.WindowState=Maximized 同步，SetWindowPlacement 与 Avalonia 平台层
			// 仍互相打架，恢复不可靠。改为跨平台统一走 Avalonia 原生 API：Width/Height（DIP）+
			// Position（物理像素 = DIP × RenderScaling）+ WindowState，与 Unix 路径完全一致。
			// 先设正常态几何，再切最大化——还原时窗口回到此处设的 normal rect（与 save 端
			// GetWindowPlacement.normalPosition 语义对齐）。Win32 GetWindowPlacement 仍用于
			// save 端读取正常态矩形（最大化时取还原矩形），restore 端不再用 SetWindowPlacement。
			window.Width = state.Width;
			window.Height = state.Height;
			double num = (window.RenderScaling > 0.0) ? window.RenderScaling : 1.0;
			window.Position = new global::Avalonia.PixelPoint((int)(state.Left * num), (int)(state.Top * num));
			if (state.WindowState == global::Avalonia.Controls.WindowState.Maximized)
			{
				window.WindowState = global::Avalonia.Controls.WindowState.Maximized;
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
		// 修复（2026-09-17）：placement 读取可能失败（句柄无效/窗口已销毁）——见 GetPlacement，
		// 失败时回退 Avalonia 属性读取（Bounds/Position），再经 SanitizeGeometry 兜底，
		// 绝不返回 0×0 状态。
		WindowPlacement? placement = GetPlacement(new WindowInteropHelper(window).Handle);
		if (placement == null)
		{
			return SanitizeGeometry(GetWindowLocationStateAvalonia(window));
		}
		TransformFromPixels(window, placement.Value.normalPosition.Left, placement.Value.normalPosition.Top, out var unitX, out var unitY);
	TransformFromPixels(window, placement.Value.normalPosition.Right, placement.Value.normalPosition.Bottom, out var unitX2, out var unitY2);
	// 修复（2026-09-10，"最大化没保存/下次启动不最大化"）：状态用 Avalonia 的 window.WindowState
	// （用户实际看到的状态），不用 Win32 ShowCmd——SystemDecorations.None 自绘 chrome 下
	// Avalonia WindowState 与 Win32 实际状态偶发不一致（Win32 仍是 Normal），用 Win32
	// ShowCmd 会误存成 Normal，下次启动不最大化。normal rect 仍取 Win32 placement（还原矩形正确）。
	return SanitizeGeometry(new WindowLocationState(unitX, unitY, unitX2 - unitX, unitY2 - unitY, window.WindowState));
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
		// 修复（2026-09-17）：同 GetWindowLocationState——placement 读取失败（句柄无效/
		// 窗口已销毁）时回退 Avalonia 属性读取并消毒，绝不返回 0×0。
		// 本方法正是 MainWindow.Window_Closing 的保存路径：Avalonia 关闭链路上 Closing
		// 可能被二次触发（lifetime.Shutdown → CloseCore 无已关闭守卫），二次触发时
		// PlatformImpl 已销毁、句柄为 0，旧代码直接产出全零状态并落盘——
		// 这就是"每次启动都是极小窗口、Width/Height 恒为 0.0"的直接来源。
		WindowPlacement? placement = GetPlacement(new WindowInteropHelper(window).Handle);
		if (placement == null)
		{
			return SanitizeGeometry(GetWindowLocationStateAvalonia(window));
		}
		TransformFromPixels(window, placement.Value.normalPosition.Left, placement.Value.normalPosition.Top, out var unitX, out var unitY);
		TransformFromPixels(window, placement.Value.normalPosition.Right, placement.Value.normalPosition.Bottom, out var unitX2, out var unitY2);
		// 修复（2026-09-10）：同上，状态用 Avalonia window.WindowState，不用 Win32 ShowCmd。
		return SanitizeGeometry(new WindowLocationState(unitX, unitY, unitX2 - unitX, unitY2 - unitY, window.WindowState));
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
		// 修复（2026-09-17）：兜底消毒——窗口已销毁时 Bounds/Width/Height 也可能归零，
		// 保证缓存与返回值都不携带 0×0 几何。
		windowLocationState = SanitizeGeometry(windowLocationState);
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

	// 修复（2026-09-17）：返回值改为可空并检查 Win32 调用结果——
	// 旧代码忽略 GetWindowPlacement 的 BOOL 返回值：句柄无效（IntPtr.Zero，窗口已销毁）或
	// 调用失败时，result 保持 default(WindowPlacement)（normalPosition 全 0），调用方直接
	// 产出 Left=0/Top=0/Width=0/Height=0 的全零状态并落盘。失败时返回 null，
	// 由调用方回退 Avalonia 属性读取 + SanitizeGeometry 兜底。
	private static WindowPlacement? GetPlacement(IntPtr windowHandle)
	{
		if (windowHandle == IntPtr.Zero)
		{
			return null;
		}
		WindowPlacement result = default(WindowPlacement);
		result.Length = Marshal.SizeOf(typeof(WindowPlacement));
		if (!GetWindowPlacement(windowHandle, ref result))
		{
			return null;
		}
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
