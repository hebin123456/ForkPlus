using System;
using System.Runtime.InteropServices;
using Avalonia;

namespace ForkPlus.UI.Helpers
{
	// 跨平台全局鼠标位置获取。
	//
	// 策略：
	// 1. 优先使用 SetLastPointerPosition 缓存的屏幕坐标（由 UI 控件在 PointerMoved 事件中调用）。
	//    这是跨平台首选方案 —— Pointer 事件是 Avalonia 抽象，所有平台（含 Wayland）一致。
	//    调用方（如 Treemap）在 OnPointerMoved 中调用 SetLastPointerPosition(e.GetPosition(null))，
	//    即可让 GetMousePosition 返回准确的屏幕坐标。
	// 2. Windows 平台 fallback：user32!GetCursorPos（当无缓存 Pointer 事件时，如非 UI 线程触发）。
	// 3. 非 Windows 无缓存时返回 Origin（保守回退）。
	//
	// 注：Wayland 协议禁止客户端获取全局鼠标坐标，但 Pointer 事件在控件 surface 内可用，
	//     只要鼠标在 Treemap 控件上方悬停，PointerMoved 就会触发，覆盖 tooltip 场景。
	internal static class MouseHelper
	{
		private static readonly bool s_isWindows =
			OperatingSystem.IsWindows();

		// 缓存最近一次 PointerMoved 事件的屏幕坐标（e.GetPosition(null) 返回值）。
		// 由 UI 控件在 OnPointerMoved 中调用 SetLastPointerPosition 设置。
		[Null]
		private static Point? s_lastPointerScreenPosition;

		private struct Win32Point
		{
			public int X;

			public int Y;
		}

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GetCursorPos(ref Win32Point pt);

		/// <summary>
		/// 缓存最近一次 PointerMoved 事件的屏幕坐标。
		/// 调用方（如 Treemap.OnPointerMoved）应传入 e.GetPosition(null) 的返回值。
		/// </summary>
		public static void SetLastPointerPosition(Point screenPosition)
		{
			s_lastPointerScreenPosition = screenPosition;
		}

		public static Point GetMousePosition()
		{
			// 优先返回缓存的 Pointer 屏幕坐标（跨平台，含 Wayland）。
			if (s_lastPointerScreenPosition.HasValue)
			{
				return s_lastPointerScreenPosition.Value;
			}

			// Windows fallback：user32!GetCursorPos（无缓存 Pointer 事件时）。
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

			// 非 Windows 无缓存时的降级：返回 Origin。
			return new Point(0, 0);
		}
	}
}
