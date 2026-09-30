// 用户手册 v4.3.0 新特性插图（2026-09-30）：为新手引导向导补手册截图（ch-01 欢迎页）。
//   1) 第 1 步「欢迎使用 ForkPlus」→ 01-welcome/08-onboarding-tour-welcome.png。
//   2) 第 11 步「随心定制」（StepPanel10：主题色板 / 界面语言 / 标签条位置）
//      → 01-welcome/09-onboarding-tour-customize.png。
// 直构 OnboardingTourWindow（生产入口 OnboardingManager 自动弹窗 + 帮助菜单
// ShowOnboardingTourCommand 都只是 ShowDialog 包装）；翻页复用窗口自身按钮的 Click 事件，
// 与 OnboardingManagerTests 同一套做法。
// 注意：本向导若保持可见，HeadlessAppBootstrap 的看门狗会在宽限期后兜底关闭；这里两步
// 各自显式 Close()（并在 Run 收尾前完成），不触碰看门狗路径。
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class ManualV430ChaptersTests
	{
		private const string ModuleDir = "01-welcome";

		[Fact]
		public void Ch01_OnboardingTourWelcomeStep()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string originalLanguage = ForkPlusSettings.Default.UiLanguage;
			try
			{
				ForkPlusSettings.Default.UiLanguage = "zh-Hans";
				HeadlessAppBootstrap.Run(delegate
				{
					var window = new OnboardingTourWindow();
					window.Show();
					Dispatcher.UIThread.RunJobs();
					Assert.Equal(0, window.CurrentStep);
					ManualScreenshotHelper.Snap(window, "08-onboarding-tour-welcome", ModuleDir);
					window.Close();
					Dispatcher.UIThread.RunJobs();
				});
			}
			finally
			{
				ForkPlusSettings.Default.UiLanguage = originalLanguage;
			}
		}

		[Fact]
		public void Ch01_OnboardingTourCustomizeStep()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string originalLanguage = ForkPlusSettings.Default.UiLanguage;
			try
			{
				ForkPlusSettings.Default.UiLanguage = "zh-Hans";
				HeadlessAppBootstrap.Run(delegate
				{
					var window = new OnboardingTourWindow();
					window.Show();
					Dispatcher.UIThread.RunJobs();
					// 连点 Next 10 次 → 第 11 步「随心定制」（StepPanel10）
					for (int i = 0; i < 10; i++)
					{
						window.NextButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
						Dispatcher.UIThread.RunJobs();
					}
					Assert.Equal(10, window.CurrentStep);
					ManualScreenshotHelper.Snap(window, "09-onboarding-tour-customize", ModuleDir);
					window.Close();
					Dispatcher.UIThread.RunJobs();
				});
			}
			finally
			{
				ForkPlusSettings.Default.UiLanguage = originalLanguage;
			}
		}
	}
}