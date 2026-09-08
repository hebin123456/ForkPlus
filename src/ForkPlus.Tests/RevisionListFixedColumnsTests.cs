// 回归测试（2026-09-07，"轨道太多/徽章太多时把作者、SHA 顶到后面去"）：
// 用户报告：所有提交列表里，多 lane 轨道图 + 超长 refs 徽章会把行尾的作者/SHA/日期列
// 整体顶出视口右缘（裁剪不可见）。WPF 原版靠 GridView 两层结构保证固定列永不被顶：
//   第 0 复合列(graph+refs+subject)由 UpdateResizableColumnWidth(0) 钳制在剩余宽度内，
//   avatar/author/sha/date 是独立固定列，恒定贴视口右缘。
// 迁移版曾把固定列做成行 Grid 末尾的 Auto 列——左侧 Auto 列超宽时它们被顶走。
// 修复：单行模板重建 WPF 两层结构（外层 "* ,Auto,Auto,Auto,Auto" + 左组合列 ClipToBounds）。
// 本测试守卫：80-lane 轨道图（968px desired，远超 900px 视口）下，
// 每个实化行的日期/SHA/作者列 Bounds.Right 仍在视口内（被顶出即红），
// 且轨道图确实处于超宽状态（防止测试退化成不触发场景的假绿）。
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Jobs;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class RevisionListFixedColumnsTests
	{
		[Fact]
		public void WideGraphManyLanes_FixedColumnsStayInViewport()
		{
			HeadlessAppBootstrap.EnsureStarted();
			var task = Dispatcher.UIThread.InvokeAsync(delegate
			{
				// 80 条平行链辫子（行 r 的 parent = r+80）：中部行穿过 ~80 条轨道线，
				// GraphCellView desired = 12px * 80 = 960px + 8px margin，远超 900px 视口
				// （修复前：author/sha/date 被顶出右缘；修复后：左组合列裁剪，固定列恒可见）。
				const int N = 400;
				const int Lanes = 80;
				var shas = new Sha[N];
				for (int i = 0; i < N; i++)
				{
					shas[i] = Sha.Parse(i.ToString("x40")).Value;
				}
				var parents = new Sha[N - Lanes];
				var parentIndexes = new int[N];
				for (int i = 0; i < N - Lanes; i++)
				{
					parents[i] = shas[i + Lanes];
					parentIndexes[i] = i;
				}
				for (int i = N - Lanes; i < N; i++)
				{
					parentIndexes[i] = N - Lanes;
				}
				var storage = new RevisionStorage(shas, parents, parentIndexes, hasMore: false, timestamp: 0L);
				var control = new RevisionListViewUserControl();
				control.RevisionsDataSource.Reload(new JobQueue(), storage,
					RepositoryStashes.Empty, RepositoryReferences.Empty, RepositoryRemotes.Empty,
					RepositoryWorktrees.Empty,
					showStashesInRevisionList: false, reflog: false,
					CollapseState.Empty, UserColors.Empty,
					gitModule: null);

				var window = new Window { Width = 900, Height = 400, Content = control };
				window.Show();
				Dispatcher.UIThread.RunJobs();
				HeadlessWindowExtensions.CaptureRenderedFrame(window);

				// 滚到中部（那里是满 80-lane 拓扑；头部行 lane 数还在爬坡）
				ScrollViewer? scroller = control.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
				if (scroller != null)
				{
					scroller.Offset = new Avalonia.Vector(0, scroller.Extent.Height / 2.0);
					Dispatcher.UIThread.RunJobs();
					HeadlessWindowExtensions.CaptureRenderedFrame(window);
				}
				double viewportWidth = scroller != null ? scroller.Viewport.Width : 900.0;

				var results = new System.Collections.Generic.List<string>();
				double widestGraph = 0.0;
				foreach (var container in control.RevisionListView.GetRealizedContainers())
				{
					// Avalonia 语义：ListBoxItem.Content 恒为数据项本身，行模板（Grid）
					// 挂在容器内 ContentPresenter.Child 上。
					if (container is not ContentControl rowHost)
					{
						continue;
					}
					global::Avalonia.Controls.Presenters.ContentPresenter presenter = rowHost.GetVisualDescendants()
						.OfType<global::Avalonia.Controls.Presenters.ContentPresenter>().FirstOrDefault();
					if (presenter?.Child is not Grid rowGrid)
					{
						results.Add("unexpected-template");
						continue;
					}
					// 单行模板结构：[左组合Grid, avatar Border, author Border, sha HighlightableTextBlock, date TextBlock]
					if (rowGrid.ColumnDefinitions.Count != 5)
					{
						results.Add("unexpected-template");
						continue;
					}
					var graphs = rowGrid.GetVisualDescendants().OfType<GraphCellView>().ToList();
					widestGraph = System.Math.Max(widestGraph, graphs.Count > 0 ? graphs[0].Bounds.Width : 0.0);
					// 末三个固定列（author Border 之后）都必须完整落在视口内
					for (int c = 1; c < 5; c++)
					{
						var child = rowGrid.Children.FirstOrDefault(x => Grid.GetColumn(x) == c);
						if (child == null)
						{
							continue;
						}
						if (child.Bounds.Right > viewportWidth + 0.5)
						{
							results.Add("pushed-out-col" + c + ":right=" + child.Bounds.Right.ToString("F0"));
						}
						if (child.Bounds.Width <= 0.0)
						{
							results.Add("zero-width-col" + c);
						}
					}
				}
				window.Close();
				return "widestGraph=" + widestGraph.ToString("F0") +
					";viewport=" + viewportWidth.ToString("F0") +
					";issues=[" + string.Join("|", results) + "]";
			});
			string result = task.Result;
			// 守卫 1：测试场景必须真实触发"轨道图超宽"（否则测试退化成假绿）。
			Assert.Contains("widestGraph=", result);
			int graphIdx = result.IndexOf("widestGraph=") + "widestGraph=".Length;
			double widestGraph = double.Parse(result.Substring(graphIdx, result.IndexOf(';', graphIdx) - graphIdx));
			Assert.True(widestGraph > 600.0,
				"测试场景未触发超宽轨道图，测试无效。诊断: " + result);
			// 守卫 2：任何实化行的固定列都不允许被顶出视口或被压为零宽。
			Assert.DoesNotContain("pushed-out-col", result);
			Assert.DoesNotContain("zero-width-col", result);
			Assert.DoesNotContain("unexpected-template", result);
		}
	}
}
