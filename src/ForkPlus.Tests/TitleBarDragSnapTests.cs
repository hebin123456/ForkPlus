// 回归测试（问题H，2026-09-04）："Windows 上最大化后拖住顶部下拉可以还原，但把顶部拖住
// 贴到屏幕上方理论上应该最大化，可是没有"。根因：CustomWindow 用 SystemDecorations.None
// 自绘 chrome，窗口没有 WS_THICKFRAME/WS_MAXIMIZEBOX，Win32 原生移动循环不提供 Aero Snap
// 贴边吸附——"下拉还原"是 SC_MOVE 移动循环自带行为所以有，"贴顶最大化"依赖贴边吸附所以没有。
// 修复：TitleBarDragSnapTracker 在标题栏拖拽期间借 PositionChanged（Win32 为 WM_MOVE，原生
// 模态移动循环中仍触发）采样，"光标 AND 窗口顶边均贴住光标所在屏幕顶边"视为吸附；拖拽结束
// （紧随移动循环任务之后 post 的同优先级 Dispatcher 任务 = 松手时机）仍吸附则最大化。
// 双条件缺一不可，对应两个真实误报场景（各有一条守卫回归）：
//   - 只看光标：拖拽中 Escape 取消把窗口弹回起点，光标仍停在屏幕顶 → 松手误最大化；
//   - 只看窗口顶边：最大化窗口被拖拽还原的瞬间窗口顶边天然贴屏顶，而光标还在标题栏中部 → 误最大化。
// headless 环境实证（探针，2026-09-04，用后即删）：单屏幕 Bounds=(0,0,1920,1280)；对 Position
// 赋值同步触发 PositionChanged；BeginMoveDrag 不抛异常；DispatcherPriority.Send 任务在 RunJobs
// 时执行——完整链路可在 headless 驱动（本文件窗口接线测试全部基于这些事实）。Sample 的采样
// 行为（吸附/解除/最后一次采样生效/屏幕查询失败）由接线测试覆盖：Avalonia 12 的 Screen 是
// 抽象类（internal ctor），纯逻辑测试无法构造实例。
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI;
using ForkPlus.UI.Helpers;
using Xunit;

namespace ForkPlus.Tests
{
	// ===== 纯逻辑：判定函数（无 UI 依赖） =====
	public class TitleBarDragSnapTrackerTests
	{
		[Theory]
		[InlineData(true, true, WindowState.Normal, true)]      // Windows + 可缩放 + 普通：唯一完整启用组合
		[InlineData(true, true, WindowState.Maximized, true)]   // 最大化窗口拖拽还原场景仍需跟踪
		[InlineData(false, true, WindowState.Normal, false)]    // 非 Windows（macOS 光标 API 返回 (0,0) 恒判贴顶会误最大化）
		[InlineData(true, false, WindowState.Normal, false)]    // NoResize 不允许最大化（与双击标题栏行为一致）
		[InlineData(true, true, WindowState.FullScreen, false)] // 全屏窗口不被拖拽打断
		public void ShouldTrackDrag_GuardsPlatformResizeFullScreen(bool platform, bool canResize, WindowState state, bool expected)
		{
			Assert.Equal(expected, TitleBarDragSnapTracker.ShouldTrackDrag(platform, canResize, state));
		}

		[Theory]
		[InlineData(0, 0, true)]   // 光标与窗口顶边都贴住屏幕顶：吸附
		[InlineData(-3, 0, true)]  // 越过顶边同样算（系统把光标钳在屏幕内，等值是常态，越过是冗余容错）
		[InlineData(5, 0, false)]  // 光标未到顶（还原拖拽瞬间光标在标题栏中部）
		[InlineData(0, 5, false)]  // 窗口未到顶（Escape 取消后窗口弹回起点）
		[InlineData(5, 5, false)]  // 都未到顶
		public void IsTopSnapEngaged_RequiresBothCursorAndWindowTopAtScreenTop(int cursorY, int windowY, bool expected)
		{
			PixelRect screen = new PixelRect(0, 0, 1920, 1280);
			Assert.Equal(expected, TitleBarDragSnapTracker.IsTopSnapEngaged(
				new PixelPoint(100, cursorY), new PixelPoint(50, windowY), screen));
		}

		[Fact]
		public void IsTopSnapEngaged_MultiMonitor_NegativeYSecondaryScreen()
		{
			// 主显示器正上方的副屏：屏幕顶边是负 Y 坐标，判定必须相对屏幕 Bounds 而非绝对 0。
			PixelRect screen = new PixelRect(-1920, -1080, 1920, 1080);
			Assert.True(TitleBarDragSnapTracker.IsTopSnapEngaged(
				new PixelPoint(-500, -1080), new PixelPoint(-600, -1080), screen));
			Assert.False(TitleBarDragSnapTracker.IsTopSnapEngaged(
				new PixelPoint(-500, -500), new PixelPoint(-600, -1080), screen));
		}

		[Theory]
		[InlineData(true, true, WindowState.Normal, true)]     // 吸附 + 可见 + 普通：最大化
		[InlineData(false, true, WindowState.Normal, false)]   // 未吸附（拖拽结束前已离开顶边）
		[InlineData(true, false, WindowState.Normal, false)]   // 拖拽中窗口被关闭（WindowState getter 对已关窗口 NRE，可见性必须短路在前）
		[InlineData(true, true, WindowState.Maximized, false)] // Escape 取消还原拖拽后系统已把状态变回 Maximized：不再最大化
		[InlineData(true, true, WindowState.Minimized, false)]
		public void ShouldMaximize_GuardsEngagedVisibleNormal(bool engaged, bool visible, WindowState state, bool expected)
		{
			Assert.Equal(expected, TitleBarDragSnapTracker.ShouldMaximize(engaged, visible, state));
		}
	}

	// ===== 窗口接线：CustomWindow 端到端链路（headless，含 Sample 采样行为） =====
	[Collection("HeadlessAvalonia")]
	public class TitleBarDragSnapWindowTests
	{
		/// <summary>
		/// 测试窗口：override 贴顶启用开关（生产默认仅 Windows，本环境是 Linux）并注入可控光标；
		/// 屏幕提供者默认保持生产实现（headless 单屏幕 (0,0,1920,1280)，顶边 Y=0），可替换以模拟查询失败。
		/// </summary>
		private sealed class SnapTestWindow : CustomWindow
		{
			private Point _cursorPosition = new Point(200, 115);

			private Func<PixelPoint, Screen> _screenProvider;

			protected override bool IsTitleBarDragSnapEnabled => true;

			protected override TitleBarDragSnapTracker CreateTitleBarDragSnapTracker()
			{
				return new TitleBarDragSnapTracker(
					delegate { return _cursorPosition; },
					delegate (PixelPoint point)
					{
						Func<PixelPoint, Screen> provider = _screenProvider;
						if (provider != null)
						{
							return provider(point);
						}
						return Screens != null ? Screens.ScreenFromPoint(point) : null;
					});
			}

			public void SetCursor(Point position)
			{
				_cursorPosition = position;
			}

			public void SetScreenProvider(Func<PixelPoint, Screen> provider)
			{
				_screenProvider = provider;
			}
		}

		private static Control GetHeader(CustomWindow window)
		{
			foreach (Visual descendant in window.GetVisualDescendants())
			{
				Control control = descendant as Control;
				if (control != null && control.Name == "PART_WindowHeader")
				{
					return control;
				}
			}
			return null;
		}

		/// <summary>在标题栏上按下鼠标左键（WindowHeader_PointerPressed → BeginMoveDragWithSnapTracking）。</summary>
		private static void PressHeader(Window window, Control header, Point position)
		{
			Avalonia.Input.Pointer pointer = new Avalonia.Input.Pointer(
				Avalonia.Input.Pointer.GetNextFreeId(),
				Avalonia.Input.PointerType.Mouse,
				true);
			PointerPointProperties properties = new PointerPointProperties(
				RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
			PointerPressedEventArgs args = new PointerPressedEventArgs(
				header, pointer, window, position, (ulong)Environment.TickCount64, properties, KeyModifiers.None);
			header.RaiseEvent(args);
		}

		private delegate void DragScenario(SnapTestWindow window, Control header);

		private static void RunDragScenario(DragScenario scenario)
		{
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				SnapTestWindow window = new SnapTestWindow();
				window.Width = 400;
				window.Height = 300;
				window.Show();
				Dispatcher.UIThread.RunJobs(); // 等模板应用，PART_WindowHeader 才在可视树中
				try
				{
					Control header = GetHeader(window);
					Assert.NotNull(header);
					scenario(window, header);
				}
				finally
				{
					window.Close();
				}
			});
		}

		[Fact]
		public void DragTitleBarToScreenTop_OnRelease_Maximizes()
		{
			RunDragScenario(delegate (SnapTestWindow window, Control header)
			{
				window.Position = new PixelPoint(100, 100);
				window.SetCursor(new Point(200, 115)); // 抓住标题栏中部
				PressHeader(window, header, new Point(100, 15));
				window.SetCursor(new Point(200, 0)); // 拖到屏幕顶边（headless 屏幕顶 Y=0）
				window.Position = new PixelPoint(150, 0); // 窗口顶边贴屏 → PositionChanged → 采样吸附
				Dispatcher.UIThread.RunJobs(); // 松手：仍吸附 → 最大化
				Assert.Equal(WindowState.Maximized, window.WindowState);
			});
		}

		[Fact]
		public void DragTitleBarMidScreen_OnRelease_KeepsNormal()
		{
			RunDragScenario(delegate (SnapTestWindow window, Control header)
			{
				window.Position = new PixelPoint(100, 100);
				window.SetCursor(new Point(200, 115));
				PressHeader(window, header, new Point(100, 15));
				window.SetCursor(new Point(200, 300)); // 拖到屏幕中部
				window.Position = new PixelPoint(150, 250);
				Dispatcher.UIThread.RunJobs();
				Assert.Equal(WindowState.Normal, window.WindowState);
			});
		}

		[Fact]
		public void WindowTopEdgeAtScreenTop_CursorMidScreen_KeepsNormal()
		{
			// 误报防护：最大化窗口被拖拽还原的瞬间，窗口顶边天然贴着屏幕顶边，
			// 而光标还在标题栏中部（原生此时并不吸附）——光标条件排除该误判。
			RunDragScenario(delegate (SnapTestWindow window, Control header)
			{
				window.Position = new PixelPoint(100, 100);
				window.SetCursor(new Point(200, 115));
				PressHeader(window, header, new Point(100, 15));
				window.Position = new PixelPoint(150, 0); // 只有窗口顶边贴屏
				window.SetCursor(new Point(200, 115)); // 光标在标题栏中部
				Dispatcher.UIThread.RunJobs();
				Assert.Equal(WindowState.Normal, window.WindowState);
			});
		}

		[Fact]
		public void CursorAtScreenTop_WindowBackAtOrigin_KeepsNormal()
		{
			// 误报防护：拖拽中 Escape 取消把窗口弹回拖拽起点，光标仍停在屏幕顶部
			// ——窗口顶边（已回起点）条件排除该误判。同时覆盖"最后一次采样生效"：
			// 先吸附（顶边）后解除（弹回起点），判定以最后一次采样为准。
			RunDragScenario(delegate (SnapTestWindow window, Control header)
			{
				window.Position = new PixelPoint(100, 100);
				window.SetCursor(new Point(200, 115));
				PressHeader(window, header, new Point(100, 15));
				window.SetCursor(new Point(200, 0));
				window.Position = new PixelPoint(150, 0); // 先拖到顶：吸附
				window.Position = new PixelPoint(100, 100); // Escape 取消：窗口弹回起点 → 最后一次采样解除吸附
				Dispatcher.UIThread.RunJobs();
				Assert.Equal(WindowState.Normal, window.WindowState);
			});
		}

		[Fact]
		public void ScreenLookupFails_OnRelease_KeepsNormal()
		{
			// ScreenFromPoint 拿不到屏幕（如平台实现返回 null）：不吸附、不最大化。
			RunDragScenario(delegate (SnapTestWindow window, Control header)
			{
				window.SetScreenProvider(delegate (PixelPoint point) { return null; });
				window.Position = new PixelPoint(100, 100);
				window.SetCursor(new Point(200, 115));
				PressHeader(window, header, new Point(100, 15));
				window.SetCursor(new Point(200, 0));
				window.Position = new PixelPoint(150, 0);
				Dispatcher.UIThread.RunJobs();
				Assert.Equal(WindowState.Normal, window.WindowState);
			});
		}

		[Fact]
		public void ClickTitleBarWithoutMove_DoesNotMaximize()
		{
			// 未发生移动的点击（拖拽即开始即结束，Begin 清零后无采样）不应最大化。
			RunDragScenario(delegate (SnapTestWindow window, Control header)
			{
				window.Position = new PixelPoint(100, 100);
				window.SetCursor(new Point(200, 115));
				PressHeader(window, header, new Point(100, 15));
				Dispatcher.UIThread.RunJobs(); // 不移动直接松手
				Assert.Equal(WindowState.Normal, window.WindowState);
			});
		}

		[Fact]
		public void SecondDragAfterMaximizeRestore_ClickWithoutMove_DoesNotMaximize()
		{
			// 第一次拖拽贴顶最大化 → 还原为 Normal → 第二次拖拽只点击不移动：
			// Begin 必须清零上一次拖拽遗留的吸附状态，否则第二次点击会被误最大化。
			RunDragScenario(delegate (SnapTestWindow window, Control header)
			{
				window.Position = new PixelPoint(100, 100);
				window.SetCursor(new Point(200, 115));
				PressHeader(window, header, new Point(100, 15));
				window.SetCursor(new Point(200, 0));
				window.Position = new PixelPoint(150, 0);
				Dispatcher.UIThread.RunJobs();
				Assert.Equal(WindowState.Maximized, window.WindowState); // 第一次拖拽：最大化

				window.WindowState = WindowState.Normal; // 手动还原（模拟用户点还原按钮）
				window.Position = new PixelPoint(100, 100);
				window.SetCursor(new Point(200, 115));
				PressHeader(window, header, new Point(100, 15)); // 第二次拖拽：只点击不移动
				Dispatcher.UIThread.RunJobs();
				Assert.Equal(WindowState.Normal, window.WindowState); // Begin 清零：不最大化
			});
		}

		[Fact]
		public void NoResizeWindow_DragToScreenTop_DoesNotMaximize()
		{
			// NoResize 窗口不允许最大化（与双击标题栏行为一致）。
			RunDragScenario(delegate (SnapTestWindow window, Control header)
			{
				window.CanResize = false;
				window.Position = new PixelPoint(100, 100);
				window.SetCursor(new Point(200, 115));
				PressHeader(window, header, new Point(100, 15));
				window.SetCursor(new Point(200, 0));
				window.Position = new PixelPoint(150, 0);
				Dispatcher.UIThread.RunJobs();
				Assert.Equal(WindowState.Normal, window.WindowState);
			});
		}
	}
}
