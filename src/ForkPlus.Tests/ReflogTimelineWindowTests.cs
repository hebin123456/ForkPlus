// WS9 ReflogWindow「操作历史时间线」E2E：1 用例。
// 覆盖：真实临时仓库（CreateHistoryRewrite，7 条 reflog）+ 直接向 UndoIndexStore 写入
// 一条 {HeadSha → 友好操作名}（模拟 AddUndoable 持久化路径，避免驱动完整 Undo E2E 的代价）
// → 打开 ReflogWindow（构造即 LoadReflog → BuildViewItems join）断言：
// 1) sha 命中条目带 IsIndexedOperation + 友好操作名；未命中条目降级原始 reflog 消息
// 2) 徽标渲染：命中行显示 Accent 节点（timeline-node-indexed Ellipse 可见、灰节点不可见），
//    未命中行反之（节点 Ellipse 用 Classes 标记，断言与主题色解耦）
// 3) 相对时间列非空且非绝对格式；悬停提示保留原始 reflog 消息
// 4) 图例文案走语言键（Tr 与窗口 ApplyLocalization 同源）
// JumpTo 行为回归由 E2e13 Reflog_ListEntriesAndJumpResetsHead 覆盖，此处不重复。
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.Undo;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class ReflogTimelineWindowTests
	{
		[Fact]
		public void ReflogWindow_IndexedEntry_ShowsFriendlyNameBadgeAndRelativeTime()
		{
			string repo = TestRepoFactory.CreateHistoryRewrite();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// —— 预置：把第 2 新 reflog 条目（checkout main）的 sha 写进 undo-index ——
						List<ReflogEntry> reflog = new ReflogHistoryProvider().ReadHeadReflog(repoControl.GitModule);
						Assert.True(reflog.Count >= 3, "CreateHistoryRewrite 应产生多段 reflog（实际 " + reflog.Count + " 条）");
						ReflogEntry target = reflog[1];
						string friendlyName = "Checkout 'main' (e2e timeline)";
						new UndoIndexStore(repoControl.GitModule).Record(
							new UndoIndexEntry(target.Sha, friendlyName, DateTime.UtcNow, "Checkout"));

						// —— 开窗：构造即 LoadReflog → BuildViewItems left-outer join ——
						var dialog = new ReflogWindow(repoControl);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						Assert.True(UiClick.WaitFor(delegate
						{
							return dialog.ReflogListView.ItemsSource != null
								&& dialog.ReflogListView.ItemsSource.OfType<ReflogViewItem>()
									.Any(delegate (ReflogViewItem i) { return i.IsIndexedOperation; });
						}), "sha 命中 undo-index 的条目应携带友好操作名（15s 超时）");

						List<ReflogViewItem> items = dialog.ReflogListView.ItemsSource.OfType<ReflogViewItem>().ToList();

						// —— 1) 命中条目：IsIndexedOperation + 友好名；未命中条目：降级原始消息 ——
						ReflogViewItem hit = items.First(delegate (ReflogViewItem i) { return i.Sha == target.Sha; });
						Assert.True(hit.IsIndexedOperation, "命中 sha 的条目应标记 IsIndexedOperation");
						Assert.Equal(friendlyName, hit.OperationName);
						Assert.Contains(hit.RawReflogSubject, hit.TooltipText, StringComparison.Ordinal);
						foreach (ReflogViewItem plain in items.Where(delegate (ReflogViewItem i) { return !i.IsIndexedOperation; }))
						{
							Assert.False(plain.IsIndexedOperation);
							Assert.Equal(plain.RawReflogSubject, plain.OperationName);
						}

						// —— 2) 相对时间：工厂提交刚发生 → 相对格式（非空且不等于绝对格式）——
						Assert.NotEqual("", hit.RelativeTimeDisplay);
						Assert.NotEqual(hit.TimeDisplay, hit.RelativeTimeDisplay);

						// —— 3) 徽标渲染：命中行 Accent 节点可见、灰节点隐藏；未命中行反之 ——
						Assert.True(UiClick.WaitFor(delegate
						{
							return (ListBoxItem)dialog.ReflogListView.ContainerFromItem(hit) != null;
						}), "命中行容器应完成虚拟化装配（15s 超时）");
						ListBoxItem hitContainer = (ListBoxItem)dialog.ReflogListView.ContainerFromItem(hit);
						Ellipse hitIndexedNode = hitContainer.GetVisualDescendants().OfType<Ellipse>()
							.FirstOrDefault(delegate (Ellipse e) { return e.Classes.Contains("timeline-node-indexed"); });
						Ellipse hitPlainNode = hitContainer.GetVisualDescendants().OfType<Ellipse>()
							.FirstOrDefault(delegate (Ellipse e) { return e.Classes.Contains("timeline-node-plain"); });
						Assert.NotNull(hitIndexedNode);
						Assert.NotNull(hitPlainNode);
						// 等绑定生效（IsVisible 绑定 IsIndexedOperation），再断言同模板的另一个节点
						Assert.True(UiClick.WaitFor(delegate { return hitIndexedNode.IsVisible; }), "命中行应显示 Accent 节点");
						Assert.False(hitPlainNode.IsVisible, "命中行不应显示灰色节点");

						ReflogViewItem plainItem = items.First(delegate (ReflogViewItem i) { return !i.IsIndexedOperation; });
						Assert.True(UiClick.WaitFor(delegate
						{
							return (ListBoxItem)dialog.ReflogListView.ContainerFromItem(plainItem) != null;
						}), "未命中行容器应完成虚拟化装配（15s 超时）");
						ListBoxItem plainContainer = (ListBoxItem)dialog.ReflogListView.ContainerFromItem(plainItem);
						Ellipse plainPlainNode = plainContainer.GetVisualDescendants().OfType<Ellipse>()
							.FirstOrDefault(delegate (Ellipse e) { return e.Classes.Contains("timeline-node-plain"); });
						Ellipse plainIndexedNode = plainContainer.GetVisualDescendants().OfType<Ellipse>()
							.FirstOrDefault(delegate (Ellipse e) { return e.Classes.Contains("timeline-node-indexed"); });
						Assert.NotNull(plainPlainNode);
						Assert.NotNull(plainIndexedNode);
						Assert.True(UiClick.WaitFor(delegate { return plainPlainNode.IsVisible; }), "未命中行应显示灰色节点");
						Assert.False(plainIndexedNode.IsVisible, "未命中行不应显示 Accent 节点");

					// —— 4) 时间线图例文案走语言键（与 ApplyLocalization 同源的 Tr 断言）——
					Assert.Equal(
						E2eMainWindowHarness.Tr("Accent dots are operations performed in ForkPlus; gray dots are other reflog entries."),
						dialog.TimelineLegendText.Text);

					// —— 5) 表头列与行内容对齐（2026-09-30 修复）——
					// 修复前表头 Grid 无内缩补偿：行内容因 ListBoxItem Padding=6 左右内缩 +
					// 竖滚动条占宽，表头相对行恒偏移（左偏 6px、右多滚动条宽）。
					// 修复后 ReflogListView.LayoutUpdated 按已实现行容器几何校准表头 Margin，
					// 断言表头左缘 = 行容器原点+6、表头右缘 = 行容器右缘-6（0.6px 容差吸收布局取整）。
					Assert.True(UiClick.WaitFor(delegate
					{
						return dialog.HeaderColumns.Margin.Left >= 6.0 && dialog.HeaderColumns.Margin.Right >= 6.0;
					}), "表头 Margin 应完成行几何同步（左/右 ≥ 行内缩 6px）");
					global::Avalonia.Matrix? containerMatrix = hitContainer.TransformToVisual(dialog);
					global::Avalonia.Point containerOrigin = containerMatrix.HasValue ? new global::Avalonia.Point(containerMatrix.Value.M31, containerMatrix.Value.M32) : new global::Avalonia.Point();
					global::Avalonia.Matrix? headerMatrix = dialog.HeaderColumns.TransformToVisual(dialog);
					global::Avalonia.Point headerOrigin = headerMatrix.HasValue ? new global::Avalonia.Point(headerMatrix.Value.M31, headerMatrix.Value.M32) : new global::Avalonia.Point();
					Assert.InRange(headerOrigin.X, containerOrigin.X + 6.0 - 0.6, containerOrigin.X + 6.0 + 0.6);
					double rowContentRight = containerOrigin.X + hitContainer.Bounds.Width - 6.0;
					double headerRight = headerOrigin.X + dialog.HeaderColumns.Bounds.Width;
					Assert.InRange(headerRight, rowContentRight - 0.6, rowContentRight + 0.6);

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
	}
}
