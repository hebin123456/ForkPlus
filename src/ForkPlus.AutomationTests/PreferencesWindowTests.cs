using System;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using Xunit;

namespace ForkPlus.AutomationTests
{
	/// <summary>
	/// 偏好设置窗口 ST：通过 File 菜单打开 Preferences 窗口，验证窗口能弹出、可关闭。
	/// </summary>
	public class PreferencesWindowTests : AutomationTestBase, IDisposable
	{
		[Fact]
		public void OpenPreferences_WindowAppears()
		{
			using (var app = LaunchApp())
			{
				// 展开 File 菜单（WPF 菜单懒加载，必须先展开父项）
				Assert.True(ExpandRootMenu(app, "File") || ExpandRootMenu(app, "文件"),
					"未找到 File 菜单");

				// 点击 "Preferences..."
				var prefItem = FindMenuItemByText(app.Window, "Preferences");
				if (prefItem == null) prefItem = FindMenuItemByText(app.Window, "偏好设置");
				Assert.NotNull(prefItem);
				try { prefItem.Click(); } catch { }

				var win = WaitForTopLevelWindow(app, "Preferences", System.TimeSpan.FromSeconds(10))
						  ?? WaitForTopLevelWindow(app, "偏好设置", System.TimeSpan.FromSeconds(3));
				Assert.NotNull(win);

				// 给 Preferences 窗口内部 tab 渲染一些时间
				Thread.Sleep(1000);

				CloseWindow(win);
				Thread.Sleep(500);
				Assert.False(app.Application.HasExited, "关闭 Preferences 窗口后主应用退出。");
			}
		}
	}
}
