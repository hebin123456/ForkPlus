// 性能回归测试（2026-09-03，轨道树/提交列表性能审计产物）：
// 用户报告"大仓库提交列表（轨道树）很卡"，怀疑缺缓存。经与 WPF 原版
// （ForkPlus 仓库）全面对比审计：数据层（GraphInfo/GraphLine/RevisionVisualGraph/
// CommitGraphCache）逐字节相同；WPF 原版亦无渲染缓存（每次 OnRender 重建几何）。
// 真实根因已排查确认（性能审计，2026-09-03）。本测试是审计沉淀的回归防线：
// 列表虚拟化必须保持生效——N 项数据源只允许实化可见行数量级的容器。
// 若本测试失败 = ItemsPanel/ItemsPresenter 虚拟化被破坏（全量实化回归）。
using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI.Controls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class RevisionListVirtualizationPerfTests
	{
		[Fact]
		public void LargeItemsSource_MaterializesOnlyVisibleRows()
		{
			HeadlessAppBootstrap.EnsureStarted();
			var task = Dispatcher.UIThread.InvokeAsync(delegate
			{
				var lv = new NoUIAutomationListView();
				var theme = Avalonia.Application.Current!.TryFindResource("ListViewWithGridViewStyle", out object? o) ? o : null;
				if (theme is Avalonia.Styling.ControlTheme ct)
				{
					lv.Theme = ct;
				}
				lv.ItemsSource = Enumerable.Range(0, 5000).Select(i => "commit-" + i).ToArray();
				lv.Width = 900;
				lv.Height = 400;

				var window = new Window { Width = 1000, Height = 500, Content = lv };
				window.Show();
				Dispatcher.UIThread.RunJobs();
				HeadlessWindowExtensions.CaptureRenderedFrame(window);

				int containers = lv.GetVisualDescendants().OfType<ListBoxItem>().Count();
				window.Close();
				return containers;
			});
			// 5000 项、500px 高窗口：可见行数量级 ~25；上界 100 留足裕量。
			// 失败即虚拟化失效（ItemsPanel 回落非虚拟化 StackPanel → 全量实化）。
			Assert.True(task.Result < 100,
				"提交列表虚拟化失效：5000 项实化了 " + task.Result + " 个容器（应仅可见行 ~25）");
		}
	}
}
