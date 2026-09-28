// 回归测试（2026-09-28，"所有提交→提交 tab 无法选中内容复制"）：
// RevisionSummaryUserControl 的 Subject/Description 是 SelectableTextBlock（ForkPlus 派生类），
// 用户鼠标拖选 commit message 始终无效。三层根因（缺一不可，均已修复）：
//   1) 全局样式选择器 Selector="SelectableTextBlock" 是精确类型匹配（TypeNameAndClassSelector.
//      OfType → IsConcreteType），Avalonia 类型选择器不匹配派生类（与 WPF 不同）→ 9/19 加的
//      SelectionBrush 样式从未生效。改 :is()（IsAssignableFrom 语义）。
//   2) Background=null 时命中测试只认字形像素，行间留白/字后空白全部穿透，按下落到下层
//      ScrollContentPresenter 被吃掉，OnPointerPressed 永不触发。样式补 Background=Transparent。
//   3) 选中态无高亮渲染（SelectionBrush null）→ 即使选中也不可见。同一样式修复。
// 本测试走真实输入管线（HeadlessWindowExtensions.MouseDown/Move/Up：RawPointerEventArgs →
// MouseDevice → 真实命中测试 → 路由事件），与 UiClick.Press（直接 RaiseEvent、绕过命中测试）
// 不同——根因 2 只有本方式能复现。注意 modifiers 必须显式传 LeftMouseButton，
// PointerPointProperties.IsLeftButtonPressed 由该标志驱动（默认 None 时拖选逻辑恒不进）。
using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class RevisionSummaryDragSelectionTests
	{
		[Fact]
		public void RevisionSummary_SubjectText_DragSelectWithRealInput()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string repo = TestRepoFactory.CreateBranches();
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

					// ===== 1) 加载修订列表并选中 c5（复用 E2e04 管线） =====
					control.InvalidateAndRefresh(SubDomain.All);
					var revList = control.Content.RevisionListViewUserControl;
					Assert.True(UiClick.WaitFor(delegate { return revList.RevisionsDataSource.Count > 0; }),
						"修订列表未加载出数据");
					int c5Row = -1;
					for (int row = 0; row < revList.RevisionsDataSource.Count; row++)
					{
						if (revList.RevisionsDataSource.GetDecoratedRevisionAtRow(row)?.Subject == "c5 on feature/two")
						{
							c5Row = row;
							break;
						}
					}
					Assert.True(c5Row >= 0, "应能定位到 c5 on feature/two 行");
					revList.Select(new int[1] { c5Row });
					var details = control.Content.RevisionDetails;
					Assert.True(UiClick.WaitFor(delegate
					{
						return details.FullRevisionDetails != null
							&& details.FullRevisionDetails.RevisionDetails.Message.Trim() == "c5 on feature/two";
					}), "c5 修订详情未加载");
					var summary = details.SummaryUserControl;
					Assert.True(summary.IsVisible, "应停留在 Commit（摘要）tab");

					// ===== 2) 全局样式回归断言（根因 1/3 的防线）：
					//      :is(SelectableTextBlock) 样式必须挂上派生类——SelectionBrush 缺失
					//      会让选中无高亮（不可见），Background 缺失会让拖选起点穿透。 =====
					var subject = summary.SubjectTextBlock;
					Assert.Equal("c5 on feature/two", subject.Text);
					Assert.True(subject.Background != null,
						"SelectableTextBlock 全局样式的 Background=Transparent 未生效（拖选起点会穿透到下层）");
					Assert.True(subject.SelectionBrush != null,
						"SelectableTextBlock 全局样式的 SelectionBrush 未生效（选中后无高亮不可见）");

					// ===== 3) 计算拖选起止点（窗口坐标；Background=Transparent 后整个控件区域可命中，
					//      无需精确落在字形上——这正是修复要保证的行为） =====
					double midY = Math.Max(2, subject.Bounds.Height / 2);
					var startLocal = new Point(3, midY);
					var endLocal = new Point(Math.Min(subject.Bounds.Width - 4, 90), midY);
					Point? startWin = subject.TranslatePoint(startLocal, window);
					Point? endWin = subject.TranslatePoint(endLocal, window);
					Assert.True(startWin.HasValue && endWin.HasValue, "SubjectTextBlock 应能换算到窗口坐标");

					// ===== 4) 真实输入管线拖选：按下 → 移动 → 释放 =====
					var leftBtn = global::Avalonia.Input.RawInputModifiers.LeftMouseButton;
					window.MouseDown(startWin.Value, MouseButton.Left, leftBtn);
					window.MouseMove(endWin.Value, leftBtn);
					window.MouseUp(endWin.Value, MouseButton.Left, global::Avalonia.Input.RawInputModifiers.None);
					Dispatcher.UIThread.RunJobs();

					// ===== 5) 断言：选择发生且选中文本非空 =====
					string selected = subject.SelectedText;
					Assert.True(subject.SelectionStart != subject.SelectionEnd,
						"拖选后 SelectionStart/End 应不同（选择未启动：按下点命中测试未落到文本块上）");
					Assert.False(string.IsNullOrEmpty(selected), "选中文本不应为空");
					Assert.StartsWith("c5", selected, StringComparison.Ordinal);

					window.Close();
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}
	}
}
