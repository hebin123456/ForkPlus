// 主题系统完整性守卫（2026-09-04，"有些组件样式没包进去、切换主题突兀"研究产物）：
// 全仓三路扫描（axaml 硬编码色 / C# 硬编码色 / 皮肤字典一致性）发现两类机制级缺陷，本测试守卫：
//
// 缺陷1（切换突兀主根因）：FluentTheme + AvaloniaEdit(Fluent) + OxyPlot(Default) 三个外来
//   主题的明暗只看 Application.RequestedThemeVariant，而全仓库从未设置它——Fluent 系控件
//   明暗跟随操作系统而非应用皮肤，暗皮肤下编辑器内搜索面板/未覆盖兜底控件呈亮色"亮岛"。
//   修复：App.SyncThemeVariant 按皮肤基底明暗（IsDarkBase）设置变体，加载与切换两条路径都调。
//
// 缺陷2（皮肤字典不完备）：Brushes.axaml 引用的 {DynamicResource X} Color key 没有
//   "每个皮肤都必须定义"的守卫——ClosableTabItem.MouseOver.ButtonColor 被 Brushes.axaml:118
//   引用但 22 个皮肤字典全部缺失（DynamicResource 静默解析失败，悬停关闭按钮无 hover 色）。
//   修复：22 个皮肤全部补齐该 key（Light 基底 #767676 / Dark 基底 #CCCCCC）。
//   本测试 B 遍历 Brushes.axaml 全部 DynamicResource 引用 × 22 皮肤逐个解析，任何
//   "引用了但皮肤没定义"的 key 都会在此失败——将来加新 key 漏加皮肤定义立即暴露。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using ForkPlus.UI;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class ThemeSystemIntegrityTests
	{
		// 仓库根：从测试输出目录向上找含 src/ForkPlus.Tests 的目录
		//（EvidenceScreenshotTests.FindRepoRoot 同款）
		private static string FindRepoRoot()
		{
			string dir = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Parent?.Parent?.Parent?.FullName;
			while (dir != null && !Directory.Exists(Path.Combine(dir, "src", "ForkPlus.Tests")))
			{
				dir = Directory.GetParent(dir)?.FullName;
			}
			return dir ?? throw new InvalidOperationException("找不到仓库根（src/ForkPlus.Tests 不存在）");
		}

		// 换肤：加新皮肤 include 再移除旧的（与 App.InitializeTheme / SwitchApplicationThemeCommand 同机制）
		private static void SwitchSkin(Application app, string skin)
		{
			var oldInclude = app.Resources.MergedDictionaries
				.OfType<ResourceInclude>()
				.FirstOrDefault(i => i.Source?.OriginalString.Contains("Theme/Generic.") == true);
			var newInclude = new ResourceInclude(new Uri("avares://ForkPlus/App.axaml"))
			{
				Source = new Uri("avares://ForkPlus/Theme/Generic." + skin + ".axaml")
			};
			app.Resources.MergedDictionaries.Add(newInclude);
			if (oldInclude != null)
			{
				app.Resources.MergedDictionaries.Remove(oldInclude);
			}
			Dispatcher.UIThread.RunJobs();
		}

		// 皮肤文件名 ↔ ThemeType（SkinName 的逆映射，测试数据与 ThemeTypeExtensions.AllThemes 一一对应）
		private static readonly (string skin, ThemeType theme)[] SkinThemePairs =
		{
			("Light", ThemeType.Light), ("Dark", ThemeType.Dark),
			("SolarizedLight", ThemeType.SolarizedLight), ("SolarizedDark", ThemeType.SolarizedDark),
			("GitHubLight", ThemeType.GitHubLight), ("GitHubDark", ThemeType.GitHubDark),
			("Dracula", ThemeType.Dracula), ("Monokai", ThemeType.Monokai),
			("PurpleLight", ThemeType.PurpleLight), ("PurpleDark", ThemeType.PurpleDark),
			("GreenLight", ThemeType.GreenLight), ("GreenDark", ThemeType.GreenDark),
			("RedLight", ThemeType.RedLight), ("RedDark", ThemeType.RedDark),
			("OrangeLight", ThemeType.OrangeLight), ("OrangeDark", ThemeType.OrangeDark),
			("YellowLight", ThemeType.YellowLight), ("YellowDark", ThemeType.YellowDark),
			("CyanLight", ThemeType.CyanLight), ("CyanDark", ThemeType.CyanDark),
			("BlueLight", ThemeType.BlueLight), ("BlueDark", ThemeType.BlueDark)
		};

		/// <summary>守卫 A：Fluent 变体随皮肤基底明暗同步。暗基底皮肤 → ThemeVariant.Dark，
		/// 亮基底 → Light。遍历全部 22 个皮肤（含 12 暗基底 + 10 亮基底），防止将来新增
		/// 皮肤忘归入基底或 SyncThemeVariant 回归。</summary>
		[Fact]
		public void SyncThemeVariant_FollowsSkinBaseDarkness_ForAllSkins()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				foreach (var (skin, theme) in SkinThemePairs)
				{
					App.SyncThemeVariant(theme);
					ThemeVariant expected = theme.IsDarkBase() ? ThemeVariant.Dark : ThemeVariant.Light;
					Assert.True(ThemeVariant.Equals(Application.Current!.RequestedThemeVariant, expected),
						skin + ": RequestedThemeVariant 应为 " + expected + "（IsDarkBase=" + theme.IsDarkBase() + "），实际 "
						+ Application.Current.RequestedThemeVariant);
				}
				// 还原 Light，避免污染同进程后续测试
				App.SyncThemeVariant(ThemeType.Light);
				return 0;
			}).GetAwaiter().GetResult();
		}

		/// <summary>守卫 B：Brushes.axaml 引用的每个 {DynamicResource X} key 在全部 22 个皮肤
		/// 下都必须可解析。任何"引用了但皮肤字典未定义"的 key（如修复前的
		/// ClosableTabItem.MouseOver.ButtonColor）在此失败。</summary>
		[Fact]
		public void AllSkins_EveryBrushResourceReference_Resolves()
		{
			HeadlessAppBootstrap.EnsureStarted();

			// 1) 从源文件解析 Brushes.axaml，提取全部 {DynamicResource X} 引用 key。
			// 直接读磁盘源文件而非 AssetLoader 流——编译后的 .axaml 以 XamlIl 形式嵌入，
			// 运行时 ResourceInclude 走预编译通道可加载，但 AssetLoader.Open 按流打开
			// .axaml 不可靠（实测 not found）；读源文件还额外守卫"源码与运行时一致"。
			string brushesPath = Path.Combine(FindRepoRoot(), "src", "ForkPlus", "Theme", "Styles", "Brushes", "Brushes.axaml");
			List<string> referencedKeys = new List<string>();
			{
				XDocument doc = XDocument.Load(brushesPath);
				Regex dynRes = new Regex(@"^\{DynamicResource\s+(.+?)\}$");
				foreach (XAttribute attr in doc.Descendants().Attributes())
				{
					Match m = dynRes.Match(attr.Value);
					if (m.Success)
					{
						referencedKeys.Add(m.Groups[1].Value);
					}
				}
			}
			Assert.True(referencedKeys.Count > 200,
				"Brushes.axaml 应提取到 200+ DynamicResource 引用，实际 " + referencedKeys.Count + "（解析失败？）");

			// 2) 22 个皮肤逐个加载，全部 key 必须解析成功
			List<string> failures = new List<string>();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var app = Application.Current!;
				foreach (var (skin, _) in SkinThemePairs)
				{
					SwitchSkin(app, skin);
					foreach (string key in referencedKeys)
					{
						try
						{
							bool found = app.TryFindResource(key, app.ActualThemeVariant, out object? value);
							if (!found || value == null)
							{
								failures.Add(skin + ": " + key);
							}
						}
						catch (Exception ex)
						{
							failures.Add(skin + ": " + key + "（抛异常: " + ex.GetType().Name + "）");
						}
					}
				}
				// 还原默认 Light 皮肤 + 变体，避免污染同进程后续 headless 测试
				SwitchSkin(app, "Light");
				App.SyncThemeVariant(ThemeType.Light);
				return 0;
			}).GetAwaiter().GetResult();

			Assert.True(failures.Count == 0,
				"以下 " + failures.Count + " 处资源引用在对应皮肤下解析失败（皮肤字典缺定义）：" + string.Join("; ", failures.Take(20)));
		}

		/// <summary>守卫 C：切换命令全链路——Execute(Dark) 后 Fluent 变体必须变 Dark 且
		/// 皮肤字典真实替换为 Generic.Dark。这是用户实际点菜单换肤的路径，A/B 只测了
		/// 组件级，C 保证命令入口没漏调同步。</summary>
		[Fact]
		public void SwitchApplicationThemeCommand_SyncsFluentVariant_EndToEnd()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var app = Application.Current!;
				var command = new global::ForkPlus.UI.Commands.SwitchApplicationThemeCommand();

				command.Execute(ThemeType.Monokai, followSystemTheme: false);
				RunJobsSkipRender();
				Assert.True(ThemeVariant.Equals(app.RequestedThemeVariant, ThemeVariant.Dark),
					"切到 Monokai（暗基底）后 Fluent 变体应为 Dark，实际 " + app.RequestedThemeVariant);
				Assert.True(App.FindThemeResourceInclude()?.Source?.OriginalString.Contains("Generic.Monokai") == true,
					"主题字典应为 Generic.Monokai.axaml");

				command.Execute(ThemeType.Light, followSystemTheme: false);
				RunJobsSkipRender();
				Assert.True(ThemeVariant.Equals(app.RequestedThemeVariant, ThemeVariant.Light),
					"切回 Light 后 Fluent 变体应为 Light，实际 " + app.RequestedThemeVariant);
				Assert.True(App.FindThemeResourceInclude()?.Source?.OriginalString.Contains("Generic.Light") == true,
					"主题字典应为 Generic.Light.axaml");

				// 还原设置默认值，避免污染 ForkPlusSettings（UseCustomColors 被命令置 false 等副作用）
				return 0;
			}).GetAwaiter().GetResult();
		}

		/// <summary>RunJobs 但跳过 Render 优先级（2026-09-06 CI 实证修复）：全量 RunJobs 会
		/// 派发 MediaContext.Render（DispatcherPriority.Render 调度，Avalonia 源码实证），
		/// 主题字典热替换瞬间 Compositor 提交批次会跨线程引用旧 Brush——headless Compositor
		/// 的渲染线程与 UI 线程在慢速 runner 上偶发 Brush.OnReferencedFromCompositor 的
		/// VerifyAccess 竞态（InvalidOperationException）。本测试断言的是同步逻辑状态
		/// （RequestedThemeVariant / 主题字典 Uri），Execute 全同步无 dispatcher 排队步骤，
		/// 跳过渲染 pass 不影响断言；Render 及以下任务留在队列由后续测试自然消化。
		/// RunJobs(priority) 语义（沙盒探针实证）：执行优先级值 >= priority 的任务；
		/// Send(9) > Render(4)，故仅执行 Send 级任务。</summary>
		private static void RunJobsSkipRender()
		{
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Send);
		}
	}
}
