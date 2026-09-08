// 性能回归测试（2026-09-03，轨道树/提交列表性能审计产物）：
// 加载防线——首个防 O(N)：数据源从 5000 增到 20000 行，首帧耗时与实化容器数
// 必须保持 O(1)（虚拟化健康）。审计实测：5000/20000 行首帧均 ~12-14ms（首档
// 452ms 为冷 JIT/主题加载一次性成本）。若本测试随 N 线性恶化 = 隐藏全量实化回归
// （如 ItemsPresenter 虚拟化失效、ItemsSourceView 全量枚举、Extent 探测触发
// 全量装饰等），即用户感知的"内容多界面卡"。
using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
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
	public class RevisionListLoadPerfTests
	{
		private static RevisionsDataSource CreateLinearSource(int n)
		{
			var shas = new Sha[n];
			for (int i = 0; i < n; i++)
			{
				shas[i] = Sha.Parse(i.ToString("x40")).Value;
			}
			var parents = new Sha[n - 1];
			var parentIndexes = new int[n];
			for (int i = 0; i < n - 1; i++)
			{
				parents[i] = shas[i + 1];
				parentIndexes[i] = i;
			}
			parentIndexes[n - 1] = n - 1;
			var storage = new RevisionStorage(shas, parents, parentIndexes, hasMore: false, timestamp: 0L);
			var dataSource = new RevisionsDataSource();
			dataSource.Reload(new JobQueue(), storage,
				RepositoryStashes.Empty, RepositoryReferences.Empty, RepositoryRemotes.Empty,
				RepositoryWorktrees.Empty,
				showStashesInRevisionList: false, reflog: false,
				CollapseState.Empty, UserColors.Empty,
				gitModule: null);
			return dataSource;
		}

		[Fact]
		public void FirstFrame_ScalesConstantly_WithLargeRowCounts()
		{
			HeadlessAppBootstrap.EnsureStarted();
			var task = Dispatcher.UIThread.InvokeAsync(delegate
			{
				// (行数, 首帧耗时ms, 实化容器数)
				var results = new (int N, double Ms, int Containers)[3];
				int slot = 0;
				foreach (int n in new[] { 1000, 5000, 20000 })
				{
					var dataSource = CreateLinearSource(n);
					var lv = new NoUIAutomationListView { SelectionMode = SelectionMode.Multiple };
					var theme = Avalonia.Application.Current!.TryFindResource("ListViewWithGridViewStyle", out object? o) ? o : null;
					if (theme is Avalonia.Styling.ControlTheme ct)
					{
						lv.Theme = ct;
					}
					lv.ItemsSource = dataSource;
					var window = new Window { Width = 1000, Height = 500, Content = lv };
					var sw = Stopwatch.StartNew();
					window.Show();
					Dispatcher.UIThread.RunJobs();
					HeadlessWindowExtensions.CaptureRenderedFrame(window);
					sw.Stop();
					results[slot] = (n, sw.Elapsed.TotalMilliseconds,
						lv.GetVisualDescendants().OfType<ListBoxItem>().Count());
					slot++;
					window.Close();
				}
				return results;
			});
			var results = task.Result;

			// 1) 每档实化容器数都是可见行量级（虚拟化健康）
			foreach (var r in results)
			{
				Assert.True(r.Containers < 100,
					"N=" + r.N + " 实化 " + r.Containers + " 个容器（虚拟化失效，应 ~25）");
			}

			// 2) 首帧耗时不随行数增长（O(1)）：20000 行档不显著慢于 5000 行档。
			//    首档(1000)含冷 JIT 一次性成本，跳过；阈值放 4 倍裕量防 CI 噪音。
			double ms5000 = results[1].Ms;
			double ms20000 = results[2].Ms;
			Assert.True(ms20000 < Math.Max(ms5000 * 4.0, 300.0),
				"首帧耗时随行数线性增长（O(N) 泄漏）：5000 行 " + ms5000.ToString("F0") +
				"ms vs 20000 行 " + ms20000.ToString("F0") + "ms——存在隐藏全量实化/枚举");
		}
	}
}
