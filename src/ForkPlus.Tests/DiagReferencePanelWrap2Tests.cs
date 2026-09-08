// 诊断（修复2 续）：真机 REFS 徽章不换行。逐层加容器复刻真实层级，找出
// 哪一层把宽度约束变成无限宽。层级：RevisionSummaryUserControl 的
// Grid > ScrollViewer(H=Disabled) > Grid > rowGrid > ReferencePanel(Multiline)。
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.UI.Controls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class DiagReferencePanelWrap2Tests
	{
		private static List<Reference> MakeRefs(int count)
		{
			var list = new List<Reference>();
			for (int i = 0; i < count; i++)
			{
				list.Add(new LocalBranch(Sha.Zero, "refs/heads/feature-branch-with-long-name-" + i,
					"feature-branch-with-long-name-" + i, i == 0, null, DateTime.Now));
			}
			return list;
		}

		[Fact]
		public void ReferencePanel_InsideRealHierarchy_ShouldWrap()
		{
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window { Width = 1400, Height = 900 };

				// === 完整复刻 RevisionSummaryUserControl 层级 ===
				var summaryRoot = new Grid();
				summaryRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
				summaryRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

				var scrollViewer = new ScrollViewer
				{
					VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
					HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
				};
				Grid.SetRow(scrollViewer, 0);

				var innerGrid = new Grid();
				innerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
				innerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
				innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
				innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
				innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
				innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

				var rowGrid = new Grid();
				rowGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
				rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
				rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
				Grid.SetRow(rowGrid, 1);
				Grid.SetColumnSpan(rowGrid, 4);

				var refsLabel = new TextBlock { Text = "REFS" };
				Grid.SetRow(refsLabel, 0);
				rowGrid.Children.Add(refsLabel);

				var panel = new ReferencePanel { Margin = new Thickness(16, 0, 0, 0) };
				object themeRes = null;
				global::Avalonia.Application.Current!.TryFindResource("MultilineReferencePanelStyle", out themeRes);
				panel.Theme = themeRes as global::Avalonia.Styling.ControlTheme;
				Grid.SetRow(panel, 0);
				Grid.SetColumn(panel, 1);
				rowGrid.Children.Add(panel);

				innerGrid.Children.Add(rowGrid);
				scrollViewer.Content = innerGrid;
				summaryRoot.Children.Add(scrollViewer);

				// === 外层复刻：RepositoryContentUserControl RevisionView Grid.Row=2 ColumnSpan=3 ===
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

				// === 最外层复刻 RepositoryUserControl：Grid Column0(400-600) | splitter | Column2(*) ===
				var repoGrid = new Grid();
				repoGrid.ColumnDefinitions.Add(new ColumnDefinition { MinWidth = 120, MaxWidth = 600 });
				repoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
				repoGrid.ColumnDefinitions.Add(new ColumnDefinition());
				Grid.SetColumn(outerGrid, 2);
				repoGrid.Children.Add(outerGrid);

				window.Content = repoGrid;
				window.Show();
				Dispatcher.UIThread.RunJobs();

				panel.Refresh(MakeRefs(12), new Remote[0]);
				Dispatcher.UIThread.RunJobs();
				Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window);

				var presenter = panel.GetVisualDescendants().OfType<ItemsPresenter>().FirstOrDefault();
				var wrapHost = presenter?.GetVisualDescendants().OfType<Panel>().FirstOrDefault();
				var badges = wrapHost?.Children ?? new global::Avalonia.Controls.Controls();

				double maxRight = 0;
				foreach (var c in badges) { maxRight = Math.Max(maxRight, c.Bounds.Right); }

				string diag = $"window={window.Bounds.Width:F0} panel.Bounds={panel.Bounds} wrapHostType={wrapHost?.GetType().Name} wrapHost.Bounds={wrapHost?.Bounds} badges={badges.Count} maxRight={maxRight:F0} wrapped={(panel.Bounds.Height > 30)}";
				Console.WriteLine("[diag-hierarchy] " + diag);

				Assert.True(panel.Bounds.Height > 30, "徽章应换行成多行（高度>30），实际单行。" + diag);
				Assert.True(maxRight <= panel.Bounds.Right + 2, "徽章右边界超出面板宽度（被裁剪）。" + diag);

				window.Close();
			});
		}
	}
}
