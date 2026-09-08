// 诊断（修复2）：所有提交页"提交"tab 的 REFS（引用）徽章不换行——原版 WPF 是
// MultilineReferencePanelStyle（ItemsPanel=WrapPanel）按行换行的。
// 本测试复刻 RevisionSummaryUserControl 的 REFS 行布局，注入多枚徽章，
// 断言换行（高度）与不溢出裁切。
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Utilities;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.UI.Controls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class DiagReferencePanelWrapTests
	{
		[Fact]
		public void ReferencePanel_ManyBadges_ShouldWrap()
		{
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				// 复刻 RevisionSummaryUserControl：ScrollViewer(Horizontal=Disabled) > Grid > Row0 REFS
				var window = new Window { Width = 700, Height = 600 };

				var outer = new Grid();
				outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

				var rowGrid = new Grid();
				rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
				rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
				Grid.SetRow(rowGrid, 0);
				outer.Children.Add(rowGrid);

				var panel = new ReferencePanel
				{
					Margin = new Thickness(16, 0, 0, 0)
				};
				object themeRes = null;
				global::Avalonia.Application.Current!.TryFindResource("MultilineReferencePanelStyle", out themeRes);
				panel.Theme = themeRes as global::Avalonia.Styling.ControlTheme;
				Assert.True(panel.Theme != null, "MultilineReferencePanelStyle 资源应存在");
				Console.WriteLine("[diag-theme] found=" + (panel.Theme != null));
				Grid.SetColumn(panel, 1);
				rowGrid.Children.Add(panel);

				// 注意：此测试复刻的层级没有 ScrollViewer（Warp3 的层0/层1 已覆盖带
				// ScrollViewer(HSBV=Disabled) 的完整链路），这里聚焦主题/ItemsPanel 本身。
				window.Content = outer;
				window.Show();
				Dispatcher.UIThread.RunJobs();

				// 注入 12 个本地分支徽章（每个名字较长，总宽远超列宽）
				var branches = new List<Reference>();
				for (int i = 0; i < 12; i++)
				{
					branches.Add(new LocalBranch(Sha.Zero, "refs/heads/feature-branch-with-long-name-" + i,
						"feature-branch-with-long-name-" + i, i == 0, null, DateTime.Now));
				}
				panel.Refresh(branches, new Remote[0]);
				Dispatcher.UIThread.RunJobs();
				Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window);

				var diag = new System.Text.StringBuilder();
				diag.Append($"panel.Bounds={panel.Bounds} panel.Desired={panel.DesiredSize} ");

				var presenter = panel.GetVisualDescendants().OfType<ItemsPresenter>().FirstOrDefault();
				Assert.True(presenter != null, "ReferencePanel 内应有 ItemsPresenter");
				double maxRight = 0;
				if (presenter != null)
				{
					var panelHost = presenter.GetVisualDescendants().OfType<Panel>().FirstOrDefault();
					diag.Append($"itemsPanelType={(panelHost != null ? panelHost.GetType().Name : "<null>")} panelHost.Bounds={panelHost?.Bounds} ");

					var items = (presenter.Panel != null ? presenter.Panel.Children : null) ?? (IList<Avalonia.Controls.Control>)Array.Empty<Avalonia.Controls.Control>();
					diag.Append($"realized={items.Count} ");
					foreach (var child in items)
					{
						maxRight = Math.Max(maxRight, child.Bounds.Right);
					}
					diag.Append($"maxChildRight={maxRight:F0} ");
				}
				else
				{
					diag.Append("no ItemsPresenter! ");
				}

				Console.WriteLine("[diag-wrap] " + diag);

				// 12 枚长名徽章在 ~602px 宽的列里必须换行成多行（单行高约 23px）
				Assert.True(panel.Bounds.Height > 30, "徽章应换行成多行（高度>30）。" + diag);
				Assert.True(maxRight <= panel.Bounds.Right + 2, "徽章右边界应落在面板宽度内。" + diag);

				window.Close();
			});
		}
	}
}
