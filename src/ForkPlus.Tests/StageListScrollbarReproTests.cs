// Bug3 复现（2026-09-09）：未暂存/已暂存区域把文件暂存或取消暂存后，
// 区域 ScrollViewer 的滚动条（extent/thumb）没有刷新——滚动条位置与样式
// 和左侧文件树对不上。用例直接驱动生产真实入口 StageFileUserControl.SetDataAsync
// / FileListUserControl.SetItemSource（forceRefresh=false：diff<256 走增量
// ApplyAddedEntries/ApplyRemovedEntries + LockUpdates 单次 Reset；diff>256 走
// 后台重建 + RootItem 替换），观察 ScrollViewer.Extent/ScrollBarMaximum/Offset
// 与 Thumb 实际长度和条目数是否一致。
using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.UI;
using ForkPlus.UI.UserControls;
using Xunit;
using Xunit.Abstractions;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class StageListScrollbarReproTests
	{
		private readonly ITestOutputHelper _output;

		public StageListScrollbarReproTests(ITestOutputHelper output)
		{
			_output = output;
		}

		private static ChangedFile[] MakeUnstaged(int count, string prefix = "file")
		{
			return Enumerable.Range(0, count)
				.Select(i => new ChangedFile($"{prefix}{i:D4}.txt", StatusType.Modified, StatusType.None))
				.ToArray();
		}

		private static ChangedFile[] MakeStagedFiles(int count, string prefix = "file")
		{
			return Enumerable.Range(0, count)
				.Select(i => new ChangedFile($"{prefix}{i:D4}.txt", StatusType.None, StatusType.Modified))
				.ToArray();
		}

		private static ChangedFile[] MakeTreeFiles(int folders, int filesPerFolder)
		{
			return Enumerable.Range(0, folders)
				.SelectMany(folder => Enumerable.Range(0, filesPerFolder)
					.Select(file => new ChangedFile($"folder{folder:D3}/file{file:D3}.txt", StatusType.Modified, StatusType.None)))
				.ToArray();
		}

		private static ScrollViewer GetScrollViewer(FileListUserControl list)
		{
			return list.TreeView.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
		}

		private static Thumb GetThumb(FileListUserControl list)
		{
			ScrollBar bar = list.TreeView.GetVisualDescendants().OfType<ScrollBar>()
				.FirstOrDefault(b => b.Name == "PART_VerticalScrollBar" || b.Orientation == global::Avalonia.Layout.Orientation.Vertical);
			return bar?.GetVisualDescendants().OfType<Thumb>().FirstOrDefault();
		}

		private static double ThumbHeight(FileListUserControl list)
		{
			Thumb thumb = GetThumb(list);
			return thumb?.Bounds.Height ?? -1;
		}

		private static string Probe(FileListUserControl list, string label)
		{
			ScrollViewer sv = GetScrollViewer(list);
			if (sv == null)
			{
				return $"{label}: <no ScrollViewer>";
			}
			ScrollBar bar = list.TreeView.GetVisualDescendants().OfType<ScrollBar>()
				.FirstOrDefault(b => b.Name == "PART_VerticalScrollBar");
			Thumb thumb = GetThumb(list);
			return $"{label}: extent={sv.Extent.Height:F1} viewport={sv.Viewport.Height:F1} max={sv.ScrollBarMaximum.Y:F1} "
				+ $"offset={sv.Offset.Y:F1} items={list.Items.Length} barVisible={(bar == null ? "n/a" : bar.IsVisible.ToString())} "
				+ $"thumbH={(thumb == null ? "n/a" : thumb.Bounds.Height.ToString("F1"))}";
		}

		private static void Pump()
		{
			Dispatcher.UIThread.RunJobs();
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
			Dispatcher.UIThread.RunJobs();
		}

		[Fact]
		public void FileList_ScrollExtent_Updates_After_Incremental_Remove_And_Add()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window { Width = 400, Height = 300 };
				var list = new FileListUserControl();
				window.Content = list;
				window.Show();
				Dispatcher.UIThread.RunJobs();

				// ===== 1) 初始 200 个未暂存文件（diff 全新增 200 条 < 256 阈值 → 增量路径）=====
				ChangedFile[] files200 = MakeUnstaged(200);
				list.SetItemSource(files200, forceRefresh: false, restoreSelection: false);
				Pump();
				_output.WriteLine(Probe(list, "after 200"));

				// ===== 2) 暂存一半（增量移除 100）——生产 SetData 路径 =====
				list.SetItemSource(files200.Take(100).ToArray(), forceRefresh: false, restoreSelection: false);
				Pump();
				string probe100 = Probe(list, "after remove 100");
				_output.WriteLine(probe100);
				double thumb100 = ThumbHeight(list);

				// ===== 3) 再加回 100（增量新增）=====
				list.SetItemSource(files200, forceRefresh: false, restoreSelection: false);
				Pump();
				string probe200Again = Probe(list, "after add back 100");
				_output.WriteLine(probe200Again);
				double thumb200Again = ThumbHeight(list);

				ScrollViewer sv = GetScrollViewer(list);
				double extent100 = ParseExtent(probe100);
				double extentAgain = ParseExtent(probe200Again);

				// 断言：extent 必须随条目数收缩/扩张（200 → 100 → 200）
				Assert.True(extent100 < 3000.0,
					$"移除 100 条后 extent 未收缩：100 条={extent100:F1}（应 ~2000，滚动条未刷新的直接证据）");
				Assert.True(extentAgain > 3000.0,
					$"加回 100 后 extent 未扩张：200 条={extentAgain:F1}（应 ~4000）");
				// Thumb 长度也应随内容收缩/扩张（viewport/extent 比例）
				Assert.True(thumb100 > thumb200Again * 1.2,
					$"移除 100 条后 thumb 未变长：100条 thumb={thumb100:F1}，200条 thumb={thumb200Again:F1}（thumb 样式未刷新）");
				window.Close();
			});
		}

		[Fact]
		public void FileList_ScrollExtent_Updates_While_Scrolled_Stage_And_Unstage()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window { Width = 400, Height = 360 };
				var list = new FileListUserControl();
				window.Content = list;
				window.Show();
				Dispatcher.UIThread.RunJobs();

				ChangedFile[] files200 = MakeUnstaged(200);
				list.SetItemSource(files200, forceRefresh: false, restoreSelection: false);
				Pump();
				ScrollViewer sv = GetScrollViewer(list);
				Assert.NotNull(sv);

				// ===== 滚到底部（模拟用户浏览到列表底部再操作）=====
				sv.ScrollToEnd();
				Pump();
				_output.WriteLine(Probe(list, "scrolled to bottom"));
				double offsetBottom = sv.Offset.Y;
				Assert.True(offsetBottom > 3000, $"未滚动到底部：offset={offsetBottom:F1}");

				// ===== 暂存后一半（底部 100 个文件被移走）=====
				ChangedFile[] remaining = files200.Take(100).ToArray();
				list.SetItemSource(remaining, forceRefresh: false, restoreSelection: false);
				Pump();
				_output.WriteLine(Probe(list, "after stage bottom 100"));

				// extent 必须收缩为 100 条 × 20px = 2000
				Assert.True(sv.Extent.Height < 2200,
					$"暂存后 extent 未收缩：{sv.Extent.Height:F1}（应为 ~2000，滚动条与内容不符的直接证据）");
				// offset 必须被钳制到新 max（2000 - viewport）
				Assert.True(sv.Offset.Y <= sv.ScrollBarMaximum.Y + 1,
					$"暂存后 offset={sv.Offset.Y:F1} 超出新 max={sv.ScrollBarMaximum.Y:F1}（滚动条位置与内容对不上）");

				// ===== 取消暂存（100 个文件加回）=====
				list.SetItemSource(files200, forceRefresh: false, restoreSelection: false);
				Pump();
				_output.WriteLine(Probe(list, "after unstage back 200"));

				Assert.True(sv.Extent.Height > 3800,
					$"取消暂存后 extent 未扩张：{sv.Extent.Height:F1}（应为 ~4000）");
				Assert.True(sv.Offset.Y <= sv.ScrollBarMaximum.Y + 1,
					$"取消暂存后 offset={sv.Offset.Y:F1} 超出 max={sv.ScrollBarMaximum.Y:F1}");

				window.Close();
			});
		}

		[Fact]
		public void StageFileControl_FullChain_Scrollbar_Stays_Consistent_Through_Stage_Unstage()
		{
			HeadlessAppBootstrap.EnsureStarted();
			// 生产链路：StageFileUserControl.SetDataAsync（300 条初始加载走后台重建路径 >256，
			// 暂存/取消暂存 100 条走增量路径），期间未暂存区滚到底部。
			Dispatcher.UIThread.InvokeAsync(async delegate
			{
				var window = new Window { Width = 340, Height = 640 };
				var stageControl = new StageFileUserControl();
				window.Content = stageControl;
				window.Show();
				Dispatcher.UIThread.RunJobs();

				FileListUserControl unstagedList = stageControl.UnstagedFilesFileListUserControl;
				FileListUserControl stagedList = stageControl.StagedFilesFileListUserControl;

				// ===== 300 个未暂存文件 → 初始加载走后台重建路径（>256）=====
				ChangedFile[] all = MakeUnstaged(300);
				await stageControl.SetDataAsync(all, Array.Empty<ChangedFile>(), selectFirstAvailableFile: false);
				Pump();
				_output.WriteLine(Probe(unstagedList, "initial 300"));

				ScrollViewer svUnstaged = GetScrollViewer(unstagedList);
				Assert.NotNull(svUnstaged);
				Assert.True(svUnstaged.Extent.Height > 5800,
					$"初始 extent 异常：{svUnstaged.Extent.Height:F1}（300 条应为 ~6000）");

				// ===== 滚到底部 =====
				svUnstaged.ScrollToEnd();
				Pump();
				_output.WriteLine(Probe(unstagedList, "scrolled bottom"));

				// ===== 暂存底部 100 个文件（未暂存 -100，已暂存 +100，均走增量路径）=====
				ChangedFile[] remainingUnstaged = all.Take(200).ToArray();
				ChangedFile[] nowStaged = all.Skip(200).Select(f => new ChangedFile(f.Path, StatusType.None, StatusType.Modified)).ToArray();
				await stageControl.SetDataAsync(remainingUnstaged, nowStaged, selectFirstAvailableFile: false);
				Pump();
				_output.WriteLine(Probe(unstagedList, "unstaged after stage 100"));
				_output.WriteLine(Probe(stagedList, "staged after stage 100"));

				Assert.True(svUnstaged.Extent.Height < 4200,
					$"暂存后未暂存区 extent 未收缩：{svUnstaged.Extent.Height:F1}（200 条应为 ~4000）");
				Assert.True(svUnstaged.Offset.Y <= svUnstaged.ScrollBarMaximum.Y + 1,
					$"暂存后未暂存区 offset={svUnstaged.Offset.Y:F1} 超出 max={svUnstaged.ScrollBarMaximum.Y:F1}");

				ScrollViewer svStaged = GetScrollViewer(stagedList);
				Assert.NotNull(svStaged);
				Assert.True(svStaged.Extent.Height > 1800,
					$"暂存后已暂存区 extent 未扩张：{svStaged.Extent.Height:F1}（100 条应为 ~2000）");
				Assert.True(svStaged.Offset.Y <= svStaged.ScrollBarMaximum.Y + 1,
					$"已暂存区 offset={svStaged.Offset.Y:F1} 超出 max={svStaged.ScrollBarMaximum.Y:F1}");

				// ===== 取消暂存（文件全部回到未暂存区）=====
				await stageControl.SetDataAsync(all, Array.Empty<ChangedFile>(), selectFirstAvailableFile: false);
				Pump();
				_output.WriteLine(Probe(unstagedList, "unstaged after unstage all"));
				_output.WriteLine(Probe(stagedList, "staged after unstage all"));

				Assert.True(svUnstaged.Extent.Height > 5800,
					$"取消暂存后未暂存区 extent 未扩张：{svUnstaged.Extent.Height:F1}（300 条应为 ~6000）");
				Assert.True(svStaged.Extent.Height < 400,
					$"取消暂存后已暂存区 extent 未收缩：{svStaged.Extent.Height:F1}（0 条应为 ~0）");
				Assert.True(svUnstaged.Offset.Y <= svUnstaged.ScrollBarMaximum.Y + 1,
					$"取消暂存后未暂存区 offset={svUnstaged.Offset.Y:F1} 超出 max={svUnstaged.ScrollBarMaximum.Y:F1}");

				window.Close();
				return 0;
			}).GetAwaiter().GetResult();
		}

		[Fact]
		public void StageFileControl_TreeMode_Folder_Collapse_Scrollbar_Consistent()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(async delegate
			{
				var window = new Window { Width = 340, Height = 640 };
				var stageControl = new StageFileUserControl();
				window.Content = stageControl;
				window.Show();
				Dispatcher.UIThread.RunJobs();

				// 树模式（带文件夹）
				stageControl.FileListsMode = FileListMode.Tree;
				Dispatcher.UIThread.RunJobs();

				FileListUserControl unstagedList = stageControl.UnstagedFilesFileListUserControl;

				// 10 文件夹 × 30 文件 = 300 条（含 10 个文件夹节点，重建路径）
				ChangedFile[] all = MakeTreeFiles(10, 30);
				await stageControl.SetDataAsync(all, Array.Empty<ChangedFile>(), selectFirstAvailableFile: false);
				Pump();
				_output.WriteLine(Probe(unstagedList, "tree initial 300+10 folders"));

				ScrollViewer sv = GetScrollViewer(unstagedList);
				Assert.NotNull(sv);

				// ===== 滚到底部 =====
				sv.ScrollToEnd();
				Pump();

				// ===== 暂存 folder009 整个文件夹（30 文件 + 文件夹节点一起消失）=====
			ChangedFile[] remaining = all.Where(f => !f.Path.StartsWith("folder009/")).ToArray();
			ChangedFile[] staged = all.Where(f => f.Path.StartsWith("folder009/"))
				.Select(f => new ChangedFile(f.Path, StatusType.None, StatusType.Modified)).ToArray();
			await stageControl.SetDataAsync(remaining, staged, selectFirstAvailableFile: false);
			Pump();
			_output.WriteLine(Probe(unstagedList, "tree after stage folder009"));

				// 270 文件 + 9 文件夹 = 279 行 ≈ 5580
				Assert.True(sv.Extent.Height < 5700,
					$"树模式暂存整个文件夹后 extent 未收缩：{sv.Extent.Height:F1}（应 ~5580）");
				Assert.True(sv.Offset.Y <= sv.ScrollBarMaximum.Y + 1,
					$"树模式暂存后 offset={sv.Offset.Y:F1} 超出 max={sv.ScrollBarMaximum.Y:F1}");

				window.Close();
				return 0;
			}).GetAwaiter().GetResult();
		}

		private static double ParseExtent(string probe)
		{
			string token = probe.Split(' ').First(p => p.StartsWith("extent="));
			return double.Parse(token.Substring("extent=".Length));
		}
	}
}
