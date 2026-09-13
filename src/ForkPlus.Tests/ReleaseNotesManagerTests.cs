// 集成测试（v4.1.0，首次启动"更新内容"弹窗管理器 ReleaseNotesManager）：
// 行为契约——首次启动新版本（LastShownReleaseNotesVersion != 当前版本）取章节内容
// 弹窗并记录版本；同版本二次调用幂等（数据源都不读、不弹）；章节缺失不弹但同样
// 记录（下次启动不重试，避免每次启动反复 IO）；数据源异常整体吞掉且不记录版本
//（下次启动可重试）——启动路径上的装饰性功能不允许影响主流程。
// 注入点：NotesProviderForTests / ShowWindowForTests（用例结束必须复位，静态钩子
// 泄漏会毒化同进程后续用例）。设置自恢复：还原 LastShownReleaseNotesVersion 单例
// 值与 settings.json 磁盘内容（对齐 SettingsPersistenceRoundTripTests 的收尾口径）。
// 另含 ReleaseNotesWindow UI 冒烟：标题走 "What's New in {0}" 本地化、正文入只读
// 文本框、Footer 单 Close 按钮形态与 "Release Notes" 小节标题——风格复用
// ForkPlusDialogWindow 组件框架（与 UpdateAvailableWindow / UpdateCheckWindow 一致）。
using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Settings;
using ForkPlus.UI;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class ReleaseNotesManagerTests
	{
		private sealed class ShowCapture
		{
			public string Version;

			public string Notes;

			public int Count;
		}

		private static string CurrentVersion()
		{
			return UpdateChecker.NormalizeVersion(App.Version);
		}

		[Fact]
		public void FirstLaunchOfNewVersion_ShowsWindowAndRecordsVersion()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				string original = ForkPlusSettings.Default.LastShownReleaseNotesVersion;
				string path = Path.Combine(App.ForkDirectoryPath, "settings.json");
				string originalContent = File.Exists(path) ? File.ReadAllText(path) : null;
				var capture = new ShowCapture();
				try
				{
					ForkPlusSettings.Default.LastShownReleaseNotesVersion = "4.0.12"; // 旧版本 → 首次启动新版本
					ReleaseNotesManager.NotesProviderForTests = delegate (string v) { return "v4.1.0 notes"; };
					ReleaseNotesManager.ShowWindowForTests = delegate (string v, string n)
					{
						capture.Version = v;
						capture.Notes = n;
						capture.Count++;
					};
					ReleaseNotesManager.ShowIfFirstLaunchOfNewVersion();
					Assert.Equal(1, capture.Count);
					Assert.Equal(CurrentVersion(), capture.Version);
					Assert.Equal("v4.1.0 notes", capture.Notes);
					Assert.Equal(CurrentVersion(), ForkPlusSettings.Default.LastShownReleaseNotesVersion);
				}
				finally
				{
					ReleaseNotesManager.NotesProviderForTests = null;
					ReleaseNotesManager.ShowWindowForTests = null;
					ForkPlusSettings.Default.LastShownReleaseNotesVersion = original;
					ForkPlusSettings.Default.Save();
					if (originalContent != null)
					{
						File.WriteAllText(path, originalContent);
					}
					else if (File.Exists(path))
					{
						File.Delete(path);
					}
				}
			});
		}

		[Fact]
		public void SameVersionSecondLaunch_IsIdempotent_NoReadNoPopup()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				string original = ForkPlusSettings.Default.LastShownReleaseNotesVersion;
				var capture = new ShowCapture();
				int providerCalls = 0;
				try
				{
					ForkPlusSettings.Default.LastShownReleaseNotesVersion = CurrentVersion();
					ReleaseNotesManager.NotesProviderForTests = delegate (string v)
					{
						providerCalls++;
						return "notes";
					};
					ReleaseNotesManager.ShowWindowForTests = delegate (string v, string n) { capture.Count++; };
					ReleaseNotesManager.ShowIfFirstLaunchOfNewVersion();
					Assert.Equal(0, providerCalls); // 版本已记录：连数据源都不应读取
					Assert.Equal(0, capture.Count);
				}
				finally
				{
					ReleaseNotesManager.NotesProviderForTests = null;
					ReleaseNotesManager.ShowWindowForTests = null;
					ForkPlusSettings.Default.LastShownReleaseNotesVersion = original;
				}
			});
		}

		[Fact]
		public void MissingNotes_NoPopupButVersionRecorded_NoRetryNextLaunch()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				string original = ForkPlusSettings.Default.LastShownReleaseNotesVersion;
				string path = Path.Combine(App.ForkDirectoryPath, "settings.json");
				string originalContent = File.Exists(path) ? File.ReadAllText(path) : null;
				var capture = new ShowCapture();
				try
				{
					ForkPlusSettings.Default.LastShownReleaseNotesVersion = "4.0.12";
					ReleaseNotesManager.NotesProviderForTests = delegate (string v) { return null; }; // 章节缺失
					ReleaseNotesManager.ShowWindowForTests = delegate (string v, string n) { capture.Count++; };
					ReleaseNotesManager.ShowIfFirstLaunchOfNewVersion();
					Assert.Equal(0, capture.Count);
					// 缺失同样记录版本：下次启动不重试（反复 IO 无用户价值）
					Assert.Equal(CurrentVersion(), ForkPlusSettings.Default.LastShownReleaseNotesVersion);
					// 幂等复核：同版本第二次调用直接返回
					ReleaseNotesManager.ShowIfFirstLaunchOfNewVersion();
					Assert.Equal(0, capture.Count);
				}
				finally
				{
					ReleaseNotesManager.NotesProviderForTests = null;
					ReleaseNotesManager.ShowWindowForTests = null;
					ForkPlusSettings.Default.LastShownReleaseNotesVersion = original;
					ForkPlusSettings.Default.Save();
					if (originalContent != null)
					{
						File.WriteAllText(path, originalContent);
					}
					else if (File.Exists(path))
					{
						File.Delete(path);
					}
				}
			});
		}

		[Fact]
		public void ProviderThrows_IsSwallowedAndVersionNotRecorded()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				string original = ForkPlusSettings.Default.LastShownReleaseNotesVersion;
				var capture = new ShowCapture();
				try
				{
					ForkPlusSettings.Default.LastShownReleaseNotesVersion = "4.0.12";
					ReleaseNotesManager.NotesProviderForTests = delegate (string v)
					{
						throw new InvalidOperationException("disk boom");
					};
					ReleaseNotesManager.ShowWindowForTests = delegate (string v, string n) { capture.Count++; };
					ReleaseNotesManager.ShowIfFirstLaunchOfNewVersion(); // 不应抛（装饰性功能不影响主流程）
					Assert.Equal(0, capture.Count);
					// 异常路径不记录版本：下次启动可重试
					Assert.Equal("4.0.12", ForkPlusSettings.Default.LastShownReleaseNotesVersion);
				}
				finally
				{
					ReleaseNotesManager.NotesProviderForTests = null;
					ReleaseNotesManager.ShowWindowForTests = null;
					ForkPlusSettings.Default.LastShownReleaseNotesVersion = original;
				}
			});
		}

		[Fact]
		public void ReleaseNotesWindow_LocalizedTitleNotesAndSingleCloseButtonFooter()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				string originalLanguage = ForkPlusSettings.Default.UiLanguage;
				try
				{
					ForkPlusSettings.Default.UiLanguage = "zh-Hans";
					var window = new ReleaseNotesWindow("4.1.0", "更新内容第一行\n更新内容第二行");
					window.Show();
					Dispatcher.UIThread.RunJobs();

					// 标题走 "What's New in {0}" 本地化（zh-Hans = "{0} 更新内容"），
					// 经 base.Title 同步（DialogTitle setter 同时写 Window.Title）
					Assert.Equal("4.1.0 更新内容", window.Title);
					Assert.Equal("更新内容第一行\n更新内容第二行", window.ReleaseNotesTextBox.Text);

					// 与 UpdateAvailableWindow / UpdateCheckWindow 同款 "Release Notes" 小节标题
					//（基类 Loaded 自动本地化，zh-Hans 下复用既有 "Release Notes" 翻译键 = "更新内容"）
					Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(),
						delegate (TextBlock tb) { return tb.Text == "更新内容"; });

					// Footer 单 Close 按钮形态：Submit = 关闭，Cancel 收起
				ForkPlusDialogFooter footer = window.GetVisualDescendants().OfType<ForkPlusDialogFooter>().FirstOrDefault();
				Assert.NotNull(footer);
				Assert.Equal("关闭", footer.SubmitButton.Content as string);
				Assert.False(footer.CancelButton.IsVisible, "只读信息弹窗不应显示 Cancel 按钮");

				window.Close();
				Dispatcher.UIThread.RunJobs();
			}
			finally
			{
				ForkPlusSettings.Default.UiLanguage = originalLanguage;
			}
			});
		}

		[Fact]
		public void VisibleReleaseNotesWindow_IsAutoClosedByWatchdog_AfterGracePeriod()
		{
			// v4.1.0（2026-09-13，CI 全分片挂死回归防线）：CI 全新 runner 无 settings.json，
			// 首个开 MainWindow 的用例必触发"首次启动新版本"弹 ReleaseNotesWindow；模态
			// ShowDialog 在 headless 下无人点 Close → PushFrame 永不退出 → UI 线程死锁、
			// 整分片挂死（e2e-1/2/3 + core-a/b 五片实证）。看门狗按信息窗同款语义兜底：
			// 模态泵期间 tick 3s 宽限后关闭（E2e1 全绿即证该路径）；Run 收尾同步扫描
			// immediate 关闭残留（本用例确定性验证的路径）。断言：①宽限期内不误关；
			// ②func 返回后收尾关闭；③正文捕获进 PeekCapturedReleaseNotes 可断言。
			// 窗口在 UI 线程创建/读取（Avalonia 线程归属铁律），故终态经第二个 Run 复核。
			var holder = new ReleaseNotesWindow[1];
			HeadlessAppBootstrap.Run(delegate
			{
				HeadlessAppBootstrap.ClearCapturedReleaseNotes();
				var window = new ReleaseNotesWindow("4.1.0", "看门狗回归：更新内容正文");
				holder[0] = window;
				window.Show();
				Dispatcher.UIThread.RunJobs();
				Assert.True(window.IsVisible, "宽限期内不应被关闭（首见只记时间戳）");
				// 故意不关窗口：func 返回后的 Run 收尾扫描（immediate）负责兜底关闭
			});
			bool closed = false;
			HeadlessAppBootstrap.Run(delegate
			{
				closed = !holder[0].IsVisible; // UI 线程复核终态（首个 Run 的收尾已执行）
			});
			Assert.True(closed, "Run 收尾应兜底关闭泄漏的 ReleaseNotesWindow（模态场景 UI 线程即由此解锁）");
			Assert.Contains(HeadlessAppBootstrap.PeekCapturedReleaseNotes(),
				delegate (string t) { return t != null && t.Contains("看门狗回归"); });
		}
	}
}
