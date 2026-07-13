using System;
using System.IO;
using FlaUI.Core;
using FlaUI.UIA3;
using Xunit;

namespace ForkPlus.AutomationTests
{
	/// <summary>
	/// 应用启动冒烟测试：验证 ForkPlus.exe 能启动并显示主窗口。
	/// 这是所有系统测试的前置条件——如果启动失败，后续 ST 全部无法运行。
	/// </summary>
	public class AppSmokeTests : AutomationTestBase, IDisposable
	{
		[Fact]
		public void MainWindow_StartsAndExposesWindow()
		{
			using (var app = LaunchApp())
			{
				Assert.NotNull(app.Window);
				// 主窗口标题应包含 "ForkPlus"（应用名）。
				// 注意：首次启动时如果 WelcomeWindow 未被跳过，标题可能不同。
				// AutomationTestBase.EnsureStartupPrerequisites 已预写 settings.json 跳过欢迎页。
				string title = app.Window.Title ?? "";
				Assert.Contains("ForkPlus", title);
			}
		}

		[Fact]
		public void MainWindow_HasChildElements()
		{
			using (var app = LaunchApp())
			{
				Assert.NotNull(app.Window);
				// 主窗口应至少包含一些子元素（工具栏、Tab 控件等）。
				var children = app.Window.FindAllChildren();
				Assert.True(children.Length > 0, "主窗口没有任何子元素，可能未正确渲染。");
			}
		}

		[Fact]
		public void App_RemainsRunningAfterStartup()
		{
			using (var app = LaunchApp())
			{
				Assert.NotNull(app.Window);
				// 等待 2 秒后确认应用仍在运行（没有崩溃）。
				System.Threading.Thread.Sleep(2000);
				Assert.False(app.Application.HasExited, "应用在启动后 2 秒内退出，可能发生了崩溃。");
			}
		}
	}
}
