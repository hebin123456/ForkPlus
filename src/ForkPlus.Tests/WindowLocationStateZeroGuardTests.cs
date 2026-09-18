// 修复（2026-09-17，"每次启动主窗口缩成极小窗，settings.json 里 MainWindowLocationState
// 的 Width/Height=0.0"）的回归守卫。
// 根因链：关闭时 Window_Closing 被二次触发（lifetime.Shutdown 重入 CloseCore，此时
// PlatformImpl 已销毁、句柄 IntPtr.Zero）→ GetWindowPlacement 静默失败 → 全零
// WindowLocationState 落盘 → 下次启动把 0×0 应用到窗口。
// 修复分三层：读端（GetPlacement 检查返回值 + 失败回退 + SanitizeGeometry）、
// 恢复端（SetWindowLocationState 零值兜底）、解码端（Decode 拒绝退化几何，见
// WindowLocationStateCodecTests）。本文件用 headless 窗口锁住读端/恢复端行为：
// 无论输入或窗口状态多退化，进出这条链路的状态永远不携带 0×0。
using Avalonia;
using ForkPlus.UI;
using ForkPlus.UI.Helpers;
using Xunit;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class WindowLocationStateZeroGuardTests
	{
		[Fact]
		public void SetWindowLocationState_AllZeroState_FallsBackToPositiveDefaultSize()
		{
			HeadlessAppBootstrap.EnsureStarted();
			(double width, double height) = HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window();
				window.Show();
				try
				{
					// 用户被污染的 settings.json 内容：全零几何直接喂给恢复端
					window.SetWindowLocationState(new WindowLocationState(0.0, 0.0, 0.0, 0.0, global::Avalonia.Controls.WindowState.Normal));
					return (window.Width, window.Height);
				}
				finally
				{
					window.Close();
				}
			});

			Assert.True(width > 0.0, "零宽状态不得把窗口宽度设为 0，应回退默认 1000，实际 " + width);
			Assert.True(height > 0.0, "零高状态不得把窗口高度设为 0，应回退默认 600，实际 " + height);
		}

		[Fact]
		public void SetWindowLocationState_NanSize_FallsBackToPositiveDefaultSize()
		{
			HeadlessAppBootstrap.EnsureStarted();
			(double width, double height) = HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window();
				window.Show();
				try
				{
					window.SetWindowLocationState(new WindowLocationState(100.0, 100.0, double.NaN, double.NaN, global::Avalonia.Controls.WindowState.Normal));
					return (window.Width, window.Height);
				}
				finally
				{
					window.Close();
				}
			});

			Assert.True(width > 0.0, "NaN 宽不得传播到窗口，应回退默认 1000，实际 " + width);
			Assert.True(height > 0.0, "NaN 高不得传播到窗口，应回退默认 600，实际 " + height);
		}

		[Fact]
		public void GetWindowLocationState_NeverReturnsZeroSize()
		{
			HeadlessAppBootstrap.EnsureStarted();
			(double width, double height) = HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window();
				window.Show();
				try
				{
					// headless 平台句柄非真实 Win32 HWND（Windows runner 上 GetWindowPlacement
					// 失败 → 回退 Avalonia 属性读取；Unix runner 直接走 Avalonia 路径），
					// 两条路径都必须产出正尺寸——这正是"窗口已销毁/句柄无效"时的兜底行为。
					WindowLocationState state = window.GetWindowLocationState();
					return (state.Width, state.Height);
				}
				finally
				{
					window.Close();
				}
			});

			Assert.True(width > 0.0, "读取端绝不返回零宽（含句柄无效回退路径），实际 " + width);
			Assert.True(height > 0.0, "读取端绝不返回零高（含句柄无效回退路径），实际 " + height);
		}

	[Fact]
		public void GetWindowLocationState_ValidStateRoundTrips()
		{
			HeadlessAppBootstrap.EnsureStarted();
			(double setWidth, double setHeight, double getWidth, double getHeight) = HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window();
				window.Show();
				try
				{
					window.SetWindowLocationState(new WindowLocationState(20.0, 20.0, 900.0, 500.0, global::Avalonia.Controls.WindowState.Normal));
					// Set 同步赋值 Width/Height 属性；headless 的布局/平台 resize 异步落地，
					// 这里只断言：①正常几何原样应用到属性（不被消毒逻辑误改）；
					// ②读取端返回正尺寸（不因回退路径归零）。
					double w = window.Width;
					double h = window.Height;
					WindowLocationState state = window.GetWindowLocationState();
					return (w, h, state.Width, state.Height);
				}
				finally
				{
					window.Close();
				}
			});

			Assert.Equal(900.0, setWidth);
			Assert.Equal(500.0, setHeight);
			Assert.True(getWidth > 0.0, "正常几何经读取端不得归零，实际 " + getWidth);
			Assert.True(getHeight > 0.0, "正常几何经读取端不得归零，实际 " + getHeight);
		}
	}
}
