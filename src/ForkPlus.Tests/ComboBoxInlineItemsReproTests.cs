// 最小复现（2026-09-07）：内联 ComboBoxItem + 预选中 → 弹层容器丢失。
// 矩阵变体：无选中 / Show前选中 / Show后选中 × 默认VSP / StackPanel。
using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;
using Xunit.Abstractions;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class ComboBoxInlineItemsReproTests
	{
		private readonly ITestOutputHelper _output;

		public ComboBoxInlineItemsReproTests(ITestOutputHelper output)
		{
			_output = output;
		}

		private static ComboBox BuildCombo()
		{
			var combo = new ComboBox();
			foreach (string tag in new[] { "Soft", "Mixed", "Hard" })
			{
				// 与 ResetBranchWindow 相同：Control 内容（DockPanel 定宽 420）
				var dock = new DockPanel { Width = 420 };
				dock.Children.Add(new Avalonia.Controls.Shapes.Ellipse
				{
					Width = 12, Height = 12, Margin = new Avalonia.Thickness(0, 0, 4, 0),
					Fill = Avalonia.Media.Brushes.Green, [Avalonia.Controls.DockPanel.DockProperty] = Avalonia.Controls.Dock.Left
				});
				dock.Children.Add(new TextBlock { Text = tag });
				combo.Items.Add(new ComboBoxItem { Tag = tag, Content = dock });
			}
			return combo;
		}

		private string Probe(ComboBox combo, Window window, int round)
		{
			// 展开前：每项 IsSet/IsSelected 快照 + 当前选中
			var preItems = combo.Items.OfType<ComboBoxItem>().ToList();
			_output.WriteLine($"pre-open round{round}: selIdx={combo.SelectedIndex}, selItem={(combo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "null"}, "
				+ string.Join(",", preItems.Select(i => $"{i.Tag}:isSet={i.IsSet(ComboBoxItem.IsSelectedProperty)},val={i.IsSelected}")));
			combo.IsDropDownOpen = true;
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
			Popup popup = combo.GetVisualDescendants().OfType<Popup>().First((Popup p) => p.Name == "PART_Popup");
			ItemsPresenter presenter = popup.Child.GetVisualDescendants().OfType<ItemsPresenter>().First();
			var panel = presenter.Panel;
			string tags = string.Join(",", panel.Children.Select(c => (c as ComboBoxItem)?.Tag?.ToString() ?? c.GetType().Name));
			string bounds = string.Join("|", panel.Children.Select(c => c.Bounds.Width + "x" + c.Bounds.Height));
			// 关闭态 selection box 内容
			var closed = combo.GetVisualDescendants().OfType<ContentPresenter>()
				.FirstOrDefault((ContentPresenter c) => c.Name == "contentPresenter");
			return $"round{round}: panel={panel.GetType().Name}, children={panel.Children.Count}, tags=[{tags}], bounds=[{bounds}], selIdx={combo.SelectedIndex}, closedContent={closed?.Content?.GetType().Name ?? "null"}";
		}

		[Fact]
	public void Matrix_XamlSetIsSelectedBeforeAdd()
	{
		HeadlessAppBootstrap.Run(delegate
		{
			// XAML 解析顺序复刻：ComboBoxItem 属性（含 IsSelected="True"）在加入 Items 前设置
			// ——routed IsSelectedChanged 触发时 Parent != combo，SelectingItemsControl 收不到，
			// 本地值存活到容器物化时刻 → 走 ContainerForItemPreparedOverride 的 IsSet 分支。
			var window = new Window { Width = 500, Height = 200 };
			var combo = new ComboBox();
			string[] tags = { "Soft", "Mixed", "Hard" };
			for (int i = 0; i < tags.Length; i++)
			{
				var item = new ComboBoxItem { Tag = tags[i], Content = BuildContent(tags[i]) };
				if (tags[i] == "Mixed")
					item.IsSelected = true; // XAML 属性顺序：加 Items 之前设置
				combo.Items.Add(item);
			}
			_output.WriteLine("before ctor-sel: selIdx=" + combo.SelectedIndex
				+ ", mixed.isSet=" + combo.Items.OfType<ComboBoxItem>().First(i => (string)i.Tag == "Mixed").IsSet(ComboBoxItem.IsSelectedProperty));
			combo.SelectedIndex = 1;
			var panel = new Avalonia.Controls.Panel { Children = { combo } };
			window.Content = panel;
			window.Show();
			Dispatcher.UIThread.RunJobs();
			_output.WriteLine(Probe(combo, window, 1));
			window.Close();
		});
	}

	private static Avalonia.Controls.Control BuildContent(string tag)
	{
		var dock = new DockPanel { Width = 420 };
		dock.Children.Add(new TextBlock { Text = tag });
		return dock;
	}

	[Fact]
	public void Matrix_XamlIsSelected_Plus_CtorSelectedIndex_Narrow()
	{
			HeadlessAppBootstrap.Run(delegate
			{
				// 完整还原真实弹窗初始化序：XAML IsSelected=True + 构造器 SelectedIndex=1 + 窄下拉
				var window = new Window { Width = 400, Height = 200 };
				var combo = BuildCombo();
				var mixed = combo.Items.OfType<ComboBoxItem>().First(i => (string)i.Tag == "Mixed");
				mixed.IsSelected = true;
				combo.SelectedIndex = 1;
				var panel = new Avalonia.Controls.Panel { Children = { combo } };
				window.Content = panel;
				window.Show();
				Dispatcher.UIThread.RunJobs();
				_output.WriteLine(Probe(combo, window, 1));
				// 再开关一轮：物化状态是否变化
				combo.IsDropDownOpen = false;
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
				_output.WriteLine(Probe(combo, window, 2));
				window.Close();
			});
		}

		[Fact]
		public void Matrix_NoSelection_Vsp()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window { Width = 500, Height = 200 };
				var combo = BuildCombo();
				var panel = new Avalonia.Controls.Panel { Children = { combo } };
				window.Content = panel;
				window.Show();
				Dispatcher.UIThread.RunJobs();
				_output.WriteLine(Probe(combo, window, 1));
				window.Close();
			});
		}

		[Fact]
		public void Matrix_SelectBeforeShow_Vsp()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window { Width = 500, Height = 200 };
				var combo = BuildCombo();
				combo.SelectedIndex = 1;
				var panel = new Avalonia.Controls.Panel { Children = { combo } };
				window.Content = panel;
				window.Show();
				Dispatcher.UIThread.RunJobs();
				_output.WriteLine(Probe(combo, window, 1));
				window.Close();
			});
		}

		[Fact]
		public void Matrix_SelectAfterShow_Vsp()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window { Width = 500, Height = 200 };
				var combo = BuildCombo();
				var panel = new Avalonia.Controls.Panel { Children = { combo } };
				window.Content = panel;
				window.Show();
				Dispatcher.UIThread.RunJobs();
				combo.SelectedIndex = 1;
				Dispatcher.UIThread.RunJobs();
				_output.WriteLine(Probe(combo, window, 1));
				window.Close();
			});
		}

		[Fact]
		public void Matrix_SelectBeforeShow_StackPanel_Narrow()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				// 窄窗：下拉视口(~389) < 项宽(430) → 触发水平滚动条（真实弹窗布局）
				var window = new Window { Width = 400, Height = 200 };
				var combo = BuildCombo();
				combo.SelectedIndex = 1;
				var panel = new Avalonia.Controls.Panel { Children = { combo } };
				window.Content = panel;
				window.Show();
				Dispatcher.UIThread.RunJobs();
				_output.WriteLine(Probe(combo, window, 1));
				window.Close();
			});
		}

		[Fact]
		public void Matrix_SelectBeforeShow_StackPanel()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window { Width = 500, Height = 200 };
				var combo = BuildCombo();
				combo.SelectedIndex = 1;
				combo.ItemsPanel = new Avalonia.Controls.Templates.FuncTemplate<Avalonia.Controls.Panel>(() => new StackPanel());
				var panel = new Avalonia.Controls.Panel { Children = { combo } };
				window.Content = panel;
				window.Show();
				Dispatcher.UIThread.RunJobs();
				_output.WriteLine(Probe(combo, window, 1));
				window.Close();
			});
		}
	}
}
