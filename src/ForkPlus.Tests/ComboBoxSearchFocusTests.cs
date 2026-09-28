// ComboBoxSearch 附加行为的焦点回归测试（2026-09-28，"搜索框输入一个字符后焦点被抢走"）：
// 过滤刷新（ItemsSource 被替换）触发 ComboBox.OnPropertyChanged → TryFocusSelectedItem()，
// 会把焦点塞给选中项容器，搜索框失去焦点无法继续输入。本用例复现该场景并锁定期望：
// 过滤后焦点必须仍在 PART_SearchBox 上。
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI.Controls;
using Xunit;

namespace ForkPlus.Tests
{
	public class ComboBoxSearchFocusTests
	{
		private sealed class BranchItem
		{
			public string Name { get; set; }
		}

		private static object GetFocusedElement(ComboBox comboBox)
		{
			// Avalonia 12 的 FocusManager.GetFocusManager 是 internal，公开入口是 TopLevel.FocusManager。
			return TopLevel.GetTopLevel(comboBox)?.FocusManager?.GetFocusedElement();
		}

		[Fact]
		public void Filter_KeepsFocusInSearchBox()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				ComboBox comboBox = CreateSearchableComboBox(out BranchItem[] items);
				comboBox.SelectedItem = items[10];
				Dispatcher.UIThread.RunJobs();

				comboBox.IsDropDownOpen = true;
				Dispatcher.UIThread.RunJobs();

				// 下拉打开后行为应已把焦点投到搜索框
				TextBox searchBox = GetFocusedElement(comboBox) as TextBox;
				Assert.NotNull(searchBox);
				Assert.Equal("PART_SearchBox", searchBox.Name);

				// 输入一个字符触发过滤刷新（复现用户操作）
				searchBox.Text = "branch-4";
				Dispatcher.UIThread.RunJobs();

				Assert.Same(searchBox, GetFocusedElement(comboBox));
				Assert.True(comboBox.ItemCount >= 1);
			});
		}

		[Fact]
		public void ArrowDown_KeepsFocusInSearchBox()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				ComboBox comboBox = CreateSearchableComboBox(out BranchItem[] items);
				comboBox.SelectedItem = items[0];
				Dispatcher.UIThread.RunJobs();

				comboBox.IsDropDownOpen = true;
				Dispatcher.UIThread.RunJobs();

				TextBox searchBox = GetFocusedElement(comboBox) as TextBox;
				Assert.NotNull(searchBox);

				// ↓ 触发行为里的 SelectedItem 前移 → ComboBox.TryFocusSelectedItem 抢焦点
				searchBox.RaiseEvent(new KeyEventArgs
				{
					RoutedEvent = InputElement.KeyDownEvent,
					Key = Key.Down,
					Route = Avalonia.Interactivity.RoutingStrategies.Bubble,
				});
				Dispatcher.UIThread.RunJobs();

				Assert.Same(searchBox, GetFocusedElement(comboBox));
			});
		}

		[Fact]
		public void ClearFilter_KeepsFocusInSearchBox()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				ComboBox comboBox = CreateSearchableComboBox(out BranchItem[] items);
				comboBox.SelectedItem = items[10];
				Dispatcher.UIThread.RunJobs();

				comboBox.IsDropDownOpen = true;
				Dispatcher.UIThread.RunJobs();

				TextBox searchBox = GetFocusedElement(comboBox) as TextBox;
				Assert.NotNull(searchBox);

				// 输入过滤再退格清空 → RestoreOriginal 回填 SelectedItem → 可能抢焦点
				searchBox.Text = "branch-4";
				Dispatcher.UIThread.RunJobs();
				searchBox.Text = "";
				Dispatcher.UIThread.RunJobs();

				Assert.Same(searchBox, GetFocusedElement(comboBox));
				Assert.Equal(50, comboBox.ItemCount);
				Assert.Same(items[10], comboBox.SelectedItem);
			});
		}

		private static ComboBox CreateSearchableComboBox(out BranchItem[] items)
		{
			ComboBox comboBox = new ComboBox { Width = 220 };
			ComboBoxSearch.SetIsEnabled(comboBox, true);
			items = Enumerable.Range(0, 50)
				.Select(delegate (int i) { return new BranchItem { Name = "branch-" + i }; })
				.ToArray();
			Window window = new Window { Content = comboBox, SizeToContent = SizeToContent.WidthAndHeight };
			window.Show();
			comboBox.ItemsSource = items;
			Dispatcher.UIThread.RunJobs();
			return comboBox;
		}

		/// <summary>复现 PushWindow "To:" 下拉的真实结构（2026-09-28"默认分支和搜索结果叠在一起"）：
		/// [默认项 "default (origin/main)", 分隔符, 分支…, 分隔符, "Custom..."]，默认项选中，
		/// **ItemsPanel 用 VirtualizingStackPanel（PushWindow 同款）**，搜索词不包含默认项 →
		/// 过滤后弹层里不应再出现默认项，且容器互不重叠。</summary>
		[Fact]
		public void Filter_ExcludingSelectedItem_RendersOnlyFilteredItems()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				// 模拟 RemoteBranchItem：Name 提供项文本（ComboBoxSearch 的反射文本源之一）
				List<BranchItem> source = new List<BranchItem>
				{
					new BranchItem { Name = "default (origin/main)" },
					new BranchItem { Name = "" },
				};
				source.AddRange(Enumerable.Range(0, 50).Select(delegate (int i)
				{
					return new BranchItem { Name = "branch-" + i };
				}));
				source.Add(new BranchItem { Name = "" });
				source.Add(new BranchItem { Name = "Custom..." });
				BranchItem[] items = source.ToArray();

				ComboBox comboBox = new ComboBox { Width = 220 };
				ComboBoxSearch.SetIsEnabled(comboBox, true);
				// PushWindow "To:" 下拉同款虚拟化面板
				comboBox.ItemsPanel = new Avalonia.Controls.Templates.FuncTemplate<Avalonia.Controls.Panel>(delegate
				{
					return new Avalonia.Controls.VirtualizingStackPanel();
				});
				Window window = new Window { Content = comboBox, SizeToContent = SizeToContent.WidthAndHeight };
				window.Show();
				comboBox.ItemsSource = items;
				comboBox.SelectedItem = items[0];
				Dispatcher.UIThread.RunJobs();

				comboBox.IsDropDownOpen = true;
				Dispatcher.UIThread.RunJobs();

				TextBox searchBox = GetFocusedElement(comboBox) as TextBox;
				Assert.NotNull(searchBox);

				// 逐字符输入（真实击键：每次 TextChanged 换一次 ItemsSource，VSP 反复回收容器）
				string keyword = "branch-4";
				foreach (char c in keyword)
				{
					searchBox.Text += c;
					Dispatcher.UIThread.RunJobs();
				}

				// 逻辑层：过滤后只剩 branch-4、branch-40~49（过滤词隐藏分隔符/默认项/Custom...）
				Assert.Equal(11, comboBox.ItemCount);

				// 视觉层：弹层里物化的容器内容只能是过滤结果，且互不重叠
				List<(string Text, Avalonia.Rect Bounds)> rendered = RenderedPopupItems(searchBox);
				Assert.True(rendered.Count >= 1);
				foreach ((string text, _) in rendered)
				{
					Assert.Contains("branch-4", text);
				}
				for (int i = 0; i < rendered.Count; i++)
				{
					for (int j = i + 1; j < rendered.Count; j++)
					{
						Assert.False(rendered[i].Bounds.Intersects(rendered[j].Bounds),
							"弹层容器重叠: " + rendered[i].Text + " @ " + rendered[i].Bounds + " vs " + rendered[j].Text + " @ " + rendered[j].Bounds);
					}
				}
			});
		}

		/// <summary>取弹层（PopupRoot）里已物化 ComboBoxItem 的（项文本, Bounds）。</summary>
		private static List<(string Text, Avalonia.Rect Bounds)> RenderedPopupItems(TextBox searchBox)
		{
			List<(string, Avalonia.Rect)> list = new List<(string, Avalonia.Rect)>();
			// 搜索框与列表同在弹层内，沿视觉祖先找 PopupRoot，再向下收集 ComboBoxItem
			Avalonia.Visual root = (Avalonia.Visual)TopLevel.GetTopLevel(searchBox) ?? searchBox;
			foreach (Avalonia.Visual visual in root.GetVisualDescendants())
			{
				if (visual is ComboBoxItem comboBoxItem)
				{
					string text = comboBoxItem.Content as string
						?? (comboBoxItem.Content as BranchItem)?.Name
						?? "<null>";
					list.Add((text, comboBoxItem.Bounds));
				}
			}
			return list;
		}
	}
}
