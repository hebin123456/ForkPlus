// 回归复现（2026-09-07，"重置的弹窗，重置类型下拉框点了以后里面啥也没有"）：
// 根因 = 双重叠加：
//  1) ComboBox 主题 ItemsPanel 默认 VirtualizingStackPanel 在 Popup 弹层里物化不完整
//     （已改回 WPF 同款 StackPanel，见 Theme/Styles/Combobox.axaml）；
//  2) axaml 内联 ComboBoxItem 的 IsSelected="True" 本地值在容器物化时触发 Avalonia
//     SelectingItemsControl.ContainerForItemPreparedOverride 的 IsSet 分支 →
//     UpdateSelection(index, true, toggleModifier:true) 单选 toggle 语义把已选中项反选成
//     -1 → 空 AddedItems 的 SelectionChanged → 生产处理器 e.AddedItems[0] 抛
//     IndexOutOfRangeException 中断 PanelContainerGenerator 物化循环（后续项不物化 +
//     选中丢失）。修复 = 移除 XAML IsSelected（构造器 SelectedIndex=1 编程选中）+ 处理器
//     空 AddedItems 守卫。本测试用生产链路构造 ResetBranchWindow 真实展开下拉做端到端断言。
using System;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using Xunit;
using Xunit.Abstractions;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class ResetBranchPopupDiagnosticTests
	{
		private readonly ITestOutputHelper _output;

		public ResetBranchPopupDiagnosticTests(ITestOutputHelper output)
		{
			_output = output;
		}

		[Fact]
		public void ResetTypeDropdown_PopupShowsAllItems()
		{
			string repo = TestRepoFactory.CreateHistoryRewrite();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences refs = WaitForRefs(repoControl);
						LocalBranch main = refs.ActiveBranch;
						string baseOneSha = TestRepoFactory.GitOutput(repo, "rev-parse main~1").Trim();
						Revision baseOne = RevisionFor(repoControl.GitModule, baseOneSha);

						var dialog = new ResetBranchWindow(repoControl, main, baseOne);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();

						ComboBox combo = dialog.ResetTypeCombobox;

						// Items 集合内容（生成器生成前的原始事实）
						_output.WriteLine($"ComboBox.Items: count={combo.Items.Count()} (ItemCount={combo.ItemCount})");
						foreach (object? item in combo.Items)
						{
							var cbi = item as ComboBoxItem;
							_output.WriteLine($"  item: type={item?.GetType().Name}, tag={cbi?.Tag?.ToString() ?? "n/a"}, isSet={cbi?.IsSet(ComboBoxItem.IsSelectedProperty)}, val={cbi?.IsSelected}");
						}
						_output.WriteLine($"pre-open: selIdx={combo.SelectedIndex}, selItem={(combo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "null"}");

						// SelectionChanged 事件监控（挂在 XAML 订阅之后：若 XAML 处理器抛异常，后续订阅收不到）
						combo.SelectionChanged += (s, e) =>
						{
							string added = string.Join(",", e.AddedItems.OfType<ComboBoxItem>().Select(i => i.Tag?.ToString()));
							string removed = string.Join(",", e.RemovedItems.OfType<ComboBoxItem>().Select(i => i.Tag?.ToString()));
							_output.WriteLine($"  [SelectionChanged] added=[{added}]({e.AddedItems.Count}), removed=[{removed}]({e.RemovedItems.Count})");
						};

						try
						{
							combo.IsDropDownOpen = true;
							Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						}
						catch (Exception ex)
						{
							_output.WriteLine($"  [EXCEPTION during open] {ex.GetType().Name}: {ex.Message}");
						}
						_output.WriteLine($"post-open: selIdx={combo.SelectedIndex}, selItem={(combo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "null"}");

						Popup popup = combo.GetVisualDescendants().OfType<Popup>()
							.FirstOrDefault((Popup p) => p.Name == "PART_Popup");
						Assert.NotNull(popup);

						ItemsPresenter presenter = popup.Child.GetVisualDescendants().OfType<ItemsPresenter>().FirstOrDefault();
						if (presenter?.Panel != null)
						{
							var panel = presenter.Panel;
							_output.WriteLine($"Panel: type={panel.GetType().Name}, children={panel.Children.Count}, bounds={panel.Bounds}");
							foreach (var child in panel.Children)
							{
								_output.WriteLine($"  panel-child: {child.GetType().Name}, tag={(child as ComboBoxItem)?.Tag?.ToString() ?? "n/a"}, bounds={child.Bounds}, vis={child.IsVisible}");
							}
						}
						else
						{
							_output.WriteLine("Panel: NULL (ItemsPresenter 或 Panel 未创建)");
						}

						// 关闭态 presenter（selection box）内容——验证 VisualBrush 快照机制
						ContentPresenter? closedPresenter = combo.GetVisualDescendants()
							.OfType<ContentPresenter>().FirstOrDefault((ContentPresenter c) => c.Name == "contentPresenter");
						if (closedPresenter != null)
						{
							_output.WriteLine($"closedPresenter: content={closedPresenter.Content?.GetType().Name ?? "null"}");
							if (closedPresenter.Content is Avalonia.Controls.Shapes.Rectangle rect)
							{
								var brush = rect.Fill as Avalonia.Media.VisualBrush;
								_output.WriteLine($"  rect: w={rect.Width}, h={rect.Height}, brushVisual={brush?.Visual?.GetType().Name ?? "null"}");
							}
						}

						var popupItems = popup.Child.GetVisualDescendants().OfType<ComboBoxItem>().ToList();
					string tags = string.Join(",", popupItems.Select(i => i.Tag?.ToString()));
					Assert.True(popupItems.Count == 3, $"弹层内 ComboBoxItem 应有 3 个，实际 {popupItems.Count}：[{tags}]");
					Assert.All(popupItems, (ComboBoxItem i) => Assert.True(i.Bounds.Height > 0, $"弹层项高度应为正：[{tags}]"));
					// 选中保持：容器物化不得触发 Avalonia toggle 反选（XAML IsSelected 本地值根因回归）
					Assert.Equal(1, combo.SelectedIndex);
					Assert.Equal("Mixed", (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString());
					// 三个 tag 齐全
					Assert.Contains("Soft", tags.Split(','));
					Assert.Contains("Mixed", tags.Split(','));
					Assert.Contains("Hard", tags.Split(','));
					// 关闭态 selection box 有内容（VisualBrush 快照）
					Assert.NotNull(closedPresenter?.Content);
				}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally { TestRepoFactory.Cleanup(repo); }
		}

		private static RepositoryReferences WaitForRefs(RepositoryUserControl repoControl)
		{
			Assert.True(UiClick.WaitFor(delegate
			{
				return repoControl.RepositoryData != null
					&& repoControl.RepositoryData.References.LocalBranches.Length >= 2
					&& repoControl.RepositoryData.References.ActiveBranch != null
					&& repoControl.RepositoryStatus != null;
			}), "引用/活跃分支/工作区状态未装配（15s 超时）");
			return repoControl.RepositoryData.References;
		}

		private static Revision RevisionFor(GitModule gitModule, string sha)
		{
			Assert.True(Sha.TryParse(sha, out Sha parsed), "sha 应可解析: " + sha);
			GitCommandResult<Revision[]> result = new GetRevisionsGitCommand().Execute(gitModule, new Sha[] { parsed });
			Assert.True(result.Succeeded, "GetRevisionsGitCommand 应成功: " + (result.Error?.FriendlyDescription ?? ""));
			Assert.Single(result.Result);
			return result.Result[0];
		}
	}
}
