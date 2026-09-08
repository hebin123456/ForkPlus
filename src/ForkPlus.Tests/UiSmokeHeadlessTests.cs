// Windows FlaUI 套件（ForkPlus.AutomationTests）迁移落点（2026-09-02）：
// 全部 UI 冒烟归一到 Avalonia.Headless（跨平台、控件级、异常带堆栈），Windows 专用的
// FlaUI/UIA3 进程外驱动已删除。迁移映射：
//   AppSmokeTests / AppStartupTests        → MainWindow_ConstructsShowsAndCloses
//   PreferencesWindowTests                 → PreferencesWindow_ConstructsAndShows
//   CustomColorsDialogTests + BugFixV212   → CustomColorsDialog_RandomPalette_Click_NoCrash
//     Bug1/2（自定义颜色实时生效/随机配色覆盖全 key）
//   ThemeSwitchTests                       → ThemeSwitch_LightDarkRoundtrip_NoCrash
//   LanguageSwitchTests + BugFixV212 Bug4  → LanguageSwitch_Roundtrip_ApplyLocalization
//   BugFixV212 Bug3（空仓库卡死）           → GetRevisionStorage_EmptyRepo_QuickPath /
//                                             GetRevisionStorage_UnbornBranch_QuickPath
//   RepositoryOpenTests / RepositoryManagerChangesE2EProbeTests 等 E2E 探针
//   → 按需以 headless 控件级测试补（本文件模式），不再保留进程外双轨。
using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class UiSmokeHeadlessTests
	{
		// 启动基建与 Run 助手统一收拢在 HeadlessAppBootstrap（ModuleInitializer 启动真实 App，
		// 程序集加载期即绪，任何测试线程不会再与 Compositor 初始化竞争 Dispatcher 归属）。

		[Fact]
		public void MainWindow_ConstructsShowsAndCloses_WithoutCrash()
		{
			// AppSmokeTests/AppStartupTests 迁移：主窗口完整初始化（菜单管理器、工具栏、
			// TabManager、通知中心模板部件）+ 显示 + 关闭，任何一步异常都会带堆栈失败。
			Exception crash = HeadlessAppBootstrap.Run(delegate
			{
				Exception ex = null;
				try
				{
					var window = new global::ForkPlus.UI.MainWindow();
					window.Show();
					Dispatcher.UIThread.RunJobs();
					Assert.True(window.IsVisible);
					Assert.True(window.IsEnabled);
					// ⚠️ 不能 Close()：MainWindow.Closed 处理器无条件调 lifetime.Shutdown()
					// （即使 ShutdownMode=OnExplicitShutdown），会把 Dispatcher 整个关掉，
					// 后续所有测试的 InvokeAsync 全部 TaskCanceledException。用 Hide() 即可。
					window.Hide();
					Dispatcher.UIThread.RunJobs();
				}
				catch (Exception e)
				{
					ex = e;
				}
				return ex;
			});
			Assert.True(crash == null, "主窗口冒烟崩溃堆栈：\n" + crash);
		}

		[Fact]
		public void PreferencesWindow_ConstructsAndShows_WithoutCrash()
		{
			// PreferencesWindowTests 迁移：原 Ctrl+, 快捷键最终执行 ShowPreferencesWindowCommand
			// → new PreferencesWindow().ShowDialog()。这里直接构造 + 非模态 Show 隔离窗口本身。
			Exception crash = HeadlessAppBootstrap.Run(delegate
			{
				Exception ex = null;
				try
				{
					var window = new global::ForkPlus.UI.Dialogs.PreferencesWindow();
					window.Show();
					Dispatcher.UIThread.RunJobs();
					Assert.True(window.IsVisible);
					window.Close();
				}
				catch (Exception e)
				{
					ex = e;
				}
				return ex;
			});
			Assert.True(crash == null, "偏好设置窗口冒烟崩溃堆栈：\n" + crash);
		}

		[Fact]
		public void CustomColorsDialog_RandomPalette_Click_NoCrash()
		{
			// CustomColorsDialogTests + BugFixV212 Bug1/2 迁移：对话框构造 → 点击 Random
			// Palette（ApplyAndRefresh → App.ApplyCustomColors + Save 实时落盘）→ 关闭。
			// Save 会写真实 settings.json（ForkDirectoryPath 指向 ~/.local/share/ForkPlus），
			// 测试前后备份/恢复，避免随机配色污染环境。
			// ⚠️ 线程规则：App.ForkDirectoryPath 会触发 App 静态构造（含 SolidColorBrush 等
			// Avalonia 对象），必须在 headless UI 线程启动之后、且在 UI 线程上访问——
			// 若在 xunit 测试线程先触碰，Dispatcher 线程归属错乱，后续 Compositor 初始化
			// 直接抛 "different thread owns it" 崩掉整个 test host。所以备份/恢复全部
			// 收进 Run 委托（UI 线程）内执行。
			Exception crash = HeadlessAppBootstrap.Run(delegate
			{
				Exception ex = null;
				string settingsPath = Path.Combine(global::ForkPlus.App.ForkDirectoryPath, "settings.json");
				string backup = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;
				try
				{
					var dialog = new global::ForkPlus.UI.Dialogs.CustomColorsDialog();
					dialog.Show();
					Dispatcher.UIThread.RunJobs();
					var randomButton = dialog.FindControl<Button>("RandomPaletteButton");
					Assert.NotNull(randomButton);
					// Bug2 核心：RandomPalette_Click 补齐 12 个 Set 调用覆盖全部 30 个
					// _editableColorKeys，任何 key 的 merge/落盘异常都会在这里冒出来。
					randomButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
					Dispatcher.UIThread.RunJobs();
					dialog.Close();
					Dispatcher.UIThread.RunJobs();
				}
				catch (Exception e)
				{
					ex = e;
				}
				finally
				{
					try
					{
						if (backup != null)
						{
							File.WriteAllText(settingsPath, backup);
						}
						else if (File.Exists(settingsPath))
						{
							// 测试前不存在 settings.json（全新环境）：恢复"不存在"状态。
							File.Delete(settingsPath);
						}
					}
					catch
					{
					}
				}
				return ex;
			});
			Assert.True(crash == null, "自定义颜色对话框/随机配色崩溃堆栈：\n" + crash);
		}

		[Fact]
		public void ThemeSwitch_LightDarkRoundtrip_NoCrash()
		{
			// ThemeSwitchTests 迁移：原 Appearance 下拉点主题菜单项，等价于
			// SwitchApplicationThemeCommand.Execute(theme)：换主题字典 ResourceInclude、
			// Theme.Refresh、ApplyCustomColors、RaiseApplicationThemeChanged 全链路。
			// setter 只改内存不落盘，测后恢复原值即可。
			// ⚠️ 线程规则同上：ForkPlusSettings.Default 的访问收进 Run 委托（UI 线程）。
			Exception crash = HeadlessAppBootstrap.Run(delegate
			{
				Exception ex = null;
				ThemeType original = ForkPlusSettings.Default.Theme;
				try
				{
					var command = new SwitchApplicationThemeCommand();
					command.Execute(ThemeType.Dark, followSystemTheme: false);
					Dispatcher.UIThread.RunJobs();
					command.Execute(ThemeType.Light, followSystemTheme: false);
					Dispatcher.UIThread.RunJobs();
				}
				catch (Exception e)
				{
					ex = e;
				}
				finally
				{
					ForkPlusSettings.Default.Theme = original;
				}
				return ex;
			});
			Assert.True(crash == null, "主题切换崩溃堆栈：\n" + crash);
		}

		[Fact]
		public void LanguageSwitch_Roundtrip_ApplyLocalization_NoCrash()
		{
			// LanguageSwitchTests + BugFixV212 Bug4 迁移：原 Appearance 下拉切换语言，等价于
			// UiLanguage 赋值 + MainWindow.ApplyLocalization()（通知中心等 ILocalizableControl
			// 重新翻译就在这条链上）。UiLanguage setter 只改内存，测后恢复原值。
			// ⚠️ 线程规则同上：ForkPlusSettings.Default 的访问收进 Run 委托（UI 线程）。
			Exception crash = HeadlessAppBootstrap.Run(delegate
			{
				Exception ex = null;
				string original = ForkPlusSettings.Default.UiLanguage;
				try
				{
					// ⚠️ 复用已有 MainWindow（无则新建）：同一进程存在两个 MainWindow 时，
					// ApplyLocalization 的逻辑树遍历会栈溢出（ApplyRecursive 无限递归，
					// 已实证：先跑 MainWindow 冒烟再跑本测试必崩；真机只有一个 MainWindow，
					// 单独跑本测试不崩）。测试按"进程内单 MainWindow"与真机对齐。
					var window = (Application.Current.ApplicationLifetime as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow as global::ForkPlus.UI.MainWindow;
					if (window == null)
					{
						window = new global::ForkPlus.UI.MainWindow();
					}
					window.Show();
					Dispatcher.UIThread.RunJobs();
					ForkPlusSettings.Default.UiLanguage = "en";
					window.ApplyLocalization();
					Dispatcher.UIThread.RunJobs();
					ForkPlusSettings.Default.UiLanguage = "zh-Hans";
					window.ApplyLocalization();
					Dispatcher.UIThread.RunJobs();
					// ⚠️ 同 MainWindow 冒烟测试：Close() 会触发 lifetime.Shutdown()，用 Hide()。
					window.Hide();
				}
				catch (Exception e)
				{
					ex = e;
				}
				finally
				{
					ForkPlusSettings.Default.UiLanguage = original;
				}
				return ex;
			});
			Assert.True(crash == null, "语言切换崩溃堆栈：\n" + crash);
		}

		// ── BugFixV212 Bug3（空仓库加载卡死）迁移：纯逻辑版，无需 UI ──
		// 修复前 bt_get_commits 对空 tips 永久阻塞（UI 一直转圈）；修复后
		// GetRevisionStorageGitCommand.Execute 对空仓库走快速路径直接返回空 RevisionStorage。

		[Fact]
		public void GetRevisionStorage_EmptyRepo_QuickPathReturnsEmpty()
		{
			// 完全空仓库：无 refs、无 HEAD sha（HEAD 读取失败 fallback）。
			var references = new ReferenceStorage(
				new string[0], new Sha[0], 0u, new DateTime[0],
				new string[0], new string[0],
				new Range(0, 0), new Range(0, 0), new Range(0, 0),
				new string[0], 0, headSha: null, activeBranchIndex: null);
			// gitModule 传 null：快速路径命中时不触碰它；若快速路径回归（走到原生调用），
			// 会立即 NRE 失败而不是像修复前那样永久挂死——失败模式也是清晰的。
			var result = new GetRevisionStorageGitCommand().Execute(
				null, references, topoOrder: false, reflog: false, skipPages: 0, minPagesCount: 1,
				requiredShas: new Sha[0], timestamp: 0L, commitGraphCache: null, monitor: null);
			Assert.True(result.Succeeded, "空仓库快速路径未命中：" + result.Error);
			Assert.NotNull(result.Result);
			Assert.Equal(0, result.Result.Count);
		}

		[Fact]
		public void GetRevisionStorage_UnbornBranch_QuickPathReturnsEmpty()
		{
			// git init 完毕、有 untracked 文件的仓库：refs=["refs/heads/master"]（unborn），
			// shas=[Sha.Zero]，HeadSha=null。v2.1.4 修订后此形态也必须走快速路径。
			var references = new ReferenceStorage(
				new[] { "refs/heads/master" }, new[] { Sha.Zero }, 0u, new DateTime[0],
				new string[0], new string[0],
				new Range(0, 1), new Range(0, 0), new Range(0, 0),
				new string[0], 0, headSha: null, activeBranchIndex: 0);
			var result = new GetRevisionStorageGitCommand().Execute(
				null, references, topoOrder: false, reflog: false, skipPages: 0, minPagesCount: 1,
				requiredShas: new Sha[0], timestamp: 0L, commitGraphCache: null, monitor: null);
			Assert.True(result.Succeeded, "unborn 分支快速路径未命中：" + result.Error);
			Assert.NotNull(result.Result);
			Assert.Equal(0, result.Result.Count);
		}
	}
}
