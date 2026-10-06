// 集成测试（新手引导）：OnboardingManager 行为契约——首次启动未完成引导
//（OnboardingCompleted=false）弹向导并置位保存（看过或跳过均算完成，置位在弹窗
// 前——弹窗期间崩溃/断电不会陷入每次启动重弹）；已完成时幂等直接返回；弹窗异常
// 整体吞掉且不影响置位（启动路径装饰性功能不影响主流程）。注入点：
// ShowWindowForTests（用例结束必须复位，静态钩子泄漏会毒化同进程后续用例）。
// 设置自恢复：还原 OnboardingCompleted 单例值与 settings.json 磁盘内容（对齐
// ReleaseNotesManagerTests 的收尾口径）。
// 另含 OnboardingTourWindow UI 冒烟：13 步翻页（Back/Next/Finish 随步骤切换）、
// 步骤指示器与按钮文案的 zh-Hans 本地化、Skip 关窗；以及 headless 看门狗兜底
// 关闭回归（CI 全新 runner 无 settings.json，首开 MainWindow 的用例必触发首启
// 弹窗，模态无人关闭死锁整分片——防线与 ReleaseNotesWindow 同款）。
using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.Settings;
using ForkPlus.UI;
using ForkPlus.UI.Dialogs;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class OnboardingManagerTests
	{
		[Fact]
		public void FirstLaunch_ShowsTourAndMarksCompletedPersistently()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				bool original = ForkPlusSettings.Default.OnboardingCompleted;
				string path = Path.Combine(App.ForkDirectoryPath, "settings.json");
				string originalContent = File.Exists(path) ? File.ReadAllText(path) : null;
				int showCount = 0;
				try
				{
					ForkPlusSettings.Default.OnboardingCompleted = false;
					OnboardingManager.ShowWindowForTests = delegate { showCount++; };
					OnboardingManager.ShowIfFirstLaunch();
					Assert.Equal(1, showCount);
					Assert.True(ForkPlusSettings.Default.OnboardingCompleted, "看过或跳过均算完成");
					// 持久化验证：settings.json 已带 OnboardingCompleted=true（下次启动不再弹）
					Assert.True(File.Exists(path));
					Assert.True(JObject.Parse(File.ReadAllText(path))["OnboardingCompleted"].Value<bool>());
					// 幂等复核：已完成时第二次调用直接返回
					OnboardingManager.ShowIfFirstLaunch();
					Assert.Equal(1, showCount);
				}
				finally
				{
					OnboardingManager.ShowWindowForTests = null;
					ForkPlusSettings.Default.OnboardingCompleted = original;
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
		public void AlreadyCompleted_NoPopup()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				bool original = ForkPlusSettings.Default.OnboardingCompleted;
				int showCount = 0;
				try
				{
					ForkPlusSettings.Default.OnboardingCompleted = true;
					OnboardingManager.ShowWindowForTests = delegate { showCount++; };
					OnboardingManager.ShowIfFirstLaunch();
					Assert.Equal(0, showCount);
				}
				finally
				{
					OnboardingManager.ShowWindowForTests = null;
					ForkPlusSettings.Default.OnboardingCompleted = original;
				}
			});
		}

		[Fact]
		public void ShowThrows_IsSwallowedFlagStaysSet()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				bool original = ForkPlusSettings.Default.OnboardingCompleted;
				try
				{
					ForkPlusSettings.Default.OnboardingCompleted = false;
					OnboardingManager.ShowWindowForTests = delegate
					{
						throw new InvalidOperationException("ui boom");
					};
					OnboardingManager.ShowIfFirstLaunch(); // 不应抛（装饰性功能不影响主流程）
					// 置位在弹窗前：弹窗异常不影响已完成判定（下次启动不重弹）
					Assert.True(ForkPlusSettings.Default.OnboardingCompleted);
				}
				finally
				{
					OnboardingManager.ShowWindowForTests = null;
					ForkPlusSettings.Default.OnboardingCompleted = original;
				}
			});
		}

		[Fact]
		public void OnboardingTourWindow_StepNavigationAndLocalizedButtons()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				string originalLanguage = ForkPlusSettings.Default.UiLanguage;
				try
				{
					ForkPlusSettings.Default.UiLanguage = "zh-Hans";
					var window = new OnboardingTourWindow();
					window.Show();
					Dispatcher.UIThread.RunJobs();

				// 初始第 1 步：指示器本地化、Back 隐藏、Next 文案本地化
				Assert.Equal(0, window.CurrentStep);
				Assert.Equal("第 1 步，共 13 步", window.StepIndicatorText);
				Assert.False(window.BackButton.IsVisible, "第一步不应显示 Back");
				Assert.Equal("下一步", window.NextButton.Content as string);

				// Next → 第 2 步：Back 出现、指示器前进
				window.NextButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
				Dispatcher.UIThread.RunJobs();
				Assert.Equal(1, window.CurrentStep);
				Assert.True(window.BackButton.IsVisible);
				Assert.Equal("第 2 步，共 13 步", window.StepIndicatorText);

				// Back → 回到第 1 步
				window.BackButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
				Dispatcher.UIThread.RunJobs();
				Assert.Equal(0, window.CurrentStep);
				Assert.False(window.BackButton.IsVisible);

				// 连点 Next 走到最后一步：末步 Next 变 Finish（zh-Hans = "完成"，既有键）
				for (int i = 0; i < 12; i++)
				{
					window.NextButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
					Dispatcher.UIThread.RunJobs();
				}
				Assert.Equal(12, window.CurrentStep);
				Assert.Equal("完成", window.NextButton.Content as string);
				Assert.Equal("第 13 步，共 13 步", window.StepIndicatorText);

					// 末步再点 Next → CloseWithOk 关窗
					window.NextButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
					Dispatcher.UIThread.RunJobs();
					Assert.False(window.IsVisible);
				}
				finally
				{
					ForkPlusSettings.Default.UiLanguage = originalLanguage;
				}
			});
		}

		[Fact]
		public void OnboardingTourWindow_SkipButtonClosesImmediately()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var window = new OnboardingTourWindow();
				window.Show();
				Dispatcher.UIThread.RunJobs();
				Assert.True(window.IsVisible);
				window.SkipButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
				Dispatcher.UIThread.RunJobs();
				Assert.False(window.IsVisible);
				Assert.True(window.CurrentStep == 0, "Skip 不推进步骤，直接关窗");
			});
		}

		[Fact]
		public void VisibleOnboardingTourWindow_IsAutoClosedByWatchdog_AfterGracePeriod()
		{
			// 回归防线（对齐 ReleaseNotesWindow 看门狗用例）：CI 全新 runner 无
			// settings.json，首开 MainWindow 的用例必触发首启引导弹窗，模态
			// ShowDialog 在 headless 下无人点 Close → PushFrame 永不退出 → UI
			// 线程死锁。看门狗 3s 宽限 + Run 收尾 immediate 兜底关闭。断言：
			// ①宽限期内不误关；②func 返回后收尾关闭；③步骤指示器文本被捕获可断言。
			// 窗口在 UI 线程创建/读取（Avalonia 线程归属铁律），故终态经第二个 Run 复核。
			var holder = new OnboardingTourWindow[1];
			HeadlessAppBootstrap.Run(delegate
			{
				HeadlessAppBootstrap.ClearCapturedOnboardingTours();
				var window = new OnboardingTourWindow();
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
			Assert.True(closed, "Run 收尾应兜底关闭泄漏的 OnboardingTourWindow（模态场景 UI 线程即由此解锁）");
			Assert.Contains(HeadlessAppBootstrap.PeekCapturedOnboardingTours(),
				delegate (string t) { return t != null && t.Contains("1"); });
		}
	}
}
