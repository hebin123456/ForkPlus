// PushWindow "To:" 下拉搜索残影复现（2026-09-28，"默认分支残影叠在搜索结果上"）：
// 有上游分支时 "To:" 列表首项是 "default (origin/main)" 且默认选中；搜索词不包含它时，
// 弹层里不应残留它的容器（VSP 对聚焦/选中容器有不回收路径，残影 = 旧容器未摘除、
// 叠在新列表第一项后面；hover 时新项画不透明 hover 背景盖住残影，移开又透出来）。
using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using Xunit;
using Xunit.Abstractions;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class PushWindowSearchGhostTests
	{
		private readonly ITestOutputHelper _output;

		public PushWindowSearchGhostTests(ITestOutputHelper output)
		{
			_output = output;
		}

		[Fact]
		public void ToCombo_Search_ExcludingDefault_LeavesNoGhostContainer()
		{
			string work = TestRepoFactory.CreateRemoteBranches();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(work, out var window);
					try
					{
						var dialog = new PushWindow(repoControl);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();

						ComboBox toCombo = dialog.RemoteBranchesComboBox;
						var defaultItem = (PushWindow.RemoteBranchItem)toCombo.SelectedItem;
						_output.WriteLine("初始选中: " + defaultItem.Title);
						Assert.Equal(E2eMainWindowHarness.TrFormat("default ({0})", "origin/main"), defaultItem.Title);

						toCombo.IsDropDownOpen = true;
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);

						Popup popup = toCombo.GetVisualDescendants().OfType<Popup>()
							.First(delegate (Popup p) { return p.Name == "PART_Popup"; });
						TextBox searchBox = popup.Child.GetVisualDescendants().OfType<TextBox>()
							.First(delegate (TextBox t) { return t.Name == "PART_SearchBox"; });

						// 真实应用时序补齐：弹层打开时默认容器已物化并被 TryFocusSelectedItem 聚焦
						//（headless 下 Opened 早于布局拿不到容器），同时 anchor 容器物化即写
						// TabOnceActiveElement（SelectingItemsControl.ContainerForItemPreparedOverride）。
						if (toCombo.ContainerFromIndex(0) is Control defaultContainer)
						{
							defaultContainer.Focus();
							Dispatcher.UIThread.RunJobs();
							_output.WriteLine("模拟真实应用：聚焦默认容器后 TabOnce="
								+ Describe(Avalonia.Input.KeyboardNavigation.GetTabOnceActiveElement(toCombo) as Avalonia.Visual));
						}
						// 用户点击搜索框开始输入
						searchBox.Focus();
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);

						// 逐字符输入搜索词（不包含 default 项）
						foreach (char c in "rb")
						{
							searchBox.Text += c;
							Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						}

						DumpPanel(toCombo, popup);

						// 断言一：逻辑层过滤正确（rb-one、rb-two）
						Assert.Equal(2, toCombo.ItemCount);

						// 断言二：弹层面板里没有容器还挂着 default 项
						var panel = popup.Child.GetVisualDescendants().OfType<Panel>()
							.First(delegate (Panel p) { return p.Name != "grid" && p.Children.Count > 0; });
						foreach (Control child in panel.Children.OfType<Control>())
						{
							if (child is ComboBoxItem comboBoxItem)
							{
								var item = comboBoxItem.Content as PushWindow.RemoteBranchItem;
								_output.WriteLine("panel-child: title=" + (item?.Title ?? "<null>")
									+ ", bounds=" + comboBoxItem.Bounds + ", visible=" + comboBoxItem.IsVisible
									+ ", tabOnce=" + (Avalonia.Input.KeyboardNavigation.GetTabOnceActiveElement(toCombo) == comboBoxItem));
								Assert.False(item != null && item.Title.Contains("default"),
									"残影：default 项的容器还挂在面板上 @ " + comboBoxItem.Bounds);
							}
						}
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, work);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(work);
			}
		}

		private static string Describe(Avalonia.Visual v)
		{
			if (v == null)
			{
				return "<null>";
			}
			string name = (v as Avalonia.StyledElement)?.Name;
			return v.GetType().Name + (string.IsNullOrEmpty(name) ? "" : "#" + name);
		}

		private void DumpPanel(ComboBox toCombo, Popup popup)
		{
			_output.WriteLine("TabOnceActiveElement: " + (
				Avalonia.Input.KeyboardNavigation.GetTabOnceActiveElement(toCombo)?.ToString() ?? "<null>"));
			foreach (Avalonia.Visual v in popup.Child.GetVisualDescendants())
			{
				if (v is ComboBoxItem cbi)
				{
					var item = cbi.Content as PushWindow.RemoteBranchItem;
					_output.WriteLine("  CBI: title=" + (item?.Title ?? "<null>") + ", bounds=" + cbi.Bounds
						+ ", visible=" + cbi.IsVisible + ", parent=" + (cbi.Parent?.GetType().Name ?? "<null>"));
				}
			}
		}
	}
}
