// E2E 模块8（2026-09-05）：DiffPopupWindow（diff 弹窗）。
// 覆盖：Commit 视图 Space 打开弹窗（生产入口：FileList KeyDown Tunnel → ShowDiffPopup 事件 →
//   CommitUserControl.ShowDiffPopup → CreateCommitDiff → ShowAtCenter(0.9 父窗) → UpdateDiff）、
//   弹窗内容装配（Title=文件路径、CommitFileDiffControl + VisualPatch）、Escape 关闭、Space 再开
//   （Closed → _diffPopupWindow 置空重开）、弹窗内 Space 关闭、长行弹窗水平滚动条范围与滚动、
//   弹窗内 Up/Down 换文件（SelectNext/SelectPrevious → 选择变化 → 异步 diff → 弹窗 Title/内容更新）。
// 截图 → docs/evidence/e2e/08-diffpopup/。
// 测试经验（模块7 遗产）：PopupDiffLayoutMode 工厂默认即 SideBySide(1)，对布局敏感的断言必须
//   显式固定 + finally 恢复后 Save() 落盘（防污染用户 settings.json）。
using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Settings;
using ForkPlus.UI;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Controls.Editor.Diff;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e08DiffPopupWindowTests
	{
		private static DiffPopupWindow FindPopup()
		{
			return WpfApp.Windows.OfType<DiffPopupWindow>().FirstOrDefault();
		}

		/// <summary>Commit 视图选中 unstaged 文件并等待行内 diff 装配完成。</summary>
		private static CommitUserControl OpenCommitViewAndWaitDiff(string repo, string filePath, out MainWindow window)
		{
			RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out window);
			repoControl.ActivateCommitView();
			Dispatcher.UIThread.RunJobs();
			CommitUserControl commit = repoControl.Content.CommitUserControl;
			StageFileUserControl stage = commit.StageFileUserControl;
			Assert.True(UiClick.WaitFor(delegate
			{
				return stage.AllUnstagedFiles.Any(f => f.Path == filePath);
			}), "工作区状态未装配（未找到未暂存文件 " + filePath + "）");
			stage.UnstagedFilesFileListUserControl.SelectFile(filePath);
			Dispatcher.UIThread.RunJobs();
			Assert.True(UiClick.WaitFor(delegate
			{
				return commit.FileDiffControl.Content != null && commit.FileDiffControl.Content.Succeeded;
			}), filePath + " 的行内 diff 未加载");
			return commit;
		}

		/// <summary>FileList 上按 Space（生产 KeyDown Tunnel 处理器）→ 打开 diff 弹窗。</summary>
		private static void PressSpaceOnFileList(StageFileUserControl stage)
		{
			stage.UnstagedFilesFileListUserControl.RaiseEvent(new KeyEventArgs
			{
				RoutedEvent = InputElement.KeyDownEvent,
				Key = Key.Space
			});
			Dispatcher.UIThread.RunJobs();
		}

		private static void PressKey(Window window, Key key)
		{
			window.RaiseEvent(new KeyEventArgs
			{
				RoutedEvent = InputElement.KeyDownEvent,
				Key = key
			});
			Dispatcher.UIThread.RunJobs();
		}

		/// <summary>收尾保险：关闭可能残留的 diff 弹窗（弹窗泄漏进 lifetime.Windows 会殃及后续用例）。</summary>
		private static void CloseLeftoverPopups()
		{
			DiffPopupWindow[] popups = WpfApp.Windows.OfType<DiffPopupWindow>().ToArray();
			foreach (DiffPopupWindow popup in popups)
			{
				try
				{
					popup.Close();
				}
				catch
				{
					// 收尾尽力而为
				}
			}
			Dispatcher.UIThread.RunJobs();
		}

		[Fact]
		public void DiffPopup_CommitView_SpaceOpens_EscapeCloses_SpaceReopensAndSpaceCloses()
		{
			string repo = TestRepoFactory.CreateWorkingDir();
			DiffLayoutMode originalLayout = ForkPlusSettings.Default.PopupDiffLayoutMode;
			try
			{
				// 固定 Split（单编辑器）保证断言确定性；恢复后落盘防用户配置污染（模块7 教训）
				ForkPlusSettings.Default.PopupDiffLayoutMode = DiffLayoutMode.Split;
				HeadlessAppBootstrap.Run(delegate
				{
					CommitUserControl commit = OpenCommitViewAndWaitDiff(repo, "a.txt", out var window);
					try
					{
						// ===== 1) 文件列表按 Space → 弹窗创建并居中 =====
						PressSpaceOnFileList(commit.StageFileUserControl);
						DiffPopupWindow popup = null;
						Assert.True(UiClick.WaitFor(delegate
						{
							popup = FindPopup();
							return popup != null;
						}), "Space 后应创建 DiffPopupWindow");
						Assert.Equal("a.txt", popup.Title); // UpdateDiff → Title=ChangedFile.Path
						Assert.True(popup.FileDiffControl.Content != null && popup.FileDiffControl.Content.Succeeded,
							"弹窗 FileDiffControl 应装配 diff 内容（UpdateDiff 行内内容）");
						Assert.True(popup.FileDiffControl is CommitFileDiffControl,
							"Commit 视图入口应创建 CommitFileDiffControl（CreateCommitDiff）");
						// ShowAtCenter：弹窗为父窗 90% 大小居中
						Assert.True(Math.Abs(popup.Width - window.Width * 0.9) < 2.0,
							"弹窗宽应为父窗 90%（实际 " + popup.Width.ToString("F0") + " / 期望 " + (window.Width * 0.9).ToString("F0") + "）");
						Assert.True(Math.Abs(popup.Height - window.Height * 0.9) < 2.0,
							"弹窗高应为父窗 90%（实际 " + popup.Height.ToString("F0") + "）");
						// 弹窗内编辑器装配（CommitCodeEditor + VisualPatch）
						CommitCodeEditor popupEditor = null;
						Assert.True(UiClick.WaitFor(delegate
						{
							popupEditor = UiClick.FindAll<CommitCodeEditor>(popup).FirstOrDefault();
							return popupEditor != null && popupEditor.VisualPatch != null;
						}), "弹窗内应装配 CommitCodeEditor 且带 VisualPatch");
						ScreenshotHelper.Snap(popup, "01-popup-from-commit-view", "08-diffpopup");

						// ===== 2) 弹窗内 Escape → 关闭（Tunnel KeyDown 处理器） =====
						PressKey(popup, Key.Escape);
						Assert.True(UiClick.WaitFor(delegate { return FindPopup() == null; }),
							"Escape 后弹窗应从 Windows 列表移除（Close）");

						// ===== 3) 再次 Space → 重开（Closed 已把 _diffPopupWindow 置空） =====
						PressSpaceOnFileList(commit.StageFileUserControl);
						DiffPopupWindow reopened = null;
						Assert.True(UiClick.WaitFor(delegate
						{
							reopened = FindPopup();
							return reopened != null;
						}), "Space 应能重开弹窗");
						Assert.Equal("a.txt", reopened.Title);
						Assert.True(UiClick.WaitFor(delegate
						{
							return UiClick.FindAll<CommitCodeEditor>(reopened).FirstOrDefault()?.VisualPatch != null;
						}), "重开的弹窗应重新装配 diff 内容");
						ScreenshotHelper.Snap(reopened, "02-popup-reopened", "08-diffpopup");

						// ===== 4) 弹窗内 Space → 关闭（Space-close 路径，提示文案约定） =====
						PressKey(reopened, Key.Space);
						Assert.True(UiClick.WaitFor(delegate { return FindPopup() == null; }),
							"弹窗内 Space 应关闭弹窗");
					}
					finally
					{
						CloseLeftoverPopups();
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				ForkPlusSettings.Default.PopupDiffLayoutMode = originalLayout;
				ForkPlusSettings.Default.Save();
				TestRepoFactory.Cleanup(repo);
			}
		}

		[Fact]
		public void DiffPopup_LongLineDiff_HorizontalScrollbarHasRangeAndScrolls()
		{
			string repo = TestRepoFactory.CreateLongLines();
			DiffLayoutMode originalLayout = ForkPlusSettings.Default.PopupDiffLayoutMode;
			try
			{
				ForkPlusSettings.Default.PopupDiffLayoutMode = DiffLayoutMode.Split;
				HeadlessAppBootstrap.Run(delegate
				{
					CommitUserControl commit = OpenCommitViewAndWaitDiff(repo, "wide.txt", out var window);
					try
					{
						PressSpaceOnFileList(commit.StageFileUserControl);
						DiffPopupWindow popup = null;
						Assert.True(UiClick.WaitFor(delegate
						{
							popup = FindPopup();
							return popup != null;
						}), "Space 后应创建 DiffPopupWindow");
						Assert.Equal("wide.txt", popup.Title);

						// ===== 弹窗内编辑器有水平滚动范围（长行 → extent 宽 > viewport） =====
						CommitCodeEditor editor = null;
						Assert.True(UiClick.WaitFor(delegate
						{
							editor = UiClick.FindAll<CommitCodeEditor>(popup).FirstOrDefault();
							return editor != null && editor.VisualPatch != null;
						}), "弹窗内应装配 CommitCodeEditor");
						Assert.True(UiClick.WaitFor(delegate
						{
							var sv = editor.GetVisualDescendants().OfType<ScrollViewer>()
								.FirstOrDefault(s => s.Name == "PART_ScrollViewer");
							return sv != null && sv.ScrollBarMaximum.X > 100.0;
						}), "长行弹窗应有水平滚动范围（ScrollBarMaximum.X > 100）");
						ScreenshotHelper.Snap(popup, "03-popup-long-line-scrollbar", "08-diffpopup");

						// ===== 水平滚动（PART_ScrollViewer.Offset 路径） =====
						editor.ScrollToHorizontalOffsetCompat(200.0);
						Dispatcher.UIThread.RunJobs();
						double offsetX = editor.TextArea.TextView.ScrollOffset.X;
						Assert.True(offsetX > 0.0, "弹窗内水平滚动应生效（offset=" + offsetX.ToString("F1") + "）");
						Assert.True(Math.Abs(offsetX - 200.0) < 1.0,
							"滚动偏移应到位（实际 " + offsetX.ToString("F1") + " / 期望 200）");
						ScreenshotHelper.Snap(popup, "04-popup-horizontal-scrolled", "08-diffpopup");
					}
					finally
					{
						CloseLeftoverPopups();
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				ForkPlusSettings.Default.PopupDiffLayoutMode = originalLayout;
				ForkPlusSettings.Default.Save();
				TestRepoFactory.Cleanup(repo);
			}
		}

		[Fact]
		public void DiffPopup_ArrowKeys_NavigateFilesAndUpdatePopupTitleAndDiff()
		{
			string repo = TestRepoFactory.CreateWorkingDir();
			DiffLayoutMode originalLayout = ForkPlusSettings.Default.PopupDiffLayoutMode;
			try
			{
				ForkPlusSettings.Default.PopupDiffLayoutMode = DiffLayoutMode.Split;
				HeadlessAppBootstrap.Run(delegate
				{
					CommitUserControl commit = OpenCommitViewAndWaitDiff(repo, "a.txt", out var window);
					StageFileUserControl stage = commit.StageFileUserControl;
					try
					{
						PressSpaceOnFileList(stage);
						DiffPopupWindow popup = null;
						Assert.True(UiClick.WaitFor(delegate
						{
							popup = FindPopup();
							return popup != null;
						}), "Space 后应创建 DiffPopupWindow");
						Assert.Equal("a.txt", popup.Title);

						// ===== 1) 弹窗内 Down → SelectNext → 选择移到下一个未暂存文件 =====
						PressKey(popup, Key.Down);
						// 异步链：SelectNextFile → TreeView.SelectedItems.Clear()（瞬时 SelectionChanged
						// → UpdateDiff(null) → Title 短暂变 "File Preview"）→ SelectAndFocus(下一文件)
						// → SelectionChanged → UpdateDiff(Task.Run git diff) → popup.UpdateDiff。
						// 不能等 "Title != a.txt"（会抓到瞬时的 "File Preview"），必须等最终期望值。
						Assert.True(UiClick.WaitFor(delegate
						{
							return string.Equals(popup.Title, "b.txt", StringComparison.Ordinal)
								|| string.Equals(popup.Title, "new.txt", StringComparison.Ordinal);
						}), "Down 后弹窗 Title 应切换到下一个文件（b.txt/new.txt）");
						string nextFile = popup.Title;
						// 主视图行内选择同步移动（SelectNextFile → TreeView.SelectedItems）
						Assert.True(UiClick.WaitFor(delegate
						{
							return stage.SelectedUnstagedFiles.Select(f => f.Path).Distinct().Contains(nextFile);
						}), "Down 后主视图选择应同步移到 " + nextFile);
						// 新文件的 diff 装配进弹窗
						Assert.True(UiClick.WaitFor(delegate
						{
							return popup.FileDiffControl.Content != null
								&& popup.FileDiffControl.Content.Succeeded
								&& popup.FileDiffControl.Content.Result.ChangedFile.Path == nextFile;
						}), "弹窗应装载 " + nextFile + " 的 diff 内容");
						ScreenshotHelper.Snap(popup, "05-popup-arrow-next-file", "08-diffpopup");

						// ===== 2) 弹窗内 Up → SelectPrevious → 回到 a.txt =====
						PressKey(popup, Key.Up);
						Assert.True(UiClick.WaitFor(delegate
						{
							return string.Equals(popup.Title, "a.txt", StringComparison.Ordinal);
						}), "Up 后弹窗 Title 应回到 a.txt");
						Assert.True(UiClick.WaitFor(delegate
						{
							return popup.FileDiffControl.Content != null
								&& popup.FileDiffControl.Content.Succeeded
								&& popup.FileDiffControl.Content.Result.ChangedFile.Path == "a.txt";
						}), "弹窗应重新装载 a.txt 的 diff 内容");
					}
					finally
					{
						CloseLeftoverPopups();
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				ForkPlusSettings.Default.PopupDiffLayoutMode = originalLayout;
				ForkPlusSettings.Default.Save();
				TestRepoFactory.Cleanup(repo);
			}
		}
		[Fact]
		public void DiffPopup_RevisionChangesView_SpaceOpensRevisionDiffPopup_EscapeCloses()
		{
			string repo = TestRepoFactory.CreateBranches();
			DiffLayoutMode originalLayout = ForkPlusSettings.Default.PopupDiffLayoutMode;
			try
			{
				ForkPlusSettings.Default.PopupDiffLayoutMode = DiffLayoutMode.Split;
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// ===== 选中 c5 on feature/two（同秒提交下行序不保证，按主题扫行定位——模块4 模式）=====
						var revList = repoControl.Content.RevisionListViewUserControl;
						Assert.True(UiClick.WaitFor(delegate { return revList.RevisionsDataSource.Count > 0; }),
							"修订列表未加载");
						int c5Row = -1;
						for (int row = 0; row < revList.RevisionsDataSource.Count; row++)
						{
							if (revList.RevisionsDataSource.GetDecoratedRevisionAtRow(row)?.Subject == "c5 on feature/two")
							{
								c5Row = row;
								break;
							}
						}
						Assert.True(c5Row >= 0, "应能定位到 c5 on feature/two 行");
						revList.Select(new int[1] { c5Row }); // Select 内部补发通知 → 修订详情刷新
						var details = repoControl.Content.RevisionDetails;
						Assert.True(UiClick.WaitFor(delegate
						{
							return details.FullRevisionDetails != null
								&& details.FullRevisionDetails.RevisionDetails.Message.Trim() == "c5 on feature/two";
						}), "c5 修订详情未加载");

						// ===== 切 Changes tab，等文件列表 + 行内 diff 装配 =====
						details.ChangesRadioButton.IsChecked = true;
						Dispatcher.UIThread.RunJobs();
						var changes = details.ChangesUserControl;
						Assert.True(UiClick.WaitFor(delegate { return changes.FileListUserControl.Items.Length == 1; }),
							"c5 变更文件列表应加载 two.txt");
						Assert.True(UiClick.WaitFor(delegate
						{
							return changes.FileDiffControl.Content != null && changes.FileDiffControl.Content.Succeeded;
						}), "行内 diff 未加载");

						// ===== 变更文件列表 Space → 弹窗（RevisionChangesUserControl → CreateRevisionDiff）=====
						changes.FileListUserControl.RaiseEvent(new KeyEventArgs
						{
							RoutedEvent = InputElement.KeyDownEvent,
							Key = Key.Space
						});
						Dispatcher.UIThread.RunJobs();
						DiffPopupWindow popup = null;
						Assert.True(UiClick.WaitFor(delegate
						{
							popup = FindPopup();
							return popup != null;
						}), "Space 后应创建 DiffPopupWindow");
						Assert.Equal("two.txt", popup.Title);
						// 变更视图入口创建基类 FileDiffControl（非 CommitFileDiffControl）
						Assert.False(popup.FileDiffControl is CommitFileDiffControl,
							"变更视图入口应创建基类 FileDiffControl（CreateRevisionDiff）");
						Assert.True(popup.FileDiffControl.Content != null && popup.FileDiffControl.Content.Succeeded,
							"弹窗 FileDiffControl 应装配 diff 内容");
						// ShowAtCenter：弹窗为父窗 90% 大小
						Assert.True(Math.Abs(popup.Width - window.Width * 0.9) < 2.0,
							"弹窗宽应为父窗 90%（实际 " + popup.Width.ToString("F0") + "）");
						// 修订 diff 内容渲染：TextDiffControl → DiffCodeEditor 装配非空文档
						Assert.True(UiClick.WaitFor(delegate
						{
							return UiClick.FindAll<DiffCodeEditor>(popup).Any(delegate (DiffCodeEditor e)
							{
								return e.Document != null && e.Document.TextLength > 0;
							});
						}), "弹窗内应装配 DiffCodeEditor 且文档非空");
						ScreenshotHelper.Snap(popup, "06-popup-from-changes-view", "08-diffpopup");

						// ===== Escape → 关闭 =====
						PressKey(popup, Key.Escape);
						Assert.True(UiClick.WaitFor(delegate { return FindPopup() == null; }),
							"Escape 后弹窗应关闭");
					}
					finally
					{
						CloseLeftoverPopups();
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				ForkPlusSettings.Default.PopupDiffLayoutMode = originalLayout;
				ForkPlusSettings.Default.Save();
				TestRepoFactory.Cleanup(repo);
			}
		}
	}
}
