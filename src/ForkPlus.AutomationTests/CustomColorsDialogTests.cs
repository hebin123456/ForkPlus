using System;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using Xunit;

namespace ForkPlus.AutomationTests
{
	/// <summary>
	/// 自定义颜色对话框 ST：通过 Appearance 下拉打开 Custom Colors 对话框，
	/// 验证对话框能弹出、点击 Random Palette 不崩溃、能通过 Cancel/OK 关闭。
	/// </summary>
	public class CustomColorsDialogTests : AutomationTestBase, IDisposable
	{
		[Fact]
		public void OpenCustomColorsDialog_WindowAppears()
		{
			using (var app = LaunchApp())
			{
				Assert.True(OpenAppearanceDropdown(app), "未找到 Appearance 下拉按钮。");

				// 点击 "Custom Colors..."（中文 "自定义颜色..."）
				var item = FindMenuItemByText(app.Window, "Custom Colors");
				if (item == null) item = FindMenuItemByText(app.Window, "自定义颜色");
				Assert.NotNull(item);
				try { item.Click(); } catch { }

				var win = WaitForTopLevelWindow(app, "Custom Colors", System.TimeSpan.FromSeconds(10))
						  ?? WaitForTopLevelWindow(app, "自定义颜色", System.TimeSpan.FromSeconds(3));
				Assert.NotNull(win);

				CloseWindow(win);
				Thread.Sleep(500);
				Assert.False(app.Application.HasExited, "关闭自定义颜色对话框后主应用退出。");
			}
		}

		[Fact]
		public void CustomColorsDialog_RandomPalette_DoesNotCrash()
		{
			using (var app = LaunchApp())
			{
				Assert.True(OpenAppearanceDropdown(app));

				var item = FindMenuItemByText(app.Window, "Custom Colors")
					?? FindMenuItemByText(app.Window, "自定义颜色");
				Assert.NotNull(item);
				try { item.Click(); } catch { }

				var win = WaitForTopLevelWindow(app, "Custom Colors", System.TimeSpan.FromSeconds(10))
						  ?? WaitForTopLevelWindow(app, "自定义颜色", System.TimeSpan.FromSeconds(3));
				Assert.NotNull(win);

				// 点击 "Random Palette"（中文 "随机配色"）
				var randomBtn = win.FindFirstDescendant(cf => cf.ByName("Random Palette"))
					?? win.FindFirstDescendant(cf => cf.ByName("随机配色"));
				Assert.NotNull(randomBtn);
				try { randomBtn.Click(); Thread.Sleep(1000); } catch { }

				// 验证应用未崩溃
				Assert.False(app.Application.HasExited, "点击 Random Palette 后应用退出。");

				// 点击 Cancel 关闭对话框（不保存随机配色）
				var cancelBtn = win.FindFirstDescendant(cf => cf.ByName("Cancel"))
					?? win.FindFirstDescendant(cf => cf.ByName("取消"));
				if (cancelBtn != null)
				{
					try { cancelBtn.Click(); Thread.Sleep(500); } catch { }
				}
				else
				{
					CloseWindow(win);
				}
			}
		}
	}
}
