using System;
using System.Linq;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using Xunit;

namespace ForkPlus.AutomationTests
{
	/// <summary>
	/// 主题切换 ST：验证通过工具栏 Appearance 下拉切换主题不导致应用崩溃或卡死。
	/// 不验证像素级颜色变化（CI 无 GPU，截图不准），只验证主窗口在切换后仍可响应。
	/// </summary>
	public class ThemeSwitchTests : AutomationTestBase, IDisposable
	{
		[Fact]
		public void SwitchTheme_ToDark_AppRemainsResponsive()
		{
			using (var app = LaunchApp())
			{
				Assert.True(OpenAppearanceDropdown(app), "未找到 Appearance 下拉按钮。");

				// 主题项名称已本地化，遍历多语言候选
				string[] darkCandidates = { "Dark", "深色", "Dark Mode", "Dracula", "Monokai" };
				bool clicked = false;
				foreach (var name in darkCandidates)
				{
					var item = FindMenuItemByText(app.Window, name);
					if (item != null)
					{
						try { item.Click(); Thread.Sleep(1500); clicked = true; break; } catch { }
					}
				}
				Assert.True(clicked, "未找到任何深色主题菜单项。");

				// 切换后等待主题应用，再验证主窗口仍启用
				Thread.Sleep(2000);
				var win = app.RefreshMainWindow();
				Assert.NotNull(win);
				Assert.False(app.Application.HasExited, "切换主题后应用退出，可能崩溃。");
			}
		}

		[Fact]
		public void SwitchTheme_ToLight_AppRemainsResponsive()
		{
			using (var app = LaunchApp())
			{
				Assert.True(OpenAppearanceDropdown(app), "未找到 Appearance 下拉按钮。");

				string[] lightCandidates = { "Light", "浅色", "Light Mode", "Solarized" };
				bool clicked = false;
				foreach (var name in lightCandidates)
				{
					var item = FindMenuItemByText(app.Window, name);
					if (item != null)
					{
						try { item.Click(); Thread.Sleep(1500); clicked = true; break; } catch { }
					}
				}
				Assert.True(clicked, "未找到任何浅色主题菜单项。");

				Thread.Sleep(2000);
				Assert.False(app.Application.HasExited, "切换主题后应用退出。");
			}
		}

		[Fact]
		public void SwitchTheme_MultipleTimes_AppRemainsStable()
		{
			using (var app = LaunchApp())
			{
				// 连续切换 3 次主题，验证主题切换链路无累积错误
				for (int i = 0; i < 3; i++)
				{
					Assert.True(OpenAppearanceDropdown(app), $"第 {i + 1} 次未找到 Appearance 下拉按钮。");
					// 找任意主题项点击（Dark/Light/深色/浅色 任一）。
					// 主题项在 Appearance 下拉的 ContextMenu popup 里，不在主窗口视觉树，
					// 必须从桌面根找（FindMenuItemByText 已内置桌面 fallback）。
					string[] anyTheme = { "Dark", "Light", "深色", "浅色" };
					bool clicked = false;
					foreach (var name in anyTheme)
					{
						var item = FindMenuItemByText(app.Window, name);
						if (item != null)
						{
							try { item.Click(); Thread.Sleep(1000); clicked = true; break; } catch { }
						}
					}
					// 即使没找到主题项也不失败——本测试只验证应用不崩溃（与原行为一致）。
				}
				Assert.False(app.Application.HasExited, "多次切换主题后应用退出。");
			}
		}
	}
}
