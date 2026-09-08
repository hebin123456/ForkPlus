// 复现测试（2026-09-08，"彩色主题下弹窗外圈颜色不一致"）：
// Window.axaml 两套窗口模板（CustomWindow / ForkPlusDialogWindowStyle）的 BorderBrush 均引用
// {DynamicResource WindowBorderBrush}，而该资源由 App.RefreshWindowBorderBrush() 注入——
// 原版浅/深均写死 #3BACED 亮蓝，导致 Purple/Green 等彩色主题下弹窗四周浮出一圈突兀亮蓝
// （Solarized 蓝色系下恰好协调）。修复后边框应跟随当前主题 AccentColor。
// 本测试走真实主题切换命令链路（SwitchApplicationThemeCommand.Execute），验证：
//   1) 切到紫色浅色 → WindowBorderBrush == 该主题 AccentColor（#7C3AED）；
//   2) 切到 Solarized 浅色 → 跟随其 AccentColor（#268BD2）；
//   3) 切回 Light 恢复，避免污染后续测试。
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using ForkPlus.Settings;
using ForkPlus.UI;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class WindowBorderBrushThemeTests
	{
		private static IBrush GetWindowBorderBrush()
		{
			object res = null;
			bool found = global::Avalonia.Application.Current.TryGetResource("WindowBorderBrush", global::Avalonia.Application.Current.ActualThemeVariant, out res);
			Assert.True(found, "WindowBorderBrush 资源应已由 RefreshWindowBorderBrush 注入");
			return res as IBrush;
		}

		private static void SwitchTo(ThemeType theme)
		{
			MainWindow.Commands.SwitchApplicationTheme.Execute(theme);
			Dispatcher.UIThread.RunJobs();
		}

		[Fact]
		public void WindowBorderBrush_FollowsThemeAccent_AfterThemeSwitch()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				try
				{
					// Linux/headless 下 IsSystemAccentBrushEnabled() 恒 false（注册表不可用），
					// RefreshWindowBorderBrush 走主题 AccentColor 分支——正是被测路径。
					SwitchTo(ThemeType.PurpleLight);
					IBrush purpleBrush = GetWindowBorderBrush();
					Assert.IsType<SolidColorBrush>(purpleBrush);
					// Colors.PurpleLight.axaml: AccentColor = #7C3AED（不再是写死的 #3BACED 亮蓝）
					Assert.Equal(global::Avalonia.Media.Color.Parse("#7C3AED"), ((SolidColorBrush)purpleBrush).Color);

					// Solarized 浅色跟随其主题色（与原亮蓝观感最接近，用户认可的现状基线）
					SwitchTo(ThemeType.SolarizedLight);
					IBrush solBrush = GetWindowBorderBrush();
					Assert.IsType<SolidColorBrush>(solBrush);
					Assert.Equal(global::Avalonia.Media.Color.Parse("#268BD2"), ((SolidColorBrush)solBrush).Color);

					// 深色主题同样跟随（原版深色也是亮蓝，一并修正；PurpleDark Accent=#A855F7）
					SwitchTo(ThemeType.PurpleDark);
					IBrush darkBrush = GetWindowBorderBrush();
					Assert.IsType<SolidColorBrush>(darkBrush);
					Assert.NotEqual(global::Avalonia.Media.Color.Parse("#3BACED"), ((SolidColorBrush)darkBrush).Color);

					// 恢复默认，避免污染同 Collection 的其它测试
					SwitchTo(ThemeType.Light);
				}
				finally
				{
					// 兜底恢复（断言失败路径）
					Dispatcher.UIThread.RunJobs();
					MainWindow.Commands.SwitchApplicationTheme.Execute(ThemeType.Light);
					Dispatcher.UIThread.RunJobs();
				}
			});
		}
	}
}
