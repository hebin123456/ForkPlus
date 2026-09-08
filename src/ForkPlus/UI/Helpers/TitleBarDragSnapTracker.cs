using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace ForkPlus.UI.Helpers
{
	/// <summary>
	/// 问题H（2026-09-04）："Windows 上拖住标题栏贴到屏幕顶部，理论上应该最大化"（原生 Aero Snap 顶边行为）。
	/// CustomWindow 用 SystemDecorations.None 自绘 chrome，窗口没有 WS_THICKFRAME/WS_MAXIMIZEBOX，
	/// 原生移动循环不提供贴边吸附（拖到顶边松手不最大化）。本类手工模拟该行为：
	/// 拖拽期间每次窗口 PositionChanged（Win32 为 WM_MOVE，原生模态移动循环中仍会触发）采样一次，
	/// "光标 AND 窗口顶边都贴住光标所在屏幕的顶边"视为吸附；拖拽结束时仍吸附则最大化。
	/// 判定必须光标与窗口顶边双条件缺一不可：
	///   - 只看光标：拖拽中按 Escape 取消会把窗口弹回拖拽起点，若光标仍停在屏幕顶部，
	///     松手（或取消本身）就会被误判为贴顶而最大化——窗口顶边（已回起点）条件排除该误判；
	///   - 只看窗口顶边：最大化窗口被拖拽还原的瞬间，还原位置 = 光标 - 抓取偏移，窗口顶边
	///     天然贴着屏幕顶边，而光标还在标题栏中部（原生此时并不吸附）——光标条件排除该误判。
	/// </summary>
	public sealed class TitleBarDragSnapTracker
	{
		/// <summary>
		/// 是否在当前平台启用贴顶吸附模拟。仅 Windows：原生 Aero Snap 语义只在 Windows 存在；
		/// MouseHelper.GetMousePosition 在 macOS 返回 (0,0)（恒判贴顶，会误最大化），
		/// Linux/X11 无原生贴边行为且窗口管理器差异大，保持默认不启用。
		/// </summary>
		public static bool IsPlatformSupported => OperatingSystem.IsWindows();

		/// <summary>拖拽是否启用贴顶跟踪：平台支持 + 可缩放（NoResize 窗口不允许最大化，与双击标题栏一致）+ 非全屏。</summary>
		public static bool ShouldTrackDrag(bool isPlatformSupported, bool canResize, WindowState windowState)
		{
			return isPlatformSupported && canResize && windowState != WindowState.FullScreen;
		}

		/// <summary>光标与窗口顶边均贴住（或越过）屏幕顶边视为吸附。光标被系统钳在屏幕内，正常贴顶时恰好相等。</summary>
		public static bool IsTopSnapEngaged(PixelPoint cursorPosition, PixelPoint windowPosition, PixelRect screenBounds)
		{
			return cursorPosition.Y <= screenBounds.Y && windowPosition.Y <= screenBounds.Y;
		}

		/// <summary>
		/// 拖拽结束时是否最大化：吸附中 + 窗口仍可见（拖拽中 Alt+F4 关闭的防护，WindowState
		/// getter 在窗口关闭后会 NRE，isWindowVisible 必须在前短路）+ 当前为普通窗口
		/// （Escape 取消还原拖拽时系统会把窗口变回 Maximized，不应再最大化）。
		/// </summary>
		public static bool ShouldMaximize(bool topSnapEngaged, bool isWindowVisible, WindowState windowState)
		{
			return topSnapEngaged && isWindowVisible && windowState == WindowState.Normal;
		}

		private readonly Func<Point> _getCursorPosition;

		private readonly Func<PixelPoint, Screen> _getScreenFromPoint;

		/// <summary>最近一次采样的贴顶状态（拖拽结束时的判定依据）。</summary>
		public bool TopSnapEngaged { get; private set; }

		public TitleBarDragSnapTracker(Func<Point> getCursorPosition, Func<PixelPoint, Screen> getScreenFromPoint)
		{
			_getCursorPosition = getCursorPosition ?? throw new ArgumentNullException(nameof(getCursorPosition));
			_getScreenFromPoint = getScreenFromPoint ?? throw new ArgumentNullException(nameof(getScreenFromPoint));
		}

		/// <summary>拖拽开始：清零吸附状态（未发生移动的点击不应最大化）。</summary>
		public void Begin()
		{
			TopSnapEngaged = false;
		}

		/// <summary>
		/// 拖拽中采样：<paramref name="windowPosition"/> 为本次 PositionChanged 的新窗口位置（物理像素）。
		/// 光标位置用硬件级查询（MouseHelper.GetMousePosition）——原生模态移动循环期间
		/// Avalonia 不派发指针事件，常规输入 API 拿不到光标。
		/// </summary>
		public void Sample(PixelPoint windowPosition)
		{
			Point cursor = _getCursorPosition();
			PixelPoint cursorPosition = new PixelPoint((int)cursor.X, (int)cursor.Y);
			Screen screen = _getScreenFromPoint(cursorPosition);
			TopSnapEngaged = screen != null && IsTopSnapEngaged(cursorPosition, windowPosition, screen.Bounds);
		}
	}
}
