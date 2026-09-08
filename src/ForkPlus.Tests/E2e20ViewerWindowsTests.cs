// E2E 模块20（2026-09-06）：查看类窗口 6 窗口（7 用例）。
// 覆盖：BlameWindow（多提交文件 blame：修订下拉/时间线/blame 块双归属（context 行归旧提交、
// Added 行归当前提交——CreateBlameItems 对齐 git blame HEAD~ 与 --unified=100000 全上下文
// diff）/修订切换导航 + Undo/Redo 栈生命周期）/FileHistoryWindow File 模式（历史列表装配 +
// 首项自动选中 → 防抖 RefreshDiff → FileDiffControl.Content + Escape 关闭）/FileHistoryWindow
// Directory 模式（FolderHistoryEntryViewModel 文件夹条目 + 子文件 SubItem 条目 +
// ExpandAllChildren 展开）/RepositoryOverviewWindow（树图数据装配（JobQueue →
// GetRepositoryOverviewDataGitCommand）→ 根选中联动：提交列表 + 作者列表 + 选中文件名）/
// RepositoryStatisticsWindow（Loaded → ShowStatistics → GetRepositoryStatsGitCommand →
// StatsContainer + AuthorStatListBox 作者聚合）/RevisionDetailsWindow（独立窗：Loaded →
// ShowRevisionDetails → RevisionDetailsUpdated → 标题 "sha7 subject" + Escape 关闭）/
// GoToLineWindow（OnSubmit 行号解析：空/非数字 → null、有效数字/边界 0 → 值）。
//
// 模式：全部走 E2eMainWindowHarness.OpenRepository（真实 MainWindow 生产入口）+ 窗口直构
// （BlameWindow/FileHistoryWindow/RepositoryOverviewWindow/RepositoryStatisticsWindow/
// RevisionDetailsWindow 构造器均为公开签名，入口命令的 FindBranchTip/GitModule 判空逻辑
// 在模块 11/15 已覆盖）。截图 2026-09-06 口径：主窗口 1920×1080，弹窗按自然比例。
//
// 本模块修复的迁移期生产 bug（模块19 已探针实证同类，本模块修复落地）：
// FileHistoryWindow.OnInitialized：WPF 原仓在 OnInitialized override 里加载文件历史，
// WPF 的 Initialized 在构造完成后触发（字段已就绪）；Avalonia 12 的 Initialized 在 TopLevel
// 基类构造链中触发（Window..ctor → TopLevel..ctor → PresentationSource..ctor →
// OnAttachedToVisualTreeCore → InitializeIfNeeded → OnInitialized），此刻派生类构造器字段
// 全未赋值（_repositoryUserControl=null → 第一行 NRE → catch 吞掉 → 历史列表/时间线/diff
// 永不装配，生产必现空窗口）。修复：加载挪到构造器尾部私有方法 LoadHistory()（方法体与
// 原版逐字节一致，仅去掉 base.OnInitialized() 调用）。
//
// 探针实证（2026-09-06，本模块环境闭环）：
// ① GetRevisionFileChangesGitCommand(showEntireFile: true) 的实现是 --unified=100000 +
//    --inter-hunk-context=100000（超上下文 diff），**不是**全文件 Added——blame 视图因此
//    保持"正常 diff 结构"：PreContext/PostContext 行走 git blame HEAD~ 的旧提交归属，
//    Added 行归当前修订（CreateBlameItems 的对齐规则）。文件 "f1\n" → "f1\nf1 more\n"：
//    行1 归 f1（context）、行2 归追加提交（Added）——blame 块双归属断言的基础。
// ② git blame HEAD~ -- f1.txt 对"父版本不存在的文件"（f2.txt 首次提交）返回
//    "fatal: no such path" 且 GetBlameGitCommand 捕获该 stderr 返回 Success(空 chunks)
//    ——CreateBlameItems 全 Added 行归当前提交，单提交文件的 blame 照常装配。
// ③ BlameWindow/FileHistoryWindow 的加载链均为 new Task(...).Start() + Dispatcher.Post
//    （非 JobQueue 型），UiClick.WaitFor 内部泵（RunJobs + 50ms 轮询）可直接等待装配完成。
// ④ BlameItemBodyViewModel : BlameItemViewModel（body/dummy 均为子类）——blame 块头部
//    （每块第一个条目）过滤必须用精确类型匹配 GetType() == typeof(BlameItemViewModel)。
// ⑤ RepositoryOverviewWindow 数据链：JobQueue("Read repository overview") → Dispatcher.Post
//    → DateRangeButton.DateRange 赋值 → DateRangeChanged → RefreshData → Treemap.DataSource
//    =（setter 内 SelectedIndexPath=null）+ SelectedIndexPath=FirstVisualItem()（setter 触发
//    SelectionChanged → 提交列表/作者列表/选中文件名联动）。等待锚点用 Fallback.IsVisible
//    == false（初始 Show，数据到达 Hide）。
// ⑥ RepositoryStatisticsWindow/RevisionDetailsWindow 的装配入口都是 base.Loaded 事件——
//    headless 下 Show() + RunJobs 会触发 Avalonia 的 Loaded（attach + 布局完成）。
//
// 设置污染防护（模块 7/12/14/19 教训沿用）：本模块窗口均只读设置（BlameWindowLocationState
// 仅在 SizeChanged/Activated 后持久化，headless 无用户交互不触发），无显式快照恢复需求。
using System;
using System.IO;
using System.Linq;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Diff;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.Dialogs.RepositoryOverview;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e20ViewerWindowsTests
	{
		private const string ModuleDir = "20-viewerwindows";

		// ============================ 共享助手 ============================

		private static ForkPlusDialogFooter FooterOf(ForkPlusDialogWindow dialog)
		{
			ForkPlusDialogFooter footer = dialog.GetVisualDescendants().OfType<ForkPlusDialogFooter>().FirstOrDefault();
			Assert.NotNull(footer);
			return footer;
		}

		/// <summary>派发 KeyDown（E2e08 DiffPopupWindow 的 Escape/Space 关闭同款管线）。</summary>
		private static void PressKey(Avalonia.Controls.Window window, Key key)
		{
			window.RaiseEvent(new KeyEventArgs
			{
				RoutedEvent = InputElement.KeyDownEvent,
				Key = key
			});
			Dispatcher.UIThread.RunJobs();
		}

		private static string GitOf(string repo, string args)
		{
			return TestRepoFactory.GitOutput(repo, args).Trim();
		}

		/// <summary>测试内追加提交（TestRepoFactory.Commit 为 private，等价复刻：写文件 + add + commit）。
		/// 消息不带空格（规避 Run 参数拼接的引号语义，模块18 口径）。</summary>
		private static void AppendCommit(string repo, string relPath, string content, string message)
		{
			File.WriteAllText(Path.Combine(repo, relPath), content);
			TestRepoFactory.GitOutput(repo, "add " + relPath);
			TestRepoFactory.GitOutput(repo, "commit -q -m " + message);
		}

		// ============================ 1) Blame 窗口 ============================

		[Fact]
		public void BlameWindow_MultiCommitBlameAndRevisionNavigation()
		{
			string repo = TestRepoFactory.CreateHistoryRewrite(checkoutFeature: true);
			try
			{
				// f1.txt 二次修改（feature 追加）：blame 语义需要"文件被多个提交改过"
				AppendCommit(repo, "f1.txt", "f1\nf1 more\n", "f1second");
				string headSha = GitOf(repo, "rev-parse HEAD");
				string f1Sha = GitOf(repo, "rev-parse feature~3"); // feature: f1→f2→f3→f1second
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						var dialog = new BlameWindow(repoControl, "f1.txt", null, null);
						dialog.Show();
						// 加载链：new Task(GetFirstRevision + GetFileHistory) → Dispatcher.Post →
						// RevisionsComboBox + _refreshBlame.InvokeNow（blame 二级 Task）
						Assert.True(UiClick.WaitFor(delegate { return dialog.BlameListBox.ItemsSource != null; }),
							"blame 列表应装配（15s 超时）");
						Dispatcher.UIThread.RunJobs();

						// —— 修订下拉（GetFileHistoryGitCommand 真实执行）：f1.txt 的 2 个提交，新→旧 ——
						var revisions = dialog.RevisionsComboBox.ItemsSource as RevisionViewModel[];
						Assert.NotNull(revisions);
						Assert.Equal(2, revisions.Length);
						Assert.Equal(headSha, revisions[0].Sha.ToString());
						Assert.Equal("f1second", revisions[0].RevisionSubject);
						Assert.Equal(f1Sha, revisions[1].Sha.ToString());
						Assert.Equal("feat: one", revisions[1].RevisionSubject);
						Assert.Equal(headSha, ((RevisionViewModel)dialog.RevisionsComboBox.SelectedItem).Sha.ToString());
						Assert.Equal(headSha, dialog.RevisionTimeLine.ActiveRevision.ToString());

						// —— blame 块双归属（探针①）：行1 "f1" 归 f1（context 行走 blame HEAD~ 归属），
						// 行2 "f1 more" 归 HEAD（Added 行归当前修订）——头部条目精确类型过滤（探针④）——
						var headers = dialog.BlameListBox.ItemsSource.Cast<object>()
							.Where(i => i.GetType() == typeof(BlameItemViewModel))
							.Cast<BlameItemViewModel>().ToArray();
						Assert.Equal(2, headers.Length);
						Assert.Equal(f1Sha, headers[0].RevisionSha.ToString());
						Assert.Equal(headSha, headers[1].RevisionSha.ToString());
						ScreenshotHelper.Snap(dialog, "01-blame-multicommit", ModuleDir);

						// —— 修订导航 + Undo/Redo 栈生命周期 ——
						Assert.False(dialog.UndoButton.IsEnabled, "单栈项 undo 应禁用");
						dialog.RevisionsComboBox.SelectedItem = revisions[1]; // 切到 f1 → RefreshBlame 二屏
						Assert.True(UiClick.WaitFor(delegate { return dialog.UndoButton.IsEnabled; }),
							"切换修订后 undo 应启用（15s 超时，DelayedAction 防抖 + blame Task）");
						Assert.Equal(f1Sha, ((RevisionViewModel)dialog.RevisionsComboBox.SelectedItem).Sha.ToString());
						ScreenshotHelper.Snap(dialog, "02-blame-first-commit", ModuleDir);

						UiClick.Click(dialog.UndoButton); // 回栈底 HEAD
						Assert.True(UiClick.WaitFor(delegate
						{
							return dialog.RevisionsComboBox.SelectedItem is RevisionViewModel vm && vm.Sha.ToString() == headSha;
						}), "undo 应回到 HEAD 修订（15s 超时）");
						Assert.False(dialog.UndoButton.IsEnabled, "栈底 undo 应禁用");
						Assert.True(dialog.RedoButton.IsEnabled, "栈底 redo 应启用");

						UiClick.Click(dialog.RedoButton); // 再进到 f1
						Assert.True(UiClick.WaitFor(delegate
						{
							return dialog.RevisionsComboBox.SelectedItem is RevisionViewModel vm && vm.Sha.ToString() == f1Sha;
						}), "redo 应回到 f1 修订（15s 超时）");
						dialog.Close();
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

		// ============================ 2) 文件历史 File 模式 ============================

		[Fact]
		public void FileHistoryWindow_FileMode_HistoryListAndDiff()
		{
			// feature 活跃基底：f1.txt 的创建提交（feat: one）在 feature 祖先链上——
			// main 活跃时 git log main -- f1.txt 只有追加提交 1 条（f1 创建提交不在 main 祖先）
			string repo = TestRepoFactory.CreateHistoryRewrite(checkoutFeature: true);
			try
			{
				// feature 追加 f1.txt 修改提交：文件历史 2 条
				AppendCommit(repo, "f1.txt", "f1\nf1 more\n", "f1second");
				string headSha = GitOf(repo, "rev-parse HEAD");
				string f1Sha = GitOf(repo, "rev-parse feature~3"); // feature: f1→f2→f3→f1second
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						var dialog = new FileHistoryWindow(repoControl,
							new ShowFileHistoryWindowCommand.Mode.File("f1.txt"), null, null);
						dialog.Show();
						// LoadHistory（原 OnInitialized 主体，修复后由构造器尾部调用）：
						// GetFileHistory → TreeView + RevisionTimeLine → 首项自动选中
						Assert.True(UiClick.WaitFor(delegate { return dialog.TreeView.RootItem.Children.Count == 2; }),
							"文件历史应装配 2 条（15s 超时）");

						var entries = dialog.TreeView.RootItem.Children.Cast<HistoryEntryViewModel>().ToArray();
						Assert.Equal(headSha, entries[0].Sha.ToString()); // 新→旧
						Assert.Equal("f1second", entries[0].RevisionSubject);
						Assert.Equal(f1Sha, entries[1].Sha.ToString());
						Assert.Equal("feat: one", entries[1].RevisionSubject);
						Assert.Equal(2, dialog.RevisionTimeLine.Revisions.Length);

						// 首项自动选中（LoadHistory 尾部 GetFirstItemToSelect）→ 300ms 防抖 →
						// RefreshDiff → JobQueue(GetRevisionFileChanges) → FileDiffControl.Content
						Assert.NotNull(dialog.TreeView.SelectedItem);
						Assert.True(UiClick.WaitFor(delegate
						{
							return dialog.FileDiffControl.Content is GitCommandResult<DiffContent>;
						}), "选中历史应加载 diff（15s 超时）");
						var diffResult = (GitCommandResult<DiffContent>)dialog.FileDiffControl.Content;
						Assert.True(diffResult.Succeeded, "diff 应成功: " + (diffResult.Error?.FriendlyDescription ?? ""));
						ScreenshotHelper.Snap(dialog, "03-filehistory-file", ModuleDir);

						// Escape 关闭（OnKeyDown）
						PressKey(dialog, Key.Escape);
						Assert.False(dialog.IsVisible);
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

		// ============================ 3) 文件历史 Directory 模式 ============================

		[Fact]
		public void FileHistoryWindow_DirectoryMode_FolderTree()
		{
			string repo = TestRepoFactory.CreateHistoryRewrite();
			try
			{
				// main 追加子目录两文件提交：Directory 模式的文件夹树
				Directory.CreateDirectory(Path.Combine(repo, "src"));
				File.WriteAllText(Path.Combine(repo, "src", "a.txt"), "a\n");
				File.WriteAllText(Path.Combine(repo, "src", "b.txt"), "b\n");
				TestRepoFactory.GitOutput(repo, "add src");
				TestRepoFactory.GitOutput(repo, "commit -q -m addsrc");
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						var dialog = new FileHistoryWindow(repoControl,
							new ShowFileHistoryWindowCommand.Mode.Directory("src"), null, null);
						dialog.Show();
						Assert.True(UiClick.WaitFor(delegate { return dialog.TreeView.RootItem.Children.Count == 1; }),
							"目录历史应装配 1 条文件夹条目（15s 超时）");

						// 文件夹条目 + 子文件条目（ChangedFiles → SubItemFileHistoryEntryViewModel）
						var folder = Assert.IsType<FolderHistoryEntryViewModel>(dialog.TreeView.RootItem.Children[0]);
						Assert.Equal("addsrc", folder.RevisionSubject);
						Assert.Equal(2, folder.Children.Count); // a.txt + b.txt 子项
						Assert.True(folder.IsExpanded, "Directory 模式应展开全部（ExpandAllChildren）");
						var subPaths = folder.Children.Cast<SubItemFileHistoryEntryViewModel>()
							.Select(x => x.ChangedFile.Path).OrderBy(p => p).ToArray();
						Assert.Equal(new[] { "src/a.txt", "src/b.txt" }, subPaths);
						ScreenshotHelper.Snap(dialog, "04-filehistory-directory", ModuleDir);
						dialog.Close();
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

		// ============================ 4) 仓库概览窗口 ============================

		[Fact]
		public void RepositoryOverviewWindow_TreemapLoadAndSelection()
		{
			string repo = TestRepoFactory.CreateHistoryRewrite();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						var dialog = new RepositoryOverviewWindow(repoControl, repoControl.GitModule);
						dialog.Show();
						// 数据链（探针⑤）：JobQueue → GetRepositoryOverviewDataGitCommand → Post →
						// DateRange 赋值 → RefreshData → Treemap.DataSource + FirstVisualItem 选中。
						// main 活跃（无 refspec 的 git log 只走当前分支）：树图 = main 的 2 文件
						// （base.txt/b.txt 各 1 提交）。文件按 string.CompareOrdinal 排序：
						// "b.txt"( '.' = 0x2E ) < "base.txt"( 'a' = 0x61 ) → children = [b.txt, base.txt]。
						// FirstVisualItem 沿 MaxItemIndex（严格 > 比较平手保持首个）下行 → [0] = b.txt
						// （两文件各 1 提交平手，取序首——首跑"排序末位"理解有误，实为排序首位）
						Assert.True(UiClick.WaitFor(delegate { return !dialog.Fallback.IsVisible; }),
							"概览数据应加载完成（15s 超时）");
						Dispatcher.UIThread.RunJobs();
						Assert.NotNull(dialog.Treemap.DataSource);
						Assert.NotNull(dialog.Treemap.SelectedIndexPath); // FirstVisualItem（叶子）

						// 初始选中联动：b.txt 的提交 + 作者聚合 + 选中文件名
						Assert.True(UiClick.WaitFor(delegate
						{
							return dialog.CommitsUserControl.RevisionsListBox.ItemsSource != null;
						}), "树图选中应联动提交列表（15s 超时）");
						var commits = dialog.CommitsUserControl.RevisionsListBox.ItemsSource
							as RepositoryOverviewCommitViewModel[];
						Assert.NotNull(commits);
						Assert.Single(commits); // b.txt 仅 base two 提交过
						Assert.True(UiClick.WaitFor(delegate
						{
							return dialog.AuthorsUserControl.AuthorsListBox.ItemsSource != null;
						}), "树图选中应联动作者列表（15s 超时）");
						var authors = dialog.AuthorsUserControl.AuthorsListBox.ItemsSource
							as RepositoryOverviewAuthorViewModel[];
						Assert.NotNull(authors);
						Assert.Single(authors);
						Assert.Equal("Test", authors[0].AuthorName);
						Assert.Equal("b.txt", dialog.SelectedFileNameTextBlock.Text);
						ScreenshotHelper.Snap(dialog, "05-repository-overview", ModuleDir);

						// 主动切换树图选中（SelectedIndexPath setter 无条件触发 SelectionChanged）：
						// IndexPath 是自根向下的完整路径（GetPath 逐级索引：i=0 进根 children、i=1 取叶子），
						// 根数组只有仓库文件夹 1 项 → 叶子 = [0, k]。初始 FirstVisualItem = [0,0] = b.txt，
						// 切到 [0,1] = base.txt → 联动刷新提交/文件名（单元素 [1] 会越根数组界被
						// SelectionChanged 的 try/catch 吞掉，文件名不更新——首跑实证）
						var indexPath = new Treemap.IndexPath();
						indexPath.Add(0); // 根：仓库文件夹项（Items 唯一元素）
						indexPath.Add(1); // 根 children[1] = base.txt（ordinal 序）
						dialog.Treemap.SelectedIndexPath = indexPath;
						Assert.True(UiClick.WaitFor(delegate
						{
							return dialog.SelectedFileNameTextBlock.Text == "base.txt";
						}), "切换选中应联动文件名（15s 超时）");
						Dispatcher.UIThread.RunJobs();
						var baseCommits = dialog.CommitsUserControl.RevisionsListBox.ItemsSource
							as RepositoryOverviewCommitViewModel[];
						Assert.NotNull(baseCommits);
						Assert.Single(baseCommits); // base.txt 仅 base one 提交过
						dialog.Close();
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

		// ============================ 5) 仓库统计窗口 ============================

		[Fact]
		public void RepositoryStatisticsWindow_AuthorStatsAndPlots()
		{
			// GetRepositoryStatsGitCommand 硬门槛：解析出 >2 个修订才出统计（<=2 → GenericError
			// "no changes" → Fallback "Unable to generate statistics"，StatsContainer 永不 Show）。
			// git log 无 refspec 只走当前分支：main 活跃仅 2 提交（base one/two）必踩门槛 →
			// checkoutFeature（base one + feat:one/two/three = 4 提交，全部作者 Test）。
			string repo = TestRepoFactory.CreateHistoryRewrite(checkoutFeature: true);
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						var dialog = new RepositoryStatisticsWindow(repoControl.GitModule);
						dialog.Show();
						// Loaded（探针⑥）→ ShowStatistics → GetRepositoryStatsGitCommand（后台 Task）
						// → Post → StatsContainer.Show + UpdatePlots（AuthorStatListBox + 四图 + heatmap）
						Assert.True(UiClick.WaitFor(delegate
						{
							return dialog.GetVisualDescendants().OfType<StatisticsUserControl>()
								.FirstOrDefault()?.StatsContainer.IsVisible == true;
						}), "统计应装配完成（15s 超时）");
						StatisticsUserControl stats = dialog.GetVisualDescendants().OfType<StatisticsUserControl>().First();

						// —— 作者聚合列表（AuthorStatListBox）——
						var authors = stats.AuthorStatListBox.ItemsSource as StatisticsUserControl.AuthorStatViewModel[];
						Assert.NotNull(authors);
						Assert.Single(authors);
						Assert.Equal("Test", authors[0].Name); // Init 的 user.name
						Assert.Equal(4, authors[0].TotalCommits); // feature 活跃分支 4 提交

						// —— 四图 + heatmap（UpdatePlots 全装配，OxyPlot Series/Slices/Items 状态断言）——
						// 线图：单作者 LineSeries（Title=作者名，ItemsSource=逐月提交数）
						var lineSeries = Assert.IsType<OxyPlot.Series.LineSeries>(
							Assert.Single(stats.LinePlot.Model.Series));
						Assert.Equal("Test", lineSeries.Title);
						// 饼图：单切片，值 = 总提交数
						var pieSeries = Assert.IsType<OxyPlot.Series.PieSeries>(
							Assert.Single(stats.PiePlot.Model.Series));
						var slice = Assert.Single(pieSeries.Slices);
						Assert.Equal("Test", slice.Label);
						Assert.Equal(4, slice.Value);
						// 周几柱图：恒 7 项（DaysOfWeek 全天占位，零提交天 = 0 值项），总值 = 4
						var weekDayBars = Assert.IsType<OxyPlot.Series.BarSeries>(
							Assert.Single(stats.WeekDayPlot.Model.Series));
						Assert.Equal(7, weekDayBars.Items.Count);
						Assert.Equal(4, weekDayBars.Items.Sum(i => i.Value));
						// 小时柱图：恒 24 项（0-23 全时段占位），总值 = 4
						var dayHourBars = Assert.IsType<OxyPlot.Series.BarSeries>(
							Assert.Single(stats.DayHourPlot.Model.Series));
						Assert.Equal(24, dayHourBars.Items.Count);
						Assert.Equal(4, dayHourBars.Items.Sum(i => i.Value));
						// 贡献热图：按日聚合，4 提交（工厂毫秒级连发，通常同日单键 4 提交）
						Assert.NotNull(stats.Heatmap.CommitsByDate);
						Assert.Equal(4, stats.Heatmap.CommitsByDate.Values.Sum(v => v.Commits));
						ScreenshotHelper.Snap(dialog, "06-repository-statistics", ModuleDir);
						dialog.Close();
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

		// ============================ 6) 修订详情独立窗 ============================

		[Fact]
		public void RevisionDetailsWindow_DetachedTitleAndEscapeClose()
		{
			string repo = TestRepoFactory.CreateHistoryRewrite();
			try
			{
				string f3Sha = GitOf(repo, "rev-parse feature");
				Assert.True(Sha.TryParse(f3Sha, out Sha parsed), "sha 应可解析");
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						var dialog = new RevisionDetailsWindow(repoControl, repoControl.GitModule,
							new RevisionDiffTarget.Revision(parsed), "f3.txt");
						dialog.Show();
						// RevisionDetails.Loaded（探针⑥）→ ShowRevisionDetails → RevisionDetailsUpdated
						// → RefreshTitle："sha7 subject"
						Assert.True(UiClick.WaitFor(delegate
						{
							return dialog.Title.Contains("feat: three", StringComparison.Ordinal);
						}), "标题应含修订 subject（15s 超时）");
						Assert.Equal(parsed.ToAbbreviatedString() + " feat: three", dialog.Title);
						ScreenshotHelper.Snap(dialog, "07-revision-details-window", ModuleDir);

						// Escape 关闭（OnKeyDown）
						PressKey(dialog, Key.Escape);
						Assert.False(dialog.IsVisible);
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

		// ============================ 7) GoToLine 窗口 ============================

		[Fact]
		public void GoToLineWindow_LineParsingFlow()
		{
			string repo = TestRepoFactory.CreateBasic();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// 空输入 → int.TryParse("") 失败 → null（OnSubmit 无条件 CloseWithOk）
						var empty = new GoToLineWindow();
						empty.Show();
						Dispatcher.UIThread.RunJobs();
						ScreenshotHelper.Snap(empty, "08-gotoline", ModuleDir);
						UiClick.Click(FooterOf(empty).SubmitButton);
						Assert.True(UiClick.WaitFor(delegate { return !empty.IsVisible; }),
							"GoToLine 提交后应关闭");
						Assert.Null(empty.LineNumber);

						// 有效行号 5 → LineNumber=5（BlameWindow Ctrl+G 后 ScrollToLine 的输入契约）
						var valid = new GoToLineWindow();
						valid.Show();
						Dispatcher.UIThread.RunJobs();
						valid.LineNumberTextBox.Text = "5";
						Dispatcher.UIThread.RunJobs();
						UiClick.Click(FooterOf(valid).SubmitButton);
						Assert.True(UiClick.WaitFor(delegate { return !valid.IsVisible; }),
							"GoToLine 提交后应关闭");
						Assert.Equal(5, valid.LineNumber);

						// 非数字 → null（解析失败但窗口照常关闭）
						var invalid = new GoToLineWindow();
						invalid.Show();
						Dispatcher.UIThread.RunJobs();
						invalid.LineNumberTextBox.Text = "abc";
						Dispatcher.UIThread.RunJobs();
						UiClick.Click(FooterOf(invalid).SubmitButton);
						Assert.True(UiClick.WaitFor(delegate { return !invalid.IsVisible; }),
							"GoToLine 提交后应关闭");
						Assert.Null(invalid.LineNumber);

						// 边界 0 → 0（合法行号，第一行）
						var zero = new GoToLineWindow();
						zero.Show();
						Dispatcher.UIThread.RunJobs();
						zero.LineNumberTextBox.Text = "0";
						Dispatcher.UIThread.RunJobs();
						UiClick.Click(FooterOf(zero).SubmitButton);
						Assert.True(UiClick.WaitFor(delegate { return !zero.IsVisible; }),
							"GoToLine 提交后应关闭");
						Assert.Equal(0, zero.LineNumber);
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
