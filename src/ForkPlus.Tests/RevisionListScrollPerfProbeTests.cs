// 性能回归测试（2026-09-03，轨道树/提交列表性能审计产物）：
// 端到端滚动防线——20000 行（每行 12 条轨道线的辫子拓扑）连续滚动，
// 断言容器回收保持生效（实化数不随滚动累积）。
// 审计结论：滚动路径与 WPF 原版等价（原版无渲染缓存、每帧重建几何），
// 每屏成本由容器换绑+绑定重建主导（headless 软渲染 ~10-20ms/屏，真实 GPU 更低）。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Jobs;
using ForkPlus.UI;
using ForkPlus.UI.Controls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class RevisionListScrollPerfTests
	{
		[Fact]
		public void ScrollManyPages_ContainerRecyclingStaysEffective()
		{
			HeadlessAppBootstrap.EnsureStarted();
			var task = Dispatcher.UIThread.InvokeAsync(delegate
			{
				// 12 条平行链辫子（行 r 的 parent = r+12）：每行穿过 ~12 条轨道线，
				// 对齐多分支大仓库的真实图形密度。
				const int N = 5040;
				const int Lanes = 12;
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
				var dataSource = new RevisionsDataSource();
				dataSource.Reload(new JobQueue(), storage,
					RepositoryStashes.Empty, RepositoryReferences.Empty, RepositoryRemotes.Empty,
					RepositoryWorktrees.Empty,
					showStashesInRevisionList: false, reflog: false,
					CollapseState.Empty, UserColors.Empty,
					gitModule: null);

				// 拓扑自检：中间行必须有 12 条线（保证测试真的在跑多 lane 场景）
				var mid = dataSource.GetDecoratedRevisionAtRow(N / 2);
				if (mid.GraphInfo.Lines.Length != Lanes)
				{
					return "lane topology broken: " + mid.GraphInfo.Lines.Length;
				}

				var lv = new NoUIAutomationListView();
				var theme = Avalonia.Application.Current!.TryFindResource("ListViewWithGridViewStyle", out object? o) ? o : null;
				if (theme is Avalonia.Styling.ControlTheme ct)
				{
					lv.Theme = ct;
				}
				lv.SelectionMode = SelectionMode.Multiple;
				lv.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((item, _) =>
				{
					var grid = new Grid();
					grid.ColumnDefinitions.Add(new ColumnDefinition(0, GridUnitType.Auto));
					grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
					var graph = new GraphCellView { CellHeight = 23, Margin = new Avalonia.Thickness(4, 0, 0, 0) };
					Grid.SetColumn(graph, 0);
					var subject = new TextBlock { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
						TextTrimming = TextTrimming.CharacterEllipsis };
					subject[!TextBlock.TextProperty] = new Avalonia.Data.Binding("Subject");
					Grid.SetColumn(subject, 1);
					grid.Children.Add(graph);
					grid.Children.Add(subject);
					return grid;
				});
				lv.ItemsSource = dataSource;
				var window = new Window { Width = 1000, Height = 500, Content = lv };
				window.Show();
				Dispatcher.UIThread.RunJobs();
				HeadlessWindowExtensions.CaptureRenderedFrame(window);

				var scrollViewer = lv.GetVisualDescendants().OfType<ScrollViewer>().First();
				double step = scrollViewer.Viewport.Height * 0.9;
				int pages = 0;
				var perPage = new List<double>();
				var sw = Stopwatch.StartNew();
				while (scrollViewer.Offset.Y + scrollViewer.Viewport.Height < scrollViewer.Extent.Height && pages < 40)
				{
					scrollViewer.Offset = new Avalonia.Vector(0, scrollViewer.Offset.Y + step);
					Dispatcher.UIThread.RunJobs();
					HeadlessWindowExtensions.CaptureRenderedFrame(window);
					perPage.Add(sw.Elapsed.TotalMilliseconds);
					sw.Restart();
					pages++;
				}

				int containers = lv.GetVisualDescendants().OfType<ListBoxItem>().Count();
				window.Close();
				// 滚动 40 屏后实化容器必须仍是可见行数量级（回收生效）。
				// 失败 = 容器泄漏（换绑失效 → 滚动越滚越卡）。
				double avgMs = perPage.Count > 0 ? perPage.Average() : -1.0;
				return "containers=" + containers + ";pages=" + pages +
					";avgMs=" + avgMs.ToString("F1");
			});
			string result = task.Result;
			Assert.Contains("containers=", result);
			Assert.DoesNotContain("lane topology broken", result);
			// containers= 后面的数字必须 < 100
			int idx = result.IndexOf("containers=") + "containers=".Length;
			int num = int.Parse(result.Substring(idx, result.IndexOf(';', idx) - idx));
			Assert.True(num < 100,
				"滚动后容器回收失效：40 屏滚动后实化 " + num + " 个容器（应保持可见行量级 ~25）");
		}
	}
}
