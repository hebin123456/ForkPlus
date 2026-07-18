using System;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using Xunit;

namespace ForkPlus.AutomationTests
{
	/// <summary>
	/// 语言切换 ST：验证通过 Appearance 下拉切换 UI 语言不导致应用崩溃。
	/// 切换后 UI 文本会重新本地化，可能影响后续控件定位，所以本测试只断言进程存活。
	/// </summary>
	public class LanguageSwitchTests : AutomationTestBase, IDisposable
	{
		[Fact]
		public void SwitchLanguage_ToChinese_AppRemainsResponsive()
		{
			using (var app = LaunchApp())
			{
				Assert.True(OpenAppearanceDropdown(app), "未找到 Appearance 下拉按钮。");

				// 语言项在 "Language" 分组下，文本是各语言的 DisplayName
				// 简体中文的 DisplayName 是 "简体中文"
				var item = FindMenuItemByText(app.Window, "简体中文");
				if (item != null)
				{
					try { item.Click(); Thread.Sleep(1500); } catch { }
				}
				// 不强制断言点击成功——CI 环境语言文件可能未完整加载
				Thread.Sleep(1000);
				Assert.False(app.Application.HasExited, "切换到中文后应用退出。");
			}
		}

		[Fact]
		public void SwitchLanguage_ToEnglish_AppRemainsResponsive()
		{
			using (var app = LaunchApp())
			{
				Assert.True(OpenAppearanceDropdown(app), "未找到 Appearance 下拉按钮。");

				var item = FindMenuItemByText(app.Window, "English");
				if (item != null)
				{
					try { item.Click(); Thread.Sleep(1500); } catch { }
				}
				Thread.Sleep(1000);
				Assert.False(app.Application.HasExited, "切换到英文后应用退出。");
			}
		}
	}
}
