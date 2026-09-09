// E2E 暂存区滚动条刷新回归（v4.0.1 bug3）：
// 未暂存/已暂存区域把文件（整个文件夹）暂存或取消暂存后，区域 ScrollViewer 的
// extent / offset / thumb 必须与收缩/扩张后的内容一致，否则滚动条位置和样式
// 与左侧文件树对不上（用户反馈）。走真实 MainWindow 生产路径（TabManager.OpenRepository
// → ActivateCommitView → StageButton 点击 → ToggleFileStageCommand → JobQueue 后台
// git add/reset → Dispatcher.Post → UpdateRepositoryStatus → SetDataAsync 全链路）。
using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2eStageScrollbarRefreshTests
	{
		private static ScrollViewer GetScrollViewer(FileListUserControl list)
		{
			return list.TreeView.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
		}

		private static ScrollBar GetVerticalScrollBar(FileListUserControl list)
		{
			return list.TreeView.GetVisualDescendants().OfType<ScrollBar>()
				.FirstOrDefault(b => b.Orientation == global::Avalonia.Layout.Orientation.Vertical);
		}

		private static double ThumbHeight(FileListUserControl list)
		{
			ScrollBar bar = GetVerticalScrollBar(list);
			Thumb thumb = bar?.GetVisualDescendants().OfType<Thumb>().FirstOrDefault();
			return thumb?.Bounds.Height ?? -1;
		}

		private static double TrackHeight(FileListUserControl list)
		{
			ScrollBar bar = GetVerticalScrollBar(list);
			Track track = bar?.GetVisualDescendants().OfType<Track>().FirstOrDefault();
			return track?.Bounds.Height ?? -1;
		}

		// thumb 底边在 track 坐标系里的位置（offset=max 时应 ≈ trackH，即贴住轨道底端）
		private static double ThumbBottom(FileListUserControl list)
		{
			ScrollBar bar = GetVerticalScrollBar(list);
			Thumb thumb = bar?.GetVisualDescendants().OfType<Thumb>().FirstOrDefault();
			return thumb?.Bounds.Bottom ?? -1;
		}

		private static string ScrollProbe(FileListUserControl list, string label)
		{
			ScrollViewer sv = GetScrollViewer(list);
			if (sv == null)
			{
				return label + ": <no ScrollViewer>";
			}
			return label + ": extent=" + sv.Extent.Height.ToString("F1")
				+ " viewport=" + sv.Viewport.Height.ToString("F1")
				+ " max=" + sv.ScrollBarMaximum.Y.ToString("F1")
				+ " offset=" + sv.Offset.Y.ToString("F1")
				+ " items=" + (list.TreeView.RootItem?.Children.Count ?? -1)
				+ " thumbH=" + ThumbHeight(list).ToString("F1")
				+ " trackH=" + TrackHeight(list).ToString("F1");
		}

		[Fact]
		public void CommitView_StageAndUnstageFolderWhileScrolled_ScrollbarMatchesNewContent()
		{
			string repo = TestRepoFactory.CreateManyChangedFiles(12, 20);
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// ===== 1) 切到 Commit 视图（生产公共入口），等 240 个未暂存装配 =====
						repoControl.ActivateCommitView();
						Dispatcher.UIThread.RunJobs();
						CommitUserControl commit = repoControl.Content.CommitUserControl;
						StageFileUserControl stage = commit.StageFileUserControl;
						Assert.True(UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.Length == 240 && stage.AllStagedFiles.Length == 0;
						}), "初始状态未装配：应 240 未暂存 / 0 已暂存，实际 "
							+ stage.AllUnstagedFiles.Length + "/" + stage.AllStagedFiles.Length);

						FileListUserControl unstagedList = stage.UnstagedFilesFileListUserControl;
						FileListUserControl stagedList = stage.StagedFilesFileListUserControl;
						ScrollViewer unstagedSv = GetScrollViewer(unstagedList);
						ScrollViewer stagedSv = GetScrollViewer(stagedList);
						Assert.True(unstagedSv != null, "未暂存列表应有 ScrollViewer");
						Assert.True(stagedSv != null, "已暂存列表应有 ScrollViewer");

						// 初始 extent：12 文件夹 + 240 文件 = 252 行 × 20px ≈ 5040px（视口 ~300px）
						double extent0 = unstagedSv.Extent.Height;
						Assert.True(extent0 > 3000, "初始 extent 应远超视口（12 文件夹 + 240 文件），实际 " + extent0.ToString("F1"));

						// ===== 2) 滚到底部（用户场景：滚动浏览后选中靠近底部的文件夹暂存）=====
						unstagedSv.ScrollToEnd();
						Dispatcher.UIThread.RunJobs();
						double offset0 = unstagedSv.Offset.Y;
						Assert.True(offset0 > 1000, "应已滚动到底部，实际 offset=" + offset0.ToString("F1"));

						// ===== 3) 选中 folder010 目录节点 → 点 Stage（20 文件 + 文件夹节点一起移走）=====
						var folderNode = unstagedList.TreeView.RootItem.Children
							.OfType<FileListItem>()
							.First(n => n.ChangedFile.IsDirectory && n.ChangedFile.Path == "folder010");
						unstagedList.TreeView.SelectedItems.Clear();
						unstagedList.TreeView.SelectedItems.Add(folderNode);
						Dispatcher.UIThread.RunJobs();

						UiClick.Click(stage.StageButton);
						bool staged = UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.Length == 220 && stage.AllStagedFiles.Length == 20;
						});
						Assert.True(staged, "暂存 folder010 后应 220 未暂存 / 20 已暂存，实际 "
							+ stage.AllUnstagedFiles.Length + "/" + stage.AllStagedFiles.Length);

						// ===== 4) 未暂存列表：extent 应收缩 ~21 行（420px），offset 应被钳制 =====
						Dispatcher.UIThread.RunJobs();
						string probe1 = ScrollProbe(unstagedList, "unstaged after stage folder010");
						double extent1 = unstagedSv.Extent.Height;
						Assert.True(extent1 < extent0 - 300,
							"暂存整个文件夹后未暂存 extent 未收缩：" + probe1 + "（初始 " + extent0.ToString("F1") + "）");
						Assert.True(unstagedSv.Offset.Y <= unstagedSv.ScrollBarMaximum.Y + 0.5,
							"暂存后 offset 超出 max：" + probe1);

						// thumb 长度应与 viewport/extent 比例精确一致（WPF 原版无 MinHeight 下限；
						// 修复前被 18px 下限钳制成 2.3 倍失真，±15% 容差）
						double trackH = TrackHeight(unstagedList);
						double thumbH = ThumbHeight(unstagedList);
						if (trackH > 10 && thumbH > 0)
						{
							double ideal = trackH * unstagedSv.Viewport.Height / Math.Max(unstagedSv.Extent.Height, 1);
							Assert.True(Math.Abs(thumbH - ideal) <= Math.Max(ideal * 0.15, 2),
								"暂存后 thumb 长度与内容比例不符：thumb=" + thumbH.ToString("F1")
									+ " ideal=" + ideal.ToString("F1") + "（" + probe1 + "）");
						}

						// thumb 位置：offset=max（滚到底）时 thumb 底边应贴住轨道底端
						if (trackH > 10 && thumbH > 0)
						{
							Assert.True(Math.Abs(ThumbBottom(unstagedList) - trackH) <= 1.5,
								"暂存后滚到底但 thumb 未贴轨道底端：thumbBottom=" + ThumbBottom(unstagedList).ToString("F1")
									+ " trackH=" + trackH.ToString("F1") + "（" + probe1 + "）");
						}

						// ===== 5) 已暂存列表：extent 应从 0 扩张到 ~21 行（20 文件 + 1 文件夹节点）=====
						// 同时验证 thumb 随内容增多而变长（比例 ~viewport/extent，远大于 18px 下限时代码值）
						double stagedExtent1 = stagedSv.Extent.Height;
						Assert.True(stagedExtent1 > 380,
							"暂存后已暂存 extent 应扩张到 ~420px，实际 " + stagedExtent1.ToString("F1"));
						double stagedTrackH = TrackHeight(stagedList);
						double stagedThumbH = ThumbHeight(stagedList);
						if (stagedTrackH > 10 && stagedThumbH > 0)
						{
							double stagedIdeal = stagedTrackH * stagedSv.Viewport.Height / Math.Max(stagedSv.Extent.Height, 1);
							Assert.True(Math.Abs(stagedThumbH - stagedIdeal) <= Math.Max(stagedIdeal * 0.15, 2),
								"暂存后已暂存列表 thumb 长度应随内容扩张变长：thumb=" + stagedThumbH.ToString("F1")
									+ " ideal=" + stagedIdeal.ToString("F1"));
						}

						// ===== 6) 滚动已暂存列表到底 → 点 Unstage（收缩方向 + 钳制）=====
						stagedSv.ScrollToEnd();
						Dispatcher.UIThread.RunJobs();
						double stagedOffset1 = stagedSv.Offset.Y;

						var stagedFolderNode = stagedList.TreeView.RootItem.Children
							.OfType<FileListItem>()
							.FirstOrDefault(n => n.ChangedFile.IsDirectory);
						Assert.True(stagedFolderNode != null, "已暂存列表应含 folder010 目录节点");
						stagedList.TreeView.SelectedItems.Clear();
						stagedList.TreeView.SelectedItems.Add(stagedFolderNode);
						Dispatcher.UIThread.RunJobs();

						UiClick.Click(stage.UnstageButton);
						bool unstaged = UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.Length == 240 && stage.AllStagedFiles.Length == 0;
						});
						Assert.True(unstaged, "取消暂存后应回到 240 未暂存 / 0 已暂存，实际 "
							+ stage.AllUnstagedFiles.Length + "/" + stage.AllStagedFiles.Length);

						Dispatcher.UIThread.RunJobs();
					string probe2 = ScrollProbe(stagedList, "staged after unstage folder010");
					// 空列表时 extent 会报告为视口高度（空面板铺满视口），滚动条是否
					// 正确"归零"看可滚动范围：max 回 0 且 extent 不再超出视口。
					Assert.True(stagedSv.ScrollBarMaximum.Y < 1.0,
						"取消暂存后已暂存区不应还有可滚动范围：" + probe2 + "（收缩前 " + stagedExtent1.ToString("F1") + "）");
					Assert.True(stagedSv.Extent.Height - stagedSv.Viewport.Height < 20.0,
						"取消暂存后已暂存 extent 仍超出视口：" + probe2 + "（收缩前 " + stagedExtent1.ToString("F1") + "）");
					Assert.True(stagedSv.Offset.Y <= stagedSv.ScrollBarMaximum.Y + 0.5,
						"取消暂存后 offset 超出 max：" + probe2);

						// ===== 7) 未暂存列表：extent 应恢复到初始水平，thumb 比例同步恢复 =====
						Dispatcher.UIThread.RunJobs();
						string probe3 = ScrollProbe(unstagedList, "unstaged after unstage");
						Assert.True(Math.Abs(unstagedSv.Extent.Height - extent0) < 80,
							"取消暂存后未暂存 extent 应回到初始水平：" + probe3 + "（初始 " + extent0.ToString("F1") + "）");
						double thumbH2 = ThumbHeight(unstagedList);
						double trackH2 = TrackHeight(unstagedList);
						if (trackH2 > 10 && thumbH2 > 0)
						{
							double ideal2 = trackH2 * unstagedSv.Viewport.Height / Math.Max(unstagedSv.Extent.Height, 1);
							Assert.True(Math.Abs(thumbH2 - ideal2) <= Math.Max(ideal2 * 0.15, 2),
								"取消暂存后 thumb 比例应回到初始水平：thumb=" + thumbH2.ToString("F1")
									+ " ideal=" + ideal2.ToString("F1") + "（" + probe3 + "）");
						}
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}
	}
}
