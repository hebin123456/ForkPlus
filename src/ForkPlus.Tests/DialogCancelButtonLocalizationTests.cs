// 回归测试（2026-09-03，"弹窗取消按钮固定是 Cancel 没有国际化"修复产物）：
// 根因：Footer XAML 的 Cancel 按钮文本硬编码 "Cancel"（WPF 原版同样如此），未显式设置
// CancelButtonTitle 的弹窗（GoToLineWindow/CloneWindow/CreateBranchWindow 等）在任何语言
// 下都显示英文 "Cancel"。修复：ForkPlusDialogWindow.AddFooter 里，弹窗未显式设置
// CancelButtonTitle 时默认 Translate("Cancel")；已显式设置（Close/Later/Exit 等）不受影响。
// 本测试守卫：zh-Hans 下未显式设置的弹窗 Cancel 按钮显示"取消"，显式设置的保持各自文案。
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using System.Linq;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class DialogCancelButtonLocalizationTests
	{
		[Fact]
		public void UnsetCancelTitle_DefaultsToLocalizedCancel()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				string originalLanguage = ForkPlusSettings.Default.UiLanguage;
				try
				{
					ForkPlusSettings.Default.UiLanguage = PreferencesLocalizationConstantsForTests.ZhHans;
					// GoToLineWindow：只设置 SubmitButtonTitle("Go")，未设置 CancelButtonTitle。
					var dialog = new GoToLineWindow();
					dialog.Show();
					Dispatcher.UIThread.RunJobs();

					ForkPlusDialogFooter footer = dialog.GetVisualDescendants().OfType<ForkPlusDialogFooter>().FirstOrDefault();
					Assert.NotNull(footer);
					Assert.Equal("取消", footer.CancelButton.Content as string);
					dialog.Close();
					Dispatcher.UIThread.RunJobs();
					return 0;
				}
				finally
				{
					ForkPlusSettings.Default.UiLanguage = originalLanguage;
				}
			}).GetAwaiter().GetResult();
		}

		[Fact]
		public void ExplicitCancelTitle_NotOverriddenByDefault()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				string originalLanguage = ForkPlusSettings.Default.UiLanguage;
				try
				{
					ForkPlusSettings.Default.UiLanguage = PreferencesLocalizationConstantsForTests.ZhHans;
					// ErrorWindow 构造里显式 CancelButtonTitle = Translate("Close")。
					var dialog = new ErrorWindow("some error");
					dialog.Show();
					Dispatcher.UIThread.RunJobs();

					ForkPlusDialogFooter footer = dialog.GetVisualDescendants().OfType<ForkPlusDialogFooter>().FirstOrDefault();
					Assert.NotNull(footer);
					Assert.Equal("关闭", footer.CancelButton.Content as string);
					dialog.Close();
					Dispatcher.UIThread.RunJobs();
					return 0;
				}
				finally
				{
					ForkPlusSettings.Default.UiLanguage = originalLanguage;
				}
			}).GetAwaiter().GetResult();
		}
	}

	// PreferencesLocalization 是 internal，语言常量这里自持（与源码常量一致，测试断言亦互证）。
	internal static class PreferencesLocalizationConstantsForTests
	{
		public const string ZhHans = "zh-Hans";
	}
}
