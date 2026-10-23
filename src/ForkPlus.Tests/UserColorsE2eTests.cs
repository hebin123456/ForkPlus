// 真机 bug 复现与回归（颜色链路）：
//   症状1：标记颜色之后，下拉箭头不能重新打开颜色弹窗、不能取消颜色；
//   症状2：点颜色后视图/选择"弹回"当前分支行。
// E2E 真实管线：打开仓库 → 选中非 HEAD 提交 → 打开作者颜色弹窗 → 点色块 →
// 断言 user-colors 落盘、选择不弹回、箭头可再次打开弹窗、已选色块呈选中态。
using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class UserColorsE2eTests
	{
		// Popup 挂在 summary 的祖先 Panel（AttachColorsPopup），必须从窗口根找。
		private static Popup FindOpenPopup(Visual root)
		{
			return root.GetVisualDescendants().OfType<Popup>().FirstOrDefault(p => p.IsOpen);
		}

		private static ToggleButton FindSwatch(Visual root, int brushIndex)
		{
			return root.GetVisualDescendants()
				.OfType<ToggleButton>()
				.FirstOrDefault(x => x.DataContext is UserColorViewModel vm && vm.BrushIndex == brushIndex);
		}

		[Fact]
		public void AuthorColor_ClickSwatch_AppliesColor_NoBounce_ArrowReusable()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string repo = TestRepoFactory.CreateBranches();
			try
			{
				var module = new GitModule(repo, Path.Combine(repo, ".git"), null, null);
				string report = HeadlessAppBootstrap.Run(delegate
				{
					var sb = new System.Text.StringBuilder();
					var control = new RepositoryUserControl();
					control.OpenRepository(module);
					var window = new ForkPlus.UI.CustomWindow { Width = 1920, Height = 1080, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();
					control.InvalidateAndRefresh(SubDomain.All);
					var revList = control.Content.RevisionListViewUserControl;
					Assert.True(UiClick.WaitFor(delegate { return revList.RevisionsDataSource.Count > 0; }), "修订列表未加载");

					// ===== 1) 选中非 HEAD 提交 c5 on feature/two =====
					int c5Row = -1;
					for (int row = 0; row < revList.RevisionsDataSource.Count; row++)
					{
						if (revList.RevisionsDataSource.GetDecoratedRevisionAtRow(row)?.Subject == "c5 on feature/two")
						{
							c5Row = row;
							break;
						}
					}
					Assert.True(c5Row >= 0, "应能定位 c5 on feature/two 行");
					revList.Select(new int[1] { c5Row });
					var details = control.Content.RevisionDetails;
					Assert.True(UiClick.WaitFor(delegate
					{
						return details.FullRevisionDetails != null
							&& details.FullRevisionDetails.RevisionDetails.Message.Trim() == "c5 on feature/two";
					}), "c5 修订详情未加载");
					var summary = details.SummaryUserControl;
					int selectedRowBefore = revList.SelectedIndex;
					sb.AppendLine("before: selectedRow=" + selectedRowBefore);

					// ===== 2) 打开作者颜色弹窗 =====
					summary.AuthorColorsToggleButton.IsChecked = true;
					Dispatcher.UIThread.RunJobs();
					Popup popup1 = FindOpenPopup(window);
					Assert.True(popup1 != null, "首次点击箭头应打开颜色弹窗");

					// ===== 3) 点击 BrushIndex=3 的色块 =====
					// 注：Popup 内容在独立 PopupRoot 中（不挂在 Popup 控件视觉树下），从 Child 找。
					popup1.Child?.UpdateLayout();
					ToggleButton swatch = FindSwatch(popup1.Child, 3);
					Assert.True(swatch != null, "弹窗内应有 BrushIndex=3 色块");
					swatch.IsChecked = true;
					swatch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
					Dispatcher.UIThread.RunJobs();

					// ===== 4) 等刷新完成，断言 user-colors 落盘 =====
					Assert.True(UiClick.WaitFor(delegate
					{
						return File.Exists(Path.Combine(repo, ".git", "fork", "user-colors"));
					}), "user-colors 文件应已写入");
					sb.AppendLine("user-colors: " + File.ReadAllText(Path.Combine(repo, ".git", "fork", "user-colors")).Trim().Replace("\r\n", " | "));

					// ===== 5) 弹窗应关闭、箭头应恢复可用（症状1） =====
					Assert.True(UiClick.WaitFor(delegate
					{
						return summary.AuthorColorsToggleButton.IsEnabled;
					}), "弹窗关闭后箭头按钮应恢复可用");
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
					Dispatcher.UIThread.RunJobs();
					int selectedRowAfter = revList.SelectedIndex;
					sb.AppendLine("after: selectedRow=" + selectedRowAfter + ", arrow IsChecked=" + summary.AuthorColorsToggleButton.IsChecked + ", IsEnabled=" + summary.AuthorColorsToggleButton.IsEnabled);
					Assert.Equal(selectedRowBefore, selectedRowAfter);

					// ===== 6) 箭头可复用：等 RepositoryData 刷新（弹窗选中态的数据源——
					// RevisionSummaryUserControl.GetCurrentUserColorId 从 RepositoryData 现取），
					// 再次打开弹窗。真机路径：UserColors-only 刷新不会重跑详情面板（_userColors
					// 缓存过期），弹窗状态必须来自 RepositoryData 才是最新。
					// 症状1回归：原绑 IsCheckedChanged 时，预选中色块经 TwoWay 绑定触发
					// ColorButton_Changed → 弹窗打开即自关（用户视角"下拉点了没反应"）。 =====
					Assert.True(UiClick.WaitFor(delegate
					{
						return control.RepositoryData != null
							&& control.RepositoryData.UserColors.GetColorId("test@example.com") == 3;
					}), "刷新后 RepositoryData.UserColors 应包含刚应用的颜色");

					summary.AuthorColorsToggleButton.IsChecked = true;
					Dispatcher.UIThread.RunJobs();
					Popup popup2 = FindOpenPopup(window);
					Assert.True(popup2 != null, "标记颜色后应能再次打开颜色弹窗（症状1：下拉死了）");
					popup2.Child?.UpdateLayout();
					Dispatcher.UIThread.RunJobs();
					Popup popup2Still = FindOpenPopup(window);
					Assert.True(popup2Still != null, "弹窗打开后不应立即自关（预选中色块触发事件风暴）");
					if (popup2 != null)
					{
						ToggleButton swatch3 = FindSwatch(popup2.Child, 3);
						Assert.True(swatch3 != null, "再次打开的弹窗内应有 BrushIndex=3 色块");
						Assert.True(swatch3.IsChecked == true, "已应用的颜色（BrushIndex=3）应呈选中态");
						// 取消颜色：点击已选色块
						swatch3.IsChecked = false;
						swatch3.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
						Dispatcher.UIThread.RunJobs();
						Assert.True(UiClick.WaitFor(delegate
						{
							return !File.Exists(Path.Combine(repo, ".git", "fork", "user-colors"))
								|| !File.ReadAllText(Path.Combine(repo, ".git", "fork", "user-colors")).Contains("test@example.com");
						}), "取消选择后 user-colors 应移除该邮箱条目");
						summary.AuthorColorsToggleButton.IsChecked = false;
						Dispatcher.UIThread.RunJobs();
					}

					// ===== 7) 取消后重开弹窗：状态应已刷新（不再显示选中） =====
					// 真机 bug：取消标记后轨道上标记消失，但重开弹窗色块仍显示选中——
					// 原因是选中态取自过期的 _userColors 缓存（UserColors-only 刷新不重跑详情面板）。
					Assert.True(UiClick.WaitFor(delegate
					{
						return control.RepositoryData.UserColors.GetColorId("test@example.com") == 0;
					}), "取消后 RepositoryData.UserColors 应不再包含该邮箱");
					summary.AuthorColorsToggleButton.IsChecked = true;
					Dispatcher.UIThread.RunJobs();
					Popup popup3 = FindOpenPopup(window);
					Assert.True(popup3 != null, "取消颜色后应能再次打开颜色弹窗");
					if (popup3 != null)
					{
						popup3.Child?.UpdateLayout();
						Dispatcher.UIThread.RunJobs();
						Popup popup3Still = FindOpenPopup(window);
						Assert.True(popup3Still != null, "取消后重开的弹窗不应自关");
						ToggleButton swatch3b = FindSwatch(popup3.Child, 3);
						Assert.True(swatch3b != null, "取消后重开的弹窗内应有 BrushIndex=3 色块");
						Assert.False(swatch3b.IsChecked == true, "取消颜色后重开弹窗，色块不应仍显示选中（状态未刷新）");
						summary.AuthorColorsToggleButton.IsChecked = false;
						Dispatcher.UIThread.RunJobs();
					}
					return sb.ToString();
				});
				Console.WriteLine("[UserColorsE2e] " + report.Replace("\n", Environment.NewLine));
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		// 真机 bug（选中 b 行却标记到 a 行）：切换选中行后，修订详情（含作者邮箱）由异步
		// job 加载；job 完成前 summary 仍显示上一行邮箱，此时打开弹窗会捕获旧邮箱 →
		// 选色标记到上一行。回归：详情过期时弹窗延迟打开（toggle 呈按下态），Refresh()
		// 落地新数据后自动补开——点色块必须标记当前选中行的作者。
		[Fact]
		public void ColorsPopup_MarksNewlySelectedRow_NotPreviousRow()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string repo = TestRepoFactory.CreateTwoAuthors();
			try
			{
				var module = new GitModule(repo, Path.Combine(repo, ".git"), null, null);
				string report = HeadlessAppBootstrap.Run(delegate
				{
					var sb = new System.Text.StringBuilder();
					var control = new RepositoryUserControl();
					control.OpenRepository(module);
					var window = new ForkPlus.UI.CustomWindow { Width = 1920, Height = 1080, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();
					control.InvalidateAndRefresh(SubDomain.All);
					var revList = control.Content.RevisionListViewUserControl;
					Assert.True(UiClick.WaitFor(delegate { return revList.RevisionsDataSource.Count > 0; }), "修订列表未加载");
					var details = control.Content.RevisionDetails;

					// ===== 1) 定位两行（Other 在上、Test 在下），等初始行详情加载完成 =====
					// 列表行物化是渐进的（Count>0 不代表全部行就绪），轮询直到两行都能读到主题
					int otherRow = -1, testRow = -1;
					Assert.True(UiClick.WaitFor(delegate
					{
						otherRow = -1; testRow = -1;
						for (int row = 0; row < revList.RevisionsDataSource.Count; row++)
						{
							string subject = revList.RevisionsDataSource.GetDecoratedRevisionAtRow(row)?.Subject;
							if (subject == "commit by Other") { otherRow = row; }
							else if (subject == "commit by Test") { testRow = row; }
						}
						return otherRow >= 0 && testRow >= 0;
					}), "应能定位两行提交");
					Assert.True(UiClick.WaitFor(delegate
					{
						return details.FullRevisionDetails != null
							&& details.FullRevisionDetails.RevisionDetails.Message.Trim() == "commit by Other";
					}), "初始行（Other）详情未加载");
					var summary = details.SummaryUserControl;

					// ===== 2) 切换到 Test 行后立即点箭头（详情 job 尚未完成 → summary 过期）。
					// Select 与置 IsChecked 之间不泵消息：详情完成 post 不会被处理，
					// 保证确定性命中"过期窗口"。 =====
					revList.Select(new int[1] { testRow });
					summary.AuthorColorsToggleButton.IsChecked = true;
					Assert.True(FindOpenPopup(window) == null, "详情过期期间不应打开弹窗（会捕获上一行邮箱）");
					sb.AppendLine("deferred: stale window hit, popup not open yet");

					// ===== 3) 泵消息：Test 行详情落地 → Refresh() 对账自动补开弹窗 =====
					Assert.True(UiClick.WaitFor(delegate { return FindOpenPopup(window) != null; }),
						"详情加载完成后应自动补开颜色弹窗");
					Popup popup = FindOpenPopup(window);
					popup.Child?.UpdateLayout();
					Dispatcher.UIThread.RunJobs();

					// ===== 4) 点色块：必须标记当前选中行（Test）的作者 =====
					ToggleButton swatch = FindSwatch(popup.Child, 3);
					Assert.True(swatch != null, "弹窗内应有 BrushIndex=3 色块");
					swatch.IsChecked = true;
					swatch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
					Dispatcher.UIThread.RunJobs();
					Assert.True(UiClick.WaitFor(delegate
					{
						return File.Exists(Path.Combine(repo, ".git", "fork", "user-colors"));
					}), "user-colors 文件应已写入");
					string content = File.ReadAllText(Path.Combine(repo, ".git", "fork", "user-colors"));
					sb.AppendLine("user-colors: " + content.Trim().Replace("\r\n", " | "));
					Assert.Contains("test@example.com", content);
					Assert.DoesNotContain("other@example.com", content);
					return sb.ToString();
				});
				Console.WriteLine("[UserColorsE2e] " + report.Replace("\n", Environment.NewLine));
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		// 弹窗开着时若 Refresh() 落地了另一行（邮箱已变），弹窗应立即关闭——
		// 否则对旧邮箱选色同样是"画歪"。程序化 Select 不触发 light-dismiss，
		// 可确定性复现"弹窗开着换行"场景。
		[Fact]
		public void ColorsPopup_ClosesWhenSelectionMovesToDifferentAuthor()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string repo = TestRepoFactory.CreateTwoAuthors();
			try
			{
				var module = new GitModule(repo, Path.Combine(repo, ".git"), null, null);
				HeadlessAppBootstrap.Run(delegate
				{
					var control = new RepositoryUserControl();
					control.OpenRepository(module);
					var window = new ForkPlus.UI.CustomWindow { Width = 1920, Height = 1080, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();
					control.InvalidateAndRefresh(SubDomain.All);
					var revList = control.Content.RevisionListViewUserControl;
					Assert.True(UiClick.WaitFor(delegate { return revList.RevisionsDataSource.Count > 0; }), "修订列表未加载");
					var details = control.Content.RevisionDetails;
					int otherRow = -1, testRow = -1;
					Assert.True(UiClick.WaitFor(delegate
					{
						otherRow = -1; testRow = -1;
						for (int row = 0; row < revList.RevisionsDataSource.Count; row++)
						{
							string subject = revList.RevisionsDataSource.GetDecoratedRevisionAtRow(row)?.Subject;
							if (subject == "commit by Other") { otherRow = row; }
							else if (subject == "commit by Test") { testRow = row; }
						}
						return otherRow >= 0 && testRow >= 0;
					}), "应能定位两行提交");
					Assert.True(UiClick.WaitFor(delegate
					{
						return details.FullRevisionDetails != null
							&& details.FullRevisionDetails.RevisionDetails.Message.Trim() == "commit by Other";
					}), "初始行（Other）详情未加载");
					var summary = details.SummaryUserControl;

					// ===== 1) 在 Other 行上正常打开弹窗（详情已就绪） =====
					summary.AuthorColorsToggleButton.IsChecked = true;
					Dispatcher.UIThread.RunJobs();
					Assert.True(FindOpenPopup(window) != null, "详情就绪时应立即打开颜色弹窗");

					// ===== 2) 程序化切到 Test 行（无 light-dismiss，弹窗保持开）→ 详情落地 =====
					revList.Select(new int[1] { testRow });
					Assert.True(UiClick.WaitFor(delegate
					{
						return details.FullRevisionDetails.RevisionDetails.Message.Trim() == "commit by Test";
					}), "Test 行详情未加载");

					// ===== 3) Refresh() 对账：邮箱已变 → 弹窗应已自动关闭 =====
					Assert.True(FindOpenPopup(window) == null, "选中行作者已变，开着的弹窗应自动关闭（防对旧邮箱选色）");
					Assert.False(summary.AuthorColorsToggleButton.IsChecked == true, "弹窗关闭后箭头应复位");
					Assert.False(File.Exists(Path.Combine(repo, ".git", "fork", "user-colors")), "未选色不应写 user-colors");
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}
	}
}
