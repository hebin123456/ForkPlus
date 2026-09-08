// E2E 模块4（2026-09-05）：修订详情视图。
// 覆盖：三 tab（Commit 摘要 / Changes 变更 / File Tree 文件树）真实 git 数据加载与切换、
// 摘要字段断言（作者/主题/SHA/变更文件列表）、Changes 文件列表 + diff 内容加载、
// 文件树选择文件 → 内容预览、Range 双提交对比（Commit/FileTree tab 禁用 + 自动切 Changes）、
// reflog 显示开关（状态栏 Reflog mode enabled）、tags 隐藏开关与引用过滤（Filtered by 状态栏）。
// 截图 → docs/evidence/e2e/04-revisiondetails/。
using System;
using System.Linq;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e04RevisionDetailsTests
	{
		[Fact]
		public void RevisionDetails_ThreeTabsLoadRealData()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string repo = TestRepoFactory.CreateBranches();
			try
			{
				var module = new GitModule(repo, System.IO.Path.Combine(repo, ".git"), null, null);
				HeadlessAppBootstrap.Run(delegate
				{
					var control = new RepositoryUserControl();
					control.OpenRepository(module);
					var window = new ForkPlus.UI.CustomWindow { Width = 1920, Height = 1080, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();

					// ===== 1) 初始加载：首行自动选中 → 修订详情 Commit tab（GetFullRevisionDetails 真实管线） =====
					control.InvalidateAndRefresh(SubDomain.All);
					var revList = control.Content.RevisionListViewUserControl;
					bool loaded = UiClick.WaitFor(delegate
					{
						return revList.RevisionsDataSource.Count > 0;
					});
					Assert.True(loaded, "修订列表未加载出数据（15s 超时）");

					// ===== 2) 显式选中 c5 on feature/two（同秒提交下行序不保证，按主题扫行定位） =====
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
					// 用 revList.Select（内部补发 NotifySelectionChangedFromCurrentItems → 修订详情刷新）；
					// 直接调 listView.Select 会被 IsMultiselectionInProgress 吞掉 SelectionChanged 通知
					revList.Select(new int[1] { c5Row });
					var details = control.Content.RevisionDetails;
					bool detailsLoaded = UiClick.WaitFor(delegate
					{
						return details.FullRevisionDetails != null
							&& details.FullRevisionDetails.RevisionDetails.Message.Trim() == "c5 on feature/two";
					});
					Assert.True(detailsLoaded, "c5 修订详情未加载，当前加载的是: "
						+ (details.FullRevisionDetails?.RevisionDetails?.Message ?? "<null>"));
					var summary = details.SummaryUserControl;
					var changes = details.ChangesUserControl;
					var fileTree = details.FileTreeUserControl;
					Assert.True(summary.IsVisible, "选中单提交后应停留在 Commit（摘要）tab");
					Assert.False(changes.IsVisible, "Changes tab 不应同时可见");
					Assert.False(fileTree.IsVisible, "File Tree tab 不应同时可见");

					// ===== 3) 摘要字段断言（c5 = feature/two 分支的提交，新增 two.txt） =====
					Assert.Equal("Test", summary.AuthorTextBlock.Text);
					Assert.Equal("c5 on feature/two", summary.SubjectTextBlock.Text);
					Assert.True(summary.ShaTextBlock.Text.Length == 40, "SHA 应为 40 位，实际 " + summary.ShaTextBlock.Text);
					Assert.True(summary.DiffList.ItemCount == 1, "c5 变更文件列表应只有 two.txt，实际 " + summary.DiffList.ItemCount);
					ScreenshotHelper.Snap(window, "01-revisiondetails-commit-summary", "04-revisiondetails");

				// ===== 4) Changes tab：文件列表 + 选中文件 diff 内容（DelayedAction → Task.Run → git diff） =====
					details.ChangesRadioButton.IsChecked = true; // IsCheckedChanged 路由 → SelectTab → UpdateTabContent
					Dispatcher.UIThread.RunJobs();
					Assert.True(changes.IsVisible, "切到 Changes 后变更 tab 应可见");
					Assert.False(summary.IsVisible, "切走后摘要 tab 应隐藏");
					bool fileListLoaded = UiClick.WaitFor(delegate
					{
						return changes.FileListUserControl.Items.Length == 1;
					});
					Assert.True(fileListLoaded, "c5 的变更文件列表应加载出 two.txt");
					Assert.Equal("two.txt", changes.FileListUserControl.Items[0].Path);
					Assert.Equal("two.txt", changes.SelectedFile.Path); // UpdateFileList 自动选中首个文件
					bool diffLoaded = UiClick.WaitFor(delegate
					{
						return changes.FileDiffControl.Content != null && changes.FileDiffControl.Content.Succeeded;
					});
					Assert.True(diffLoaded, "选中文件后 diff 内容应加载成功（15s 超时）");
					Assert.True(diffLoaded && changes.FileDiffControl.Content.Result is ParsedDiffContent, "two.txt 的 diff 应是解析后的文本 diff，实际 " + changes.FileDiffControl.Content.Result?.GetType().Name);
					ScreenshotHelper.Snap(window, "02-revisiondetails-changes-tab", "04-revisiondetails");

				// ===== 5) File Tree tab：该提交时点的全量文件树 + 选文件看内容 =====
					details.FileTreeRadioButton.IsChecked = true;
					Dispatcher.UIThread.RunJobs();
					Assert.True(fileTree.IsVisible, "切到 File Tree 后文件树 tab 应可见");
					bool treeLoaded = UiClick.WaitFor(delegate
					{
						return fileTree.FilesTreeView.RootItem != null && fileTree.FilesTreeView.RootItem.Children.Count >= 2;
					});
					Assert.True(treeLoaded, "c5 时点文件树应含 main.txt/two.txt（15s 超时）");
					var treeFiles = fileTree.FilesTreeView.RootItem.Children
						.OfType<RevisionFileTreeViewItem>()
						.Where(i => i.FileTreeItem != null)
						.Select(i => i.FileTreeItem.Filename)
						.ToArray();
					Assert.True(treeFiles.Contains("two.txt") && treeFiles.Contains("main.txt") && !treeFiles.Contains("one.txt"),
						"c5（feature/two 时点）文件树应只有 main.txt/two.txt（one.txt 属于 feature/one），实际: " + string.Join(",", treeFiles));
					var twoFileNode = fileTree.FilesTreeView.RootItem.Children
						.OfType<RevisionFileTreeViewItem>()
						.First(i => i.FileTreeItem != null && i.FileTreeItem.Filename == "two.txt");
					fileTree.FilesTreeView.SelectAndFocus(twoFileNode); // 生产 SelectionChanged → UpdateFileDetails
					bool contentLoaded = UiClick.WaitFor(delegate
					{
						return fileTree.FileContentControl.Content != null && fileTree.FileContentControl.Content.Succeeded;
					});
					Assert.True(contentLoaded, "选中 two.txt 后应加载出文件内容预览（15s 超时）");
					ScreenshotHelper.Snap(window, "03-revisiondetails-filetree-tab", "04-revisiondetails");

					// ===== 5) 切回 Commit tab（恢复摘要） =====
					details.CommitRadioButton.IsChecked = true;
					Dispatcher.UIThread.RunJobs();
					Assert.True(summary.IsVisible, "切回后摘要 tab 应恢复可见");
					Assert.False(fileTree.IsVisible, "切走后文件树 tab 应隐藏");
					ScreenshotHelper.Snap(window, "04-revisiondetails-back-to-summary", "04-revisiondetails");

					// ===== 6) Range 对比（选中两行 → Range target → Commit/FileTree 禁用 + 自动切 Changes） =====
					int c4Row = -1;
					for (int row2 = 0; row2 < revList.RevisionsDataSource.Count; row2++)
					{
						if (revList.RevisionsDataSource.GetDecoratedRevisionAtRow(row2)?.Subject == "c4 on feature/one")
						{
							c4Row = row2;
							break;
						}
					}
					Assert.True(c4Row >= 0, "应能定位到 c4 on feature/one 行");
					revList.Select(new int[2] { c5Row, c4Row });
					bool rangeLoaded = UiClick.WaitFor(delegate
					{
						return details.FullRevisionDetails is FullRevisionDetailsRange;
					});
					Assert.True(rangeLoaded, "选中两行后应加载 Range 对比详情（15s 超时）");
					Assert.False(details.CommitRadioButton.IsEnabled, "Range 对比下 Commit tab 应禁用");
					Assert.False(details.FileTreeRadioButton.IsEnabled, "Range 对比下 File Tree tab 应禁用");
					Assert.True(changes.IsVisible, "Range 对比应自动切到 Changes tab");
					Assert.True(details.RevisionDetailsHeaderUserControl.SwapRevisionsButton.IsVisible, "Range 对比头部应显示交换方向按钮");
					bool rangeFilesLoaded = UiClick.WaitFor(delegate
					{
						return changes.FileListUserControl.Items.Length >= 2;
					});
					Assert.True(rangeFilesLoaded, "c5↔c4 对比应列出 one.txt 与 two.txt 两个变更文件");
					ScreenshotHelper.Snap(window, "05-revisiondetails-range-compare", "04-revisiondetails");
					window.Close();
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		[Fact]
		public void RevisionList_ReflogAndTagVisibilityToggles()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string repo = TestRepoFactory.CreateBranches();
			try
			{
				var module = new GitModule(repo, System.IO.Path.Combine(repo, ".git"), null, null);
				HeadlessAppBootstrap.Run(delegate
				{
					var control = new RepositoryUserControl();
					control.OpenRepository(module);
					var window = new ForkPlus.UI.CustomWindow { Width = 1920, Height = 1080, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();
					control.InvalidateAndRefresh(SubDomain.All);
					var revList = control.Content.RevisionListViewUserControl;
					bool loaded = UiClick.WaitFor(delegate
					{
						return revList.RevisionsDataSource.Count > 0;
					});
					Assert.True(loaded, "修订列表未加载出数据（15s 超时）");

					var statusBar = control.Content.RevisionListStatusBarUserControl;
					// ===== 1) 初始：无 reflog、无过滤 → 状态栏隐藏 =====
					Assert.False(control.RepositoryData.Reflog, "初始不应处于 reflog 模式");
					Assert.True(control.RepositoryData.References.FilterReferences.Length == 0, "初始不应有引用过滤");
					Assert.False(statusBar.IsVisible, "无 reflog/过滤时状态栏应隐藏");

					// ===== 2) reflog 显示开关（ToggleShowReflogInRevisionList 命令核心逻辑；
					//      headless 无 MainWindow，命令经 ActiveRepositoryUserControl 找不到控件，故直接走等价路径） =====
					control.ShowReflogInRevisionList = true;
					control.InvalidateAndRefresh(SubDomain.Revisions);
					bool reflogOn = UiClick.WaitFor(delegate
					{
						return control.RepositoryData != null && control.RepositoryData.Reflog;
					});
					Assert.True(reflogOn, "开启 reflog 后 RepositoryData.Reflog 应为 true（15s 超时）");
					Assert.True(statusBar.IsVisible, "reflog 模式状态栏应显示");
					Assert.Equal(Tr("Reflog mode enabled"), statusBar.HeaderTextBlock.Text);
					Assert.Equal(Tr("Exit"), statusBar.StatusBarButton.Content as string);
					ScreenshotHelper.Snap(window, "06-revisiondetails-reflog-enabled", "04-revisiondetails");

					// ===== 3) 关闭 reflog → 状态栏恢复隐藏 =====
					control.ShowReflogInRevisionList = false;
					control.InvalidateAndRefresh(SubDomain.Revisions);
					bool reflogOff = UiClick.WaitFor(delegate
					{
						return control.RepositoryData != null && !control.RepositoryData.Reflog && !statusBar.IsVisible;
					});
					Assert.True(reflogOff, "关闭 reflog 后状态栏应恢复隐藏（15s 超时）");

					// ===== 4) 引用过滤（tags 隐藏开关的另一半：按 tag 过滤提交列表） =====
					Tag tag = control.RepositoryData.References.Items.OfType<Tag>().First(t => t.FullReference == "refs/tags/v1.0");
					ForkPlus.UI.UserControls.RepositoryUserControl.Commands.UpdateReferenceFilter
						.SetFilterState(control, tag, ReferenceFilterState.Filter);
					bool filterOn = UiClick.WaitFor(delegate
					{
						return control.RepositoryData.References.FilterReferences.Length == 1;
					});
					Assert.True(filterOn, "设置 tag 过滤后 FilterReferences 应含 1 项（15s 超时）");
					Assert.True(statusBar.IsVisible, "过滤生效时状态栏应显示");
					Assert.Equal(Tr("Filtered by:"), statusBar.HeaderTextBlock.Text);
					Assert.Equal("'v1.0'", statusBar.ReferencesTextBlock.Text);
					Assert.Equal(Tr("Clear filter"), statusBar.StatusBarButton.Content as string);
					bool filteredList = UiClick.WaitFor(delegate
					{
						return revList.RevisionsDataSource.Count < 6; // 过滤后只剩 v1.0 可达提交（< 全量）
					});
					Assert.True(filteredList, "按 v1.0 过滤后提交列表应缩小");
					ScreenshotHelper.Snap(window, "07-revisiondetails-tag-filtered", "04-revisiondetails");

					// ===== 5) 清除过滤（生产命令路径） =====
					ForkPlus.UI.UserControls.RepositoryUserControl.Commands.UpdateReferenceFilter.ClearFilter(control);
					bool filterOff = UiClick.WaitFor(delegate
					{
						return control.RepositoryData.References.FilterReferences.Length == 0 && !statusBar.IsVisible;
					});
					Assert.True(filterOff, "清除过滤后 FilterReferences 应清空且状态栏隐藏（15s 超时）");

					// ===== 6) tags 隐藏开关（ToggleHideTags 命令核心逻辑：HideTags 设置 + References 重载） =====
					module.Settings.HideTags = true;
					module.Settings.Save();
					control.InvalidateAndRefresh(SubDomain.References);
					bool tagsHidden = UiClick.WaitFor(delegate
					{
						return control.RepositoryData.References.Items.All(r => !r.FullReference.StartsWith("refs/tags/"));
					});
					Assert.True(tagsHidden, "HideTags 开启后引用中不应再有 refs/tags/*（15s 超时）");
					ScreenshotHelper.Snap(window, "08-revisiondetails-tags-hidden", "04-revisiondetails");

					// ===== 7) 恢复：关闭 HideTags → tags 回到引用列表 =====
					module.Settings.HideTags = false;
					module.Settings.Save();
					control.InvalidateAndRefresh(SubDomain.References);
					bool tagsRestored = UiClick.WaitFor(delegate
					{
						return control.RepositoryData.References.Items.Count(r => r.FullReference.StartsWith("refs/tags/")) == 2;
					});
					Assert.True(tagsRestored, "关闭 HideTags 后 v1.0/v2.0 应回到引用列表（15s 超时）");
				window.Close();
			});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 工具 ============================

		/// <summary>按当前 UI 语言取期望文案（与生产代码同一翻译函数，语言无关断言）。</summary>
		private static string Tr(string text)
		{
			return ForkPlus.UI.UserControls.Preferences.PreferencesLocalization.Translate(
				text, ForkPlus.Settings.ForkPlusSettings.Default.UiLanguage);
		}
	}
}
