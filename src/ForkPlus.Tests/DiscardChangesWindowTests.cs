// WS2.3（丢弃文件对话框化）测试：DiscardChangesWindow 分组展示。
// 直接构造 ChangedFile[]/Submodule[] 参数（对话框不依赖 git 仓库——它只做展示与确认，
// 丢弃逻辑仍在 DiscardChangedFilesCommand.DiscardFiles）：
// - 双分组（子仓/普通文件）各显示组头与条目；空组整组隐藏；
// - 单组超过上限（200）截断 + "…and {0} more" 溢出行；
// - 提交按钮标题由调用方传入（保留原 MessageBox 的 "Discard Changes in N Files" 语义）。
using System;
using System.Linq;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class DiscardChangesWindowTests
	{
		private static ChangedFile File(string path)
		{
			return new ChangedFile(path, StatusType.Modified, StatusType.Modified);
		}

		[Fact]
		public void ShowsSubmodulesAndFilesGroups()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				ChangedFile[] files = new ChangedFile[] { File("a.txt"), File("src/app.cs") };
				Submodule[] submodules = new Submodule[] { new Submodule("sub", isActive: true) };
				DiscardChangesWindow dialog = new DiscardChangesWindow(files, submodules, "Discard Changes in 3 Files");
				dialog.Show();
				Dispatcher.UIThread.RunJobs();
				try
				{
					// 子仓组：组头 + 1 条
					Assert.True(dialog.SubmodulesHeaderTextBlock.IsVisible, "子仓组头应可见");
					Assert.Equal(E2eMainWindowHarness.Tr("Submodules"), dialog.SubmodulesHeaderTextBlock.Text);
					Assert.Equal(1, dialog.SubmodulesItemsControl.ItemCount);
					Assert.Equal("sub", (string)dialog.SubmodulesItemsControl.ItemsSource.OfType<object>().First());
					// 文件组：组头 + 2 条（保持传入顺序）
					Assert.True(dialog.FilesHeaderTextBlock.IsVisible, "文件组头应可见");
					Assert.Equal(E2eMainWindowHarness.Tr("Files"), dialog.FilesHeaderTextBlock.Text);
					Assert.Equal(2, dialog.FilesItemsControl.ItemCount);
					Assert.Equal("a.txt", (string)dialog.FilesItemsControl.ItemsSource.OfType<object>().First());
					// 无溢出行
					Assert.False(dialog.SubmodulesOverflowTextBlock.IsVisible);
					Assert.False(dialog.FilesOverflowTextBlock.IsVisible);
					// 提交按钮标题透传（原 MessageBox 语义：按去重路径数计数的文案由命令侧计算）
					ForkPlusDialogFooter footer = dialog.GetVisualDescendants().OfType<ForkPlusDialogFooter>().FirstOrDefault();
					Assert.NotNull(footer);
					Assert.Equal(E2eMainWindowHarness.Tr("Discard Changes in 3 Files"), footer.SubmitButton.Content);
				}
				finally
				{
					dialog.Close();
					Dispatcher.UIThread.RunJobs();
				}
			});
		}

		[Fact]
		public void HidesEmptySubmodulesGroup()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				DiscardChangesWindow dialog = new DiscardChangesWindow(
					new ChangedFile[] { File("a.txt") }, Array.Empty<Submodule>(), "Discard");
				dialog.Show();
				Dispatcher.UIThread.RunJobs();
				try
				{
					Assert.False(dialog.SubmodulesHeaderTextBlock.IsVisible, "无子仓时子仓组应隐藏");
					Assert.False(dialog.SubmodulesItemsControl.IsVisible);
					Assert.True(dialog.FilesHeaderTextBlock.IsVisible);
					Assert.Equal(1, dialog.FilesItemsControl.ItemCount);
				}
				finally
				{
					dialog.Close();
					Dispatcher.UIThread.RunJobs();
				}
			});
		}

		[Fact]
		public void TruncatesLargeFileListWithOverflowLine()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				ChangedFile[] files = Enumerable.Range(0, DiscardChangesWindow.MaxDisplayedPaths + 5)
					.Select((int i) => File("file" + i.ToString("000") + ".txt"))
					.ToArray();
				DiscardChangesWindow dialog = new DiscardChangesWindow(files, Array.Empty<Submodule>(), "Discard");
				dialog.Show();
				Dispatcher.UIThread.RunJobs();
				try
				{
					// 上限截断：恰好显示 MaxDisplayedPaths 条
					Assert.Equal(DiscardChangesWindow.MaxDisplayedPaths, dialog.FilesItemsControl.ItemCount);
					Assert.Equal("file000.txt", (string)dialog.FilesItemsControl.ItemsSource.OfType<object>().First());
					// 溢出行："…and {0} more"（5 = 205 - 200）
					Assert.True(dialog.FilesOverflowTextBlock.IsVisible, "超过上限应显示溢出行");
					Assert.Equal(E2eMainWindowHarness.TrFormat("...and {0} more", 5), dialog.FilesOverflowTextBlock.Text);
				}
				finally
				{
					dialog.Close();
					Dispatcher.UIThread.RunJobs();
				}
			});
		}
	}
}
