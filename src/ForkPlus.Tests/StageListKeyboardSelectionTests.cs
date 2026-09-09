// Bug 复现（2026-09-09）：未暂存/已暂存区域用键盘上下键选择文件时，右侧 FileDiff
// 面板不刷新（鼠标点击正常）。根因：FileListUserControl.
// NotifySelectionChangedFromCurrentItems 优先取 TreeView.LastClickedItem 作为
// 事件携带的选中文件，而 LastClickedItem 只在鼠标点击（OnPointerPressed）时
// 更新；键盘导航（ListBox 基类上下键 / SelectNextFile / SelectPreviousFile 的
// Clear + SelectAndFocus）不触碰它，事件携带的仍是上次鼠标点击的文件——该文件
// 已不在当前选中集合中，CommitUserControl.UpdateDiff 末尾的"文件仍在选中集合"
// 守卫丢弃结果，diff 面板停留旧内容。修复：LastClickedItem 不在当前选中集合时
// 回退到选中集合的第一个文件。
// 用例模拟完整链路：鼠标点击 file0（设置 LastClickedItem）→ 键盘式选中迁移
// （Clear + SelectAndFocus，LastClickedItem 保持不变）→ 断言 SelectionChanged
// 事件携带新选中文件而非过时的 LastClickedItem。
using System;
using Avalonia.Controls;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class StageListKeyboardSelectionTests
	{
		private static void Pump()
		{
			Dispatcher.UIThread.RunJobs();
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
			Dispatcher.UIThread.RunJobs();
		}

		private static FileListItem FindItem(FileListUserControl list, string path)
		{
			return FindItem(list.TreeView.RootItem as FileListItem, path);
		}

		private static FileListItem FindItem(FileListItem parent, string path)
		{
			if (parent == null)
			{
				return null;
			}
			if (!parent.IsDirectory && parent.ChangedFile.Path == path)
			{
				return parent;
			}
			foreach (FileListItem child in parent.Children)
			{
				FileListItem found = FindItem(child, path);
				if (found != null)
				{
					return found;
				}
			}
			return null;
		}

		[Fact]
		public void FileList_KeyboardNavigation_NotifiesNewFile_NotStaleLastClicked()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window { Width = 400, Height = 300 };
				var list = new FileListUserControl();
				window.Content = list;
				window.Show();
				Dispatcher.UIThread.RunJobs();

				// 3 个未暂存文件（平坦结构，无目录折叠）
				ChangedFile[] files = new[]
				{
					new ChangedFile("file0.txt", StatusType.Modified, StatusType.None),
					new ChangedFile("file1.txt", StatusType.Modified, StatusType.None),
					new ChangedFile("file2.txt", StatusType.Modified, StatusType.None),
				};
				list.SetItemSource(files, forceRefresh: false, restoreSelection: false);
				Pump();

				FileListItem item0 = FindItem(list, "file0.txt");
				FileListItem item1 = FindItem(list, "file1.txt");
				Assert.NotNull(item0);
				Assert.NotNull(item1);

				ChangedFile lastNotified = null;
				list.SelectionChanged += delegate (object sender, FileListEventArgs e)
				{
					if (e.SelectedFile != null)
					{
						lastNotified = e.SelectedFile;
					}
				};

				// ===== 1) 模拟鼠标点击 file0：OnPointerPressed 的等价效果——
				// LastClickedItem 指向被点击节点 + 选中它 =====
				list.TreeView.LastClickedItem = item0;
				list.TreeView.SelectedItems.Clear();
				list.TreeView.SelectAndFocus(item0);
				Pump();
				Assert.Equal("file0.txt", lastNotified?.Path);

				// ===== 2) 模拟键盘下移到 file1：SelectNextFile / ListBox 上下键的
				// 共同路径——Clear + SelectAndFocus，LastClickedItem 保持 file0 =====
				list.TreeView.SelectedItems.Clear();
				list.TreeView.SelectAndFocus(item1);
				Pump();

				// 修复前：事件携带过时的 file0（LastClickedItem），diff 守卫丢弃 → 不刷新
				// 修复后：事件携带新选中的 file1
				Assert.Equal("file1.txt", lastNotified?.Path);
			});
		}

		[Fact]
		public void FileList_MouseClickAfterKeyboard_StillNotifiesClickedFile()
		{
			// 反向回归：修复不得破坏鼠标多选语义——LastClickedItem 在选中集合中时
			// 仍应作为事件携带的主选中文件（多选场景 diff 显示最后点击的文件）。
			HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window { Width = 400, Height = 300 };
				var list = new FileListUserControl();
				window.Content = list;
				window.Show();
				Dispatcher.UIThread.RunJobs();

				ChangedFile[] files = new[]
				{
					new ChangedFile("file0.txt", StatusType.Modified, StatusType.None),
					new ChangedFile("file1.txt", StatusType.Modified, StatusType.None),
					new ChangedFile("file2.txt", StatusType.Modified, StatusType.None),
				};
				list.SetItemSource(files, forceRefresh: false, restoreSelection: false);
				Pump();

				FileListItem item0 = FindItem(list, "file0.txt");
				FileListItem item2 = FindItem(list, "file2.txt");
				Assert.NotNull(item0);
				Assert.NotNull(item2);

				ChangedFile lastNotified = null;
				list.SelectionChanged += delegate (object sender, FileListEventArgs e)
				{
					if (e.SelectedFile != null)
					{
						lastNotified = e.SelectedFile;
					}
				};

				// Ctrl+点击多选：先选 file0（LastClickedItem），再点击 file2（更新
				// LastClickedItem 到 file2，多选保留 file0+file2）
				list.TreeView.LastClickedItem = item0;
				list.TreeView.SelectedItems.Add(item0);
				Pump();
				list.TreeView.LastClickedItem = item2;
				list.TreeView.SelectedItems.Add(item2);
				Pump();

				// 多选 + LastClickedItem 在选中集合中：事件应携带最后点击的 file2
				Assert.Equal("file2.txt", lastNotified?.Path);
			});
		}
	}
}
