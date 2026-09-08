// v3.12 回归测试（启动后底部 tab/右键菜单状态错乱，2026-09-02）：
// 根因：RevisionsDataSource.IList.IndexOf 恒返回 -1（违反 IList 契约）。Avalonia
// SelectionModel 的 item→index 解析（SelectedItems.Add / SelectedItem / ScrollIntoView
// 等）全部依赖 IndexOf：返回 -1 时 SelectedItems.Add(item) 产生无法解析索引的
// "孤儿选中项"，后续按索引的选中（SelectedIndex / ApplyContainerSelection 的
// container.IsSelected=true）再叠加一次 → 同一 revision 在 SelectedItems 中出现两次
// → 消费端 RevisionListViewUserControl_SelectionChanged 里 SingleItem()==null、
// Length==2 → 误判 Range（双选）→ 底部"提交/文件树"tab 变灰、右键菜单变多选菜单，
// 直到用户点其他行再点回首行才恢复（重新走 Select 时 Contains 命中、不再叠加）。
//
// 修复分两层，本文件各对应一个测试：
//   1) RevisionsDataSource_IListIndexOf_ResolvesMaterializedRows——IndexOf/Contains
//      按 DecoratedRevision.Row + 引用校验做 O(1) 精确解析（根治孤儿项）；
//   2) RevisionList_StartupSelectRowZero_SelectedItemsHasNoDuplicate——走生产启动
//      选中路径 NoUIAutomationListView.Select(0)，断言 SelectedItems 无重复。
//      （另一层防御：RevisionListViewUserControl.NotifySelectionChangedFromCurrentItems
//       按引用去重，属 UserControl 级，需 RepositoryUserControl 依赖，暂不单测。）
using System;
using System.Collections;
using Avalonia.Controls;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Jobs;
using ForkPlus.UI;
using ForkPlus.UI.Controls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class RevisionGraphSelectionTests
	{
		// 三个线性提交（新→旧）：row0=A(parent B)、row1=B(parent C)、row2=C(根提交)。
		// RevisionStorage(shas, parents, parentsIndexes)：parentsIndexes[i] 为第 i 个
		// 提交的 parents 在 parents 数组中的起始偏移，末项用 parents.Length 收尾——
		// A 的 parents=[B]（偏移 0..1），B 的 parents=[C]（偏移 1..2），C 无 parent。
		private static RevisionsDataSource CreateThreeCommitDataSource()
		{
			Sha shaA = Sha.Parse("1111111111111111111111111111111111111111").Value;
			Sha shaB = Sha.Parse("2222222222222222222222222222222222222222").Value;
			Sha shaC = Sha.Parse("3333333333333333333333333333333333333333").Value;
			var storage = new RevisionStorage(
				new[] { shaA, shaB, shaC },
				new[] { shaB, shaC },
				new[] { 0, 1, 2 },
				hasMore: false, timestamp: 0L);
			var dataSource = new RevisionsDataSource();
			// gitModule 传 null：仅触碰 row>0（如 ListBox 物化容器）时会入队 revision
			// header 后台加载任务，其内 GetRevisionHeadersGitCommand.Execute 对 null 模块
			// 的 NRE 由 BtRequest.Run 兜底成失败结果（仅 Log.Error），无进程级副作用。
			dataSource.Reload(
				new JobQueue(), storage,
				RepositoryStashes.Empty, RepositoryReferences.Empty, RepositoryRemotes.Empty,
				RepositoryWorktrees.Empty,
				showStashesInRevisionList: false, reflog: false,
				CollapseState.Empty, UserColors.Empty,
				gitModule: null);
			return dataSource;
		}

		[Fact]
		public void RevisionsDataSource_IListIndexOf_ResolvesMaterializedRows()
		{
			var dataSource = CreateThreeCommitDataSource();
			IList list = (IList)dataSource;

			// 取 row0 会按页（页大小 100）物化全部 3 行；row>0 的访问会入队无害的
			// header 后台任务（见 CreateThreeCommitDataSource 注释）
			DecoratedRevision rev0 = dataSource.GetDecoratedRevisionAtRow(0);
			DecoratedRevision rev1 = (DecoratedRevision)list[1];
			DecoratedRevision rev2 = (DecoratedRevision)list[2];
			Assert.Equal(1, rev1.Row);
			Assert.Equal(2, rev2.Row);

			// 修复前恒 -1：SelectedItems.Add 产生孤儿项 → 启动首行选中后 tab 灰
			Assert.Equal(0, list.IndexOf(rev0));
			Assert.Equal(1, list.IndexOf(rev1));
			Assert.Equal(2, list.IndexOf(rev2));

			// Contains 与 IndexOf 语义对齐（修复前恒 false → Select 的去重守卫失效）
			Assert.True(list.Contains(rev0));
			Assert.True(list.Contains(rev2));
			Assert.False(list.Contains(new object()));

			// 跨实例安全：别的数据源实例（如 Reload 后的旧实例）的项不能误命中
			var otherSource = CreateThreeCommitDataSource();
			DecoratedRevision otherRev0 = otherSource.GetDecoratedRevisionAtRow(0);
			Assert.Equal(-1, list.IndexOf(otherRev0));
		}

		[Fact]
		public void RevisionList_StartupSelectRowZero_SelectedItemsHasNoDuplicate()
		{
			// 与 UiSmokeHeadlessTests 相同的异常捕获模式：堆栈完整带回断言处
			Exception failure = HeadlessAppBootstrap.Run(delegate
			{
				Exception ex = null;
				try
				{
					var dataSource = CreateThreeCommitDataSource();
					// 与 RevisionListViewUserControl.axaml 一致：SelectionMode="Multiple"
					// （DragAndDropListView : NoUIAutomationListView，选中逻辑全在后者）
					var listBox = new NoUIAutomationListView { SelectionMode = SelectionMode.Multiple };
					listBox.ItemsSource = dataSource;
					var window = new Window { Width = 800, Height = 300, Content = listBox };
					window.Show();
					Dispatcher.UIThread.RunJobs();

					// 复刻生产启动自动选中首行的路径（RepositoryContent →
					// RevisionListViewUserControl.Select → NoUIAutomationListView.Select）。
					// None 跳过 ScrollIntoView/Focus：headless 无 MainWindow.Instance，
					// 且二者与选中状态无关。Select 内部顺序：SelectedItems.Clear() →
					// Add(item)（IndexOf 解析索引）→ SelectedIndex=0 → SelectedItem=item →
					// ApplyContainerSelection（container.IsSelected=true 按索引再选中）。
					listBox.Select(0, NoUIAutomationListView.SelectOptions.None);
					Dispatcher.UIThread.RunJobs();

					// 修复前同一 revision 在 SelectedItems 中出现两次（孤儿项 + 按索引
					// 选中叠加）→ 消费端 SingleItem()==null → 误判 Range → tab 灰
					Assert.NotNull(listBox.SelectedItems);
					Assert.True(listBox.SelectedItems.Count == 1,
						"SelectedItems 应恰好 1 项，实际 " + listBox.SelectedItems.Count +
						" 项（出现重复=被误判双选 Range，底部\"提交/文件树\"tab 变灰）");
					Assert.Same(dataSource.GetDecoratedRevisionAtRow(0), listBox.SelectedItems[0]);
					Assert.Equal(0, listBox.SelectedIndex);
					// 消费端等价断言：NotifySelectionChangedFromCurrentItems 从 SelectedItems
					// 构造数组后 SingleItem() 必须命中（走 Revision 单选分支而非 Range）
					var selected = new DecoratedRevision[listBox.SelectedItems.Count];
					for (int i = 0; i < listBox.SelectedItems.Count; i++)
					{
						selected[i] = (DecoratedRevision)listBox.SelectedItems[i];
					}
					Assert.NotNull(selected.SingleItem());

					window.Close();
				}
				catch (Exception e)
				{
					ex = e;
				}
				return ex;
			});
			Assert.True(failure == null, "启动选中回归堆栈：\n" + failure);
		}
	}
}
