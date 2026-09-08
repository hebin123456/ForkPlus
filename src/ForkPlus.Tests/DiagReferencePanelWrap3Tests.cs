// 诊断（修复2 续3）：统一挂 MultilineReferencePanelStyle 主题，断言换行（高度），
// 逐层定位丢失宽度约束的层级。
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.UI.Controls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class DiagReferencePanelWrap3Tests
	{
		private static void ApplyTheme(ReferencePanel panel)
		{
			object themeRes = null;
			global::Avalonia.Application.Current!.TryFindResource("MultilineReferencePanelStyle", out themeRes);
			panel.Theme = themeRes as global::Avalonia.Styling.ControlTheme;
		}

		private static (double w, double h) RunCase(string tag, Func<Window, ReferencePanel> build)
		{
			return HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window { Width = 1400, Height = 900 };
				var panel = build(window);
				ApplyTheme(panel);
				window.Show();
				Dispatcher.UIThread.RunJobs();
				var list = new List<Reference>();
				for (int i = 0; i < 12; i++)
				{
					list.Add(new LocalBranch(Sha.Zero, "refs/heads/feature-branch-with-long-name-" + i,
						"feature-branch-with-long-name-" + i, i == 0, null, DateTime.Now));
				}
				panel.Refresh(list, new Remote[0]);
				Dispatcher.UIThread.RunJobs();
				Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window);
				double w = panel.Bounds.Width;
				double h = panel.Bounds.Height;
				Console.WriteLine($"[diag-layer-{tag}] panelW={w:F0} panelH={h:F0} wrapped={(h > 30)}");
				window.Close();
				return (w, h);
			});
		}

		// 层0：window > rowGrid(82|*) > panel —— 已知正常换行（Wrap1Tests 验证过）
		[Fact]
		public void Layer0_DirectGrid()
		{
			var (w, h) = RunCase("0", delegate(Window window)
			{
				var rowGrid = new Grid();
				rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
				rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
				var panel = new ReferencePanel();
				Grid.SetColumn(panel, 1);
				rowGrid.Children.Add(panel);
				window.Content = rowGrid;
				return panel;
			});
			Assert.True(h > 30, $"层0 徽章应换行。w={w} h={h}");
		}

		// 层1：window > innerGrid(4列2行, rowGrid 在 Row1 ColSpan4) > rowGrid > panel
		[Fact]
		public void Layer1_InnerGrid()
		{
			var (w, h) = RunCase("1", delegate(Window window)
			{
				var innerGrid = new Grid();
				innerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
				innerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
				innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
				innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
				innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
				innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

				var rowGrid = new Grid();
				rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
				rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
				var panel = new ReferencePanel();
				Grid.SetColumn(panel, 1);
				rowGrid.Children.Add(panel);
				Grid.SetRow(rowGrid, 1);
				Grid.SetColumnSpan(rowGrid, 4);
				innerGrid.Children.Add(rowGrid);
				window.Content = innerGrid;
				return panel;
			});
			Assert.True(h > 30, $"层1 徽章应换行。w={w} h={h}");
		}

		// 层2：window > scrollViewer(H=Disabled) > innerGrid > rowGrid > panel
		[Fact]
		public void Layer2_InnerGridInsideScrollViewer()
		{
			var (w, h) = RunCase("2", delegate(Window window)
			{
				var innerGrid = new Grid();
				innerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
				innerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
				innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
				innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
				innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
				innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

				var rowGrid = new Grid();
				rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
				rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
				var panel = new ReferencePanel();
				Grid.SetColumn(panel, 1);
				rowGrid.Children.Add(panel);
				Grid.SetRow(rowGrid, 1);
				Grid.SetColumnSpan(rowGrid, 4);
				innerGrid.Children.Add(rowGrid);

				var scrollViewer = new ScrollViewer
				{
					HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
					Content = innerGrid
				};
				window.Content = scrollViewer;
				return panel;
			});
			Assert.True(h > 30, $"层2 徽章应换行。w={w} h={h}");
		}

		// 层3：window > summaryRoot(2行 */Auto) > scrollViewer(Row0) > innerGrid > rowGrid > panel
		[Fact]
		public void Layer3_SummaryRootWithStarRow()
		{
			var (w, h) = RunCase("3", delegate(Window window)
			{
				var innerGrid = new Grid();
				innerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
				innerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
				innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
				innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
				innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
				innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

				var rowGrid = new Grid();
				rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
				rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
				var panel = new ReferencePanel();
				Grid.SetColumn(panel, 1);
				rowGrid.Children.Add(panel);
				Grid.SetRow(rowGrid, 1);
				Grid.SetColumnSpan(rowGrid, 4);
				innerGrid.Children.Add(rowGrid);

				var scrollViewer = new ScrollViewer
				{
					HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
					Content = innerGrid
				};
				var summaryRoot = new Grid();
				summaryRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
				summaryRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
				Grid.SetRow(scrollViewer, 0);
				summaryRoot.Children.Add(scrollViewer);
				window.Content = summaryRoot;
				return panel;
			});
			Assert.True(h > 30, $"层3 徽章应换行。w={w} h={h}");
		}

		// 层4：window > outerGrid(3行3列, summaryRoot 在 Row2 ColSpan3) > ...
		[Fact]
		public void Layer4_OuterGrid()
		{
			var (w, h) = RunCase("4", delegate(Window window)
			{
				var innerGrid = new Grid();
				innerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
				innerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
				innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
				innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
				innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
				innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

				var rowGrid = new Grid();
				rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
				rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
				var panel = new ReferencePanel();
				Grid.SetColumn(panel, 1);
				rowGrid.Children.Add(panel);
				Grid.SetRow(rowGrid, 1);
				Grid.SetColumnSpan(rowGrid, 4);
				innerGrid.Children.Add(rowGrid);

				var scrollViewer = new ScrollViewer
				{
					HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
					Content = innerGrid
				};
				var summaryRoot = new Grid();
				summaryRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
				summaryRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
				Grid.SetRow(scrollViewer, 0);
				summaryRoot.Children.Add(scrollViewer);

				var outerGrid = new Grid();
				outerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
				outerGrid.RowDefinitions.Add(new RowDefinition { MinHeight = 110 });
				outerGrid.RowDefinitions.Add(new RowDefinition { MinHeight = 110 });
				outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(400), MinWidth = 300 });
				outerGrid.ColumnDefinitions.Add(new ColumnDefinition());
				outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
				Grid.SetRow(summaryRoot, 2);
				Grid.SetColumnSpan(summaryRoot, 3);
				outerGrid.Children.Add(summaryRoot);

				window.Content = outerGrid;
				return panel;
			});
			Assert.True(h > 30, $"层4 徽章应换行。w={w} h={h}");
		}

		// 层5：完整链（含 repoGrid 三列）
		[Fact]
		public void Layer5_FullChain()
		{
			var (w, h) = RunCase("5", delegate(Window window)
			{
				var innerGrid = new Grid();
				innerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
				innerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
				innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
				innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
				innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
				innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

				var rowGrid = new Grid();
				rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
				rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
				var panel = new ReferencePanel();
				Grid.SetColumn(panel, 1);
				rowGrid.Children.Add(panel);
				Grid.SetRow(rowGrid, 1);
				Grid.SetColumnSpan(rowGrid, 4);
				innerGrid.Children.Add(rowGrid);

				var scrollViewer = new ScrollViewer
				{
					HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
					Content = innerGrid
				};
				var summaryRoot = new Grid();
				summaryRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
				summaryRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
				Grid.SetRow(scrollViewer, 0);
				summaryRoot.Children.Add(scrollViewer);

				var outerGrid = new Grid();
				outerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
				outerGrid.RowDefinitions.Add(new RowDefinition { MinHeight = 110 });
				outerGrid.RowDefinitions.Add(new RowDefinition { MinHeight = 110 });
				outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(400), MinWidth = 300 });
				outerGrid.ColumnDefinitions.Add(new ColumnDefinition());
				outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
				Grid.SetRow(summaryRoot, 2);
				Grid.SetColumnSpan(summaryRoot, 3);
				outerGrid.Children.Add(summaryRoot);

				var repoGrid = new Grid();
				repoGrid.ColumnDefinitions.Add(new ColumnDefinition { MinWidth = 120, MaxWidth = 600 });
				repoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
				repoGrid.ColumnDefinitions.Add(new ColumnDefinition());
				Grid.SetColumn(outerGrid, 2);
				repoGrid.Children.Add(outerGrid);

				window.Content = repoGrid;
				return panel;
			});
			Assert.True(h > 30, $"层5 徽章应换行。w={w} h={h}");
		}
	}
}
