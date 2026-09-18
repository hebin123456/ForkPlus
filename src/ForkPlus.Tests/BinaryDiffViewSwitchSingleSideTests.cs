// 回归测试（2026-09-18，"新增文件从十六进制切回并排模式左侧出现空文件"）：
// BinaryDiffUserControl.ImageDiffSelectedItem_Changed 切回 Side-by-Side 时此前无条件
// Show 左右两个内容控件——新增文件（仅 dst 内容，UpdateContent 已 Collapse 左侧）/
// 删除文件（仅 src 内容）切 Hex 再切回后，无内容一侧的空面板被重新显示。
// 修复后按 _srcBinaryContent/_dstBinaryContent 有无恢复可见性。
// 直接驱动 UpdateDiff（生产路径：FileDiffControl → UpdateDiff(repoControl,
// UnknownBinaryDiffContent, true, HexDiffContent)），不依赖真实 git 仓库。
using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.BinaryDiff;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class BinaryDiffViewSwitchSingleSideTests
	{
		/// <summary>装配 BinaryDiffUserControl 并走生产 UpdateDiff 路径（反射注入 GitModule，
		/// 与 BinaryDiffEndToEndTests 同款；单边内容不会触发 LFS 分支，GitModule 仅作占位）。</summary>
		private static BinaryDiffUserControl ShowBinaryDiffInWindow(ChangedFile changedFile,
			UnknownBinaryDiffContent unknownContent, HexDiffContent hexContent, out Window outWindow)
		{
			var repoControl = new RepositoryUserControl();
			typeof(RepositoryUserControl).GetProperty("GitModule")!
				.SetValue(repoControl, new GitModule(Directory.GetCurrentDirectory(),
					Directory.GetCurrentDirectory(), null, null));
			var binaryDiff = new BinaryDiffUserControl();
			var window = new Window { Width = 900, Height = 500, Content = binaryDiff };
			window.Show();
			Dispatcher.UIThread.RunJobs();
			binaryDiff.UpdateDiff(repoControl, unknownContent, true, hexContent);
			Dispatcher.UIThread.RunJobs();
			outWindow = window;
			return binaryDiff;
		}

		private static MemoryStream NewBytes(int count)
		{
			byte[] data = new byte[count];
			new Random(42).NextBytes(data);
			return new MemoryStream(data);
		}

		[Fact]
		public void AddedFile_HexThenSideBySide_LeftEmptyPanelStaysCollapsed()
		{
			HeadlessAppBootstrap.EnsureStarted();
			bool[] holder = new bool[6];
			Window window = null;
			try
			{
				Dispatcher.UIThread.InvokeAsync(delegate
				{
					// 新增二进制文件：src 无内容（无大小、无字节），dst 64 字节
					var changedFile = new ChangedFile("new.bin", StatusType.Added, StatusType.None,
						ChangeType.Added, staged: true, isNew: true, tracked: true);
					var unknown = new UnknownBinaryDiffContent(changedFile, null, 64);
					var hex = new HexDiffContent(changedFile, null, NewBytes(64));
					BinaryDiffUserControl binaryDiff = ShowBinaryDiffInWindow(changedFile, unknown, hex, out window);

					// ===== 1) 初始并排卡片视图：左侧（old）应保持 Collapse，右侧（new）显示 =====
					holder[0] = binaryDiff.SideBySideRadioButton.IsChecked.GetValueOrDefault();
					holder[1] = binaryDiff.SrcFileContentUserControl.IsVisible;
					holder[2] = binaryDiff.DstFileContentUserControl.IsVisible;

					// ===== 2) 切到 Hex 视图 =====
					binaryDiff.HexRadioButton.IsChecked = true;
					Dispatcher.UIThread.RunJobs();
					holder[3] = binaryDiff.HexDiffViewContainer.IsVisible;

					// ===== 3) 切回并排：无内容的左侧不应被重新 Show（bug 修复点） =====
					binaryDiff.SideBySideRadioButton.IsChecked = true;
					Dispatcher.UIThread.RunJobs();
					holder[4] = binaryDiff.SrcFileContentUserControl.IsVisible;
					holder[5] = binaryDiff.DstFileContentUserControl.IsVisible;
					return 0;
				}).GetAwaiter().GetResult();
			}
			finally
			{
				if (window != null)
				{
					Dispatcher.UIThread.InvokeAsync(window.Close).GetAwaiter().GetResult();
				}
			}

			Assert.True(holder[0], "初始应为 Side-by-Side 视图（UpdateContent 重置）");
			Assert.False(holder[1], "新增文件初始并排视图左侧（old）应隐藏（UpdateContent Collapse）");
			Assert.True(holder[2], "新增文件初始并排视图右侧（new）应显示");
			Assert.True(holder[3], "Hex 单选后 HexDiffViewContainer 应显示");
			Assert.False(holder[4],
				"从 Hex 切回并排后新增文件的左侧空面板应保持隐藏（修复前被无条件 Show 露出空文件）");
			Assert.True(holder[5], "从 Hex 切回并排后右侧（new）应恢复显示");
		}

		[Fact]
		public void RemovedFile_HexThenSideBySide_RightEmptyPanelStaysCollapsed()
		{
			HeadlessAppBootstrap.EnsureStarted();
			bool[] holder = new bool[6];
			Window window = null;
			try
			{
				Dispatcher.UIThread.InvokeAsync(delegate
				{
					// 删除二进制文件：src 64 字节，dst 无内容
					var changedFile = new ChangedFile("gone.bin", StatusType.Deleted, StatusType.None,
						ChangeType.Deleted, staged: true, isNew: false, tracked: true);
					var unknown = new UnknownBinaryDiffContent(changedFile, 64, null);
					var hex = new HexDiffContent(changedFile, NewBytes(64), null);
					BinaryDiffUserControl binaryDiff = ShowBinaryDiffInWindow(changedFile, unknown, hex, out window);

					holder[0] = binaryDiff.SideBySideRadioButton.IsChecked.GetValueOrDefault();
					holder[1] = binaryDiff.SrcFileContentUserControl.IsVisible;
					holder[2] = binaryDiff.DstFileContentUserControl.IsVisible;

					binaryDiff.HexRadioButton.IsChecked = true;
					Dispatcher.UIThread.RunJobs();
					holder[3] = binaryDiff.HexDiffViewContainer.IsVisible;

					binaryDiff.SideBySideRadioButton.IsChecked = true;
					Dispatcher.UIThread.RunJobs();
					holder[4] = binaryDiff.SrcFileContentUserControl.IsVisible;
					holder[5] = binaryDiff.DstFileContentUserControl.IsVisible;
					return 0;
				}).GetAwaiter().GetResult();
			}
			finally
			{
				if (window != null)
				{
					Dispatcher.UIThread.InvokeAsync(window.Close).GetAwaiter().GetResult();
				}
			}

			Assert.True(holder[0], "初始应为 Side-by-Side 视图（UpdateContent 重置）");
			Assert.True(holder[1], "删除文件初始并排视图左侧（old）应显示");
			Assert.False(holder[2], "删除文件初始并排视图右侧（new）应隐藏（UpdateContent Collapse）");
			Assert.True(holder[3], "Hex 单选后 HexDiffViewContainer 应显示");
			Assert.True(holder[4], "从 Hex 切回并排后左侧（old）应恢复显示");
			Assert.False(holder[5],
				"从 Hex 切回并排后删除文件的右侧空面板应保持隐藏（修复前被无条件 Show 露出空文件）");
		}
	}
}