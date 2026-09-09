// 回归测试（2026-09-09，v4.0.2 用户反馈"主题还是没修好，你看，还有这个一圈，如图"）：
// 根因：Theme.Refresh() 此前在 Windows 10+ 把 SystemAccentBrush 映射到【系统】DWM
// 着色色（HKCU\...\DWM\ColorizationColor，常见默认蓝），而应用内大量样式
// （Button.axaml IsDefault 按钮描边、Tabcontrol 指示条、Textbox 焦点边框、进度条、
// Sidebar 选中图标……）引用 {DynamicResource SystemAccentBrush}——彩色主题下弹窗的
// IsDefault（继续）按钮四周浮出一圈与主题不符的系统色，即用户看到的"一圈"
//（弹窗外圈 WindowBorderBrush 已于同日修复为主题 AccentColor，本缺陷是按钮描边）。
// 非 Windows 平台该路径一直回退主题 AccentBrush，故 Linux/CI 上从来复现不出——
// Windows 用户专属视觉缺陷。修复：SystemAccentBrush 一律取当前主题 AccentBrush。
// 守卫三层：① 资源级——多主题循环断言 SystemAccentBrush 颜色 == 主题 AccentColor；
// ② 控件级——真实 MmSubrepoPushGuidanceWindow（用户报告的弹窗）里 IsDefault（继续）
// 按钮模板 Border#Border 描边 == 主题 AccentColor（"一圈"本体）。全主题循环 + 用例
// 结束还原原主题（进程级共享 App，避免污染同 Collection 其他用例）。
using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Settings;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class SystemAccentBrushThemeConsistencyTests
	{
		// 用户反馈场景 PurpleDark + 默认 Dark + 另一彩色系 GreenDark（彩色主题最能暴露
		// "一圈"系统色不匹配；Light/Dark 灰系 accent 恰好接近系统蓝时不明显）。
		private static readonly global::ForkPlus.UI.ThemeType[] SampleThemes =
		{
			global::ForkPlus.UI.ThemeType.Dark,
			global::ForkPlus.UI.ThemeType.PurpleDark,
			global::ForkPlus.UI.ThemeType.GreenDark,
		};

		private static void SwitchTheme(global::ForkPlus.UI.ThemeType theme)
		{
			// 生产换肤路径：设置持久化 + 字典热替换 + SyncThemeVariant + Theme.Refresh +
			// RefreshWindowBorderBrush（SystemAccentBrush 由 Theme.Refresh 写入）。
			new global::ForkPlus.UI.Commands.SwitchApplicationThemeCommand().Execute(theme);
			Dispatcher.UIThread.RunJobs();
		}

		private static Color? TryGetThemeAccentColor()
		{
			Application app = Application.Current;
			return app != null
				&& app.TryGetResource("AccentColor", app.ActualThemeVariant, out object value)
				&& value is Color color ? color : (Color?)null;
		}

		// ===== ① 资源级：多主题循环，SystemAccentBrush 必须等于主题 AccentColor =====

		[Fact]
		public void SystemAccentBrush_EqualsThemeAccentColor_AcrossThemes()
		{
			string failure = HeadlessAppBootstrap.Run(delegate
			{
				global::ForkPlus.UI.ThemeType original = ForkPlusSettings.Default.Theme;
				try
				{
					foreach (global::ForkPlus.UI.ThemeType theme in SampleThemes)
					{
						SwitchTheme(theme);
						Color? accent = TryGetThemeAccentColor();
						if (accent == null)
						{
							return theme + ": 主题 AccentColor 资源未解析";
						}
						Application app = Application.Current;
						if (!app.TryGetResource("SystemAccentBrush", app.ActualThemeVariant, out object value)
							|| value is not ISolidColorBrush brush)
						{
							return theme + ": SystemAccentBrush 未解析为纯色画刷（实测 " + (value?.GetType().Name ?? "<null>") + "）";
						}
						if (brush.Color != accent.Value)
						{
							// 修复前 Windows 形态：DWM 系统强调色（典型 #0078D4 蓝）≠ 主题色
							return theme + ": SystemAccentBrush=" + brush.Color + " ≠ 主题 AccentColor=" + accent.Value
								+ "（若为系统默认蓝 #0078D4 即回归：又读到系统 DWM 强调色）";
						}
					}
					return "";
				}
				finally
				{
					SwitchTheme(original);
				}
			});
			Assert.True(failure.Length == 0, failure);
		}

		// ===== ② 控件级：用户报告的弹窗里，IsDefault（继续）按钮描边跟随主题 =====

		[Fact]
		public void IsDefaultButtonBorderRing_FollowsThemeAccent_PurpleDark()
		{
			string failure = HeadlessAppBootstrap.Run(delegate
			{
				global::ForkPlus.UI.ThemeType original = ForkPlusSettings.Default.Theme;
				global::ForkPlus.UI.Dialogs.MmSubrepoPushGuidanceWindow window = null;
				try
				{
					SwitchTheme(global::ForkPlus.UI.ThemeType.PurpleDark);
					window = new global::ForkPlus.UI.Dialogs.MmSubrepoPushGuidanceWindow("/tmp/workspace-demo");
					window.Show();
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
					Dispatcher.UIThread.RunJobs();

					Color? accent = TryGetThemeAccentColor();
					if (accent == null)
					{
						return "PurpleDark: 主题 AccentColor 资源未解析";
					}
					Button submit = window.GetVisualDescendants().OfType<Button>().FirstOrDefault((Button b) => b.IsDefault);
					if (submit == null)
					{
						return "找不到 IsDefault（继续）按钮";
					}
					// Button.axaml 模板：^[IsDefault=true] /template/ Border#Border 描边 = SystemAccentBrush
					Border border = submit.GetVisualDescendants().OfType<Border>().FirstOrDefault((Border b) => b.Name == "Border");
					if (border == null)
					{
						return "找不到按钮模板 Border#Border";
					}
					if (border.BorderBrush is not ISolidColorBrush ringBrush)
					{
						return "继续按钮描边不是纯色画刷（实测 " + (border.BorderBrush?.GetType().Name ?? "<null>") + "）";
					}
					if (ringBrush.Color != accent.Value)
					{
						return "继续按钮一圈描边=" + ringBrush.Color + " ≠ 主题 AccentColor=" + accent.Value
							+ "（PurpleDark 应为 #A855F7；若为 #0078D4 系统蓝即回归到系统 DWM 强调色）";
					}
					return "";
				}
				finally
				{
					window?.Close();
					SwitchTheme(original);
				}
			});
			Assert.True(failure.Length == 0, failure);
		}
	}
}
