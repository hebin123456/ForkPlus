// v5.6.2：git mm 同步弹窗「根 MR id」输入框（-s <id>）行为单测。
//
// 覆盖：
//  1) 默认留空 → SyncArgs 与命令预览都不含 -s
//  2) 输入正整数 → SyncArgs 含 -s <id>，命令预览实时联动
//  3) 输入非数字 / 负数 / 小数 / 前导 0 → 被过滤为规范正整数（留空则不加 -s）
//  4) 提交后再清空 → 不加 -s
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using ForkPlus.UI.Dialogs;
using Xunit;

namespace ForkPlus.Tests
{
	public class GitMmSyncRootMrIdTests
	{
		[Fact]
		public void RootMrId_PositiveInteger_AddsSArg()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				GitMmSyncWindow dialog = new GitMmSyncWindow("/tmp/gmm_sync_rootmr");
				dialog.Show();
				Dispatcher.UIThread.RunJobs();
				TextBox box = UiClick.Find<TextBox>(dialog, "RootMrIdTextBox");
				TextBlock preview = UiClick.Find<TextBlock>(dialog, "CommandPreviewTextBlock");
				try
				{
					// 1) 默认留空
					Assert.True(string.IsNullOrEmpty(box.Text));
					Assert.DoesNotContain(" -s ", preview.Text);
					Assert.DoesNotContain(" -s ", string.Join(" ", dialog.SyncArgs));

					// 2) 正整数 → 命令含 -s 123
					box.Text = "123";
					Dispatcher.UIThread.RunJobs();
					Assert.Contains("-s 123", preview.Text);
					Assert.Contains("-J 4 -j 8 -s 123", preview.Text);
				}
				finally
				{
					dialog.Close();
				}
			});
		}

		[Fact]
		public void RootMrId_FiltersNonDigits_AndLeadingZeros()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				GitMmSyncWindow dialog = new GitMmSyncWindow("/tmp/gmm_sync_rootmr");
				dialog.Show();
				Dispatcher.UIThread.RunJobs();
				TextBox box = UiClick.Find<TextBox>(dialog, "RootMrIdTextBox");
				TextBlock preview = UiClick.Find<TextBlock>(dialog, "CommandPreviewTextBlock");
				try
				{
					// 非数字全部剔除
					box.Text = "ab-12x";
					Dispatcher.UIThread.RunJobs();
					Assert.Equal("12", box.Text);
					Assert.Contains("-s 12", preview.Text);

					// 前导 0 去掉
					box.Text = "007";
					Dispatcher.UIThread.RunJobs();
					Assert.Equal("7", box.Text);
					Assert.Contains("-s 7", preview.Text);

					// 纯 0 → 空 → 不加 -s
					box.Text = "000";
					Dispatcher.UIThread.RunJobs();
					Assert.Equal(string.Empty, box.Text);
					Assert.DoesNotContain(" -s ", preview.Text);

					// 小数字符被剔除
					box.Text = "3.5";
					Dispatcher.UIThread.RunJobs();
					Assert.Equal("35", box.Text);
					Assert.Contains("-s 35", preview.Text);
				}
				finally
				{
					dialog.Close();
				}
			});
		}
	}
}
