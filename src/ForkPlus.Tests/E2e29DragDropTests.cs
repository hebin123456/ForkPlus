// E2E 模块29（2026-09-10）：tab 拖拽排序端到端。
// 用户问题："模拟两个事件，一个是单仓的tab拖动排序，另一个是git mm下面子仓的拖动排序，
// 没有git mm的话……可以用git repo模拟……现在子仓的拖动排序是坏的……坑1也顺便修复一下"。
//
// 被测链路（一次手势完整管线，不是绕过生产代码直调 Drop 处理器）：
//   生产 PointerPressed/PointerMoved 处理器（ClosableTabItem / GitMmUserControl）
//   → DragDropLauncher.DoDragDrop(press,...)（坑1修复后的直传重载）
//   → Avalonia DragDrop.DoDragDropAsync → AvaloniaLocator → IPlatformDragSource
//   → HeadlessInProcessDragSource（测试进程内拖拽源，经 DispatchProxy 注册，见其文件注释）
//   → DragEnter/DragOver/DragLeave/Drop 路由事件 → 生产 Drop 处理器重排 Items。
//
// 关键事实（编写前经反射/IL 探针实证，Avalonia 12.1.1）：
//   1) DragDrop.DoDragDropAsync 每次调用经 AvaloniaLocator.Current.GetService 查
//      IPlatformDragSource（无缓存静态字段）——bootstrap 里注册的代理可被命中；
//   2) IPlatformDragSource.DoDragDropAsync 恰好 3 参（PointerPressedEventArgs, IDataTransfer,
//      DragDropEffects）——代理按方法名+参数数转发；
//   3) DragDrop.AllowDrop 是 inherits:true 附加属性——TabItem 上 SetAllowDrop(true) 后
//      整个 header 子树（最深 Interactive 命中）都可作为落点；
//   4) PointerEventArgs 构造器的 position 参数是 rootVisual 坐标系——统一传 window 作
//      rootVisual，与生产 e.GetPosition(null)（根相对）同一基准。
//
// 事件驱动方式：与 UiClick.Press / TitleBarDragSnapTests 同款 RaiseEvent 指针路由事件
// （真实 Win32 管线里按下也发往 hit-test 的最深控件，冒泡经过 TabItem 的实例处理器——
// 语义等价，现有 30+ 用例实证）。手势用同一个 Pointer 实例贯穿 press/move/release。
using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e29DragDropTests
	{
		private const string ModuleDir = "29-dragdrop";

		// ============================ 拖拽手势模拟 ============================

		/// <summary>单次完整拖拽手势：press（源记录参数）→ move 超阈值（发起拖拽会话）→
		/// move 到落点（DragEnter 落点 tab）→ release（Drop 触发重排）。
		/// 位置全部用窗口相对坐标。四个事件间不泵 Dispatcher——手势期间无重建/重排插入，
		/// 与真实模态拖拽循环（DoDragDropAsync 完成前 UI 冻结在手势上）的原子性一致。</summary>
		private static void PerformDrag(InputElement source, Window window, Point pressPosition, Point dropPosition)
		{
			Pointer pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
			ulong timestamp = (ulong)Environment.TickCount64;
			// 中途点取 press→drop 线段 1/3 处：与按下点距离必然大于两档拖拽阈值
			//（ClosableTabItem 10px / SystemParameters 4px），且通常仍在源 tab 上
			//（DragEnter 打在源上无副作用，Drop 处理器对源==落点有短路）。
			Point midPosition = new Point(
				pressPosition.X + (dropPosition.X - pressPosition.X) / 3.0,
				pressPosition.Y + (dropPosition.Y - pressPosition.Y) / 3.0);

			PointerPointProperties pressProperties = new PointerPointProperties(
				RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
			source.RaiseEvent(new PointerPressedEventArgs(
				source, pointer, window, pressPosition, timestamp, pressProperties, KeyModifiers.None));

			PointerPointProperties moveProperties = new PointerPointProperties(
				RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other);
			source.RaiseEvent(new PointerEventArgs(
				InputElement.PointerMovedEvent, source, pointer, window, midPosition,
				timestamp + 1, moveProperties, KeyModifiers.None));
			source.RaiseEvent(new PointerEventArgs(
				InputElement.PointerMovedEvent, source, pointer, window, dropPosition,
				timestamp + 2, moveProperties, KeyModifiers.None));

			PointerPointProperties releaseProperties = new PointerPointProperties(
				RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased);
			source.RaiseEvent(new PointerReleasedEventArgs(
				source, pointer, window, dropPosition, timestamp + 3, releaseProperties,
				KeyModifiers.None, MouseButton.Left));

			Dispatcher.UIThread.RunJobs();
		}

		/// <summary>控件内相对坐标 (ratioX, 0.5) 换算成窗口坐标（拖拽事件统一基准）。
		/// 控件未布局/未挂树时返回 null（调用方以断言报"未完成布局"）。</summary>
		private static Point? PointIn(Visual visual, Window window, double ratioX, double ratioY)
		{
			if (visual.Bounds.Width <= 0 || visual.Bounds.Height <= 0)
			{
				return null;
			}
			return visual.TranslatePoint(new Point(visual.Bounds.Width * ratioX, visual.Bounds.Height * ratioY), window);
		}

		private static string DescribeTabOrder(ClosableTabControl tabControl)
		{
			return string.Join(", ", tabControl.Items.OfType<ClosableTabItem>()
				.Select(delegate (ClosableTabItem t)
				{
					return t.RepositoryUserControl?.GitModule?.Path ?? t.Mode.ToString();
				}));
		}

		// ============================ 1) 单仓 tab 拖拽排序 ============================

		[Fact]
		public void SingleRepo_TabDragReorder_FirstGestureWorks()
		{
			string repoA = TestRepoFactory.CreateBasic();
			string repoB = TestRepoFactory.CreateBasic();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					E2eMainWindowHarness.OpenRepository(repoA, out var window);
					try
					{
						Assert.True(window.TabManager.OpenRepository(repoB), "第二个仓库 tab 应打开成功");
						Dispatcher.UIThread.RunJobs();

						ClosableTabControl tabControl = window.GetVisualDescendants().OfType<ClosableTabControl>().FirstOrDefault();
						Assert.True(tabControl != null, "主窗口应存在 ClosableTabControl");
						ClosableTabItem tabA = tabControl.Items.OfType<ClosableTabItem>()
							.FirstOrDefault(delegate (ClosableTabItem t)
							{
								return string.Equals(t.RepositoryUserControl?.GitModule?.Path, repoA, StringComparison.OrdinalIgnoreCase);
							});
						ClosableTabItem tabB = tabControl.Items.OfType<ClosableTabItem>()
							.FirstOrDefault(delegate (ClosableTabItem t)
							{
								return string.Equals(t.RepositoryUserControl?.GitModule?.Path, repoB, StringComparison.OrdinalIgnoreCase);
							});
						Assert.True(tabA != null && tabB != null, "应存在 repoA/repoB 两个仓库 tab（顺序：" + DescribeTabOrder(tabControl) + "）");

						// 拖拽前顺序：A 在 B 前
						Assert.True(tabControl.Items.IndexOf(tabA) < tabControl.Items.IndexOf(tabB),
							"拖拽前 repoA tab 应在 repoB 前（顺序：" + DescribeTabOrder(tabControl) + "）");

						Point? pressAt = PointIn(tabA, window, 0.5, 0.5);
						Point? dropAt = PointIn(tabB, window, 0.5, 0.5);
						Assert.True(pressAt.HasValue && dropAt.HasValue, "tab 应完成布局（bounds 非零）");

						// 一次手势即换位（坑1回归：DragDropLauncher 两段式首次手势被吞的时代，
						// 这里第一次 PerformDrag 不会改变顺序）。
						PerformDrag(tabA, window, pressAt.Value, dropAt.Value);

						Assert.True(tabControl.Items.IndexOf(tabB) < tabControl.Items.IndexOf(tabA),
							"单次手势后 repoB tab 应移到 repoA 前（实际顺序：" + DescribeTabOrder(tabControl) + "）");
						Assert.True(tabA.IsSelected, "拖拽完成后被拖 tab 应被选中");
						Assert.False(HeadlessInProcessDragSource.Instance.IsActive, "拖拽会话应已结束");

						ScreenshotHelper.Snap(window, "01-single-repo-tab-reordered", ModuleDir);
					}
					finally
					{
						// 两个仓库 tab 都走标准收尾（关 tab + 排空后台任务 + 摘窗）
						E2eMainWindowHarness.CloseRepositoryTab(window, repoB);
						E2eMainWindowHarness.CloseRepositoryTab(window, repoA);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repoA);
				TestRepoFactory.Cleanup(repoB);
			}
		}

		// ============================ 2) git mm 子仓 tab 拖拽排序 ============================

		[Fact]
		public void GitMm_SubrepoTabDragReorder_Works()
		{
			string ws = TestRepoFactory.CreateGitMmWorkspace();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					// 沙箱无 git-mm CLI：打开工作区会弹 missing 警告（看门狗自动关闭并记录）
					HeadlessAppBootstrap.ExpectErrorDialogs();
					E2eMainWindowHarness.OpenTab(ws, out var window);
					try
					{
						GitMmUserControl gitMm = window.GetVisualDescendants().OfType<GitMmUserControl>().FirstOrDefault();
						Assert.True(gitMm != null, "应创建 GitMmUserControl（git mm 模式 tab）");

						// 子仓经后台扫描装配（RefreshSubrepos → RunBackground → Dispatcher.Post →
						// RebuildSubrepoTabs）：等 2 个子仓 tab 出现并完成布局。
						TabControl subrepoTabs = null;
						Assert.True(UiClick.WaitFor(delegate
						{
							subrepoTabs = gitMm.GetVisualDescendants().OfType<TabControl>()
								.FirstOrDefault(delegate (TabControl tc)
								{
									return tc.Name == "SubreposTabControl";
								});
							return subrepoTabs != null && subrepoTabs.Items.OfType<TabItem>().Count() == 2;
						}), "子仓 tab 应装配为 2 个（15s 超时；后台扫描路径）");
						TabItem tabA = subrepoTabs.Items.OfType<TabItem>().ElementAt(0);
						TabItem tabB = subrepoTabs.Items.OfType<TabItem>().ElementAt(1);
						string nameA = (tabA.Tag as GitMmSubrepoItem)?.Name;
						string nameB = (tabB.Tag as GitMmSubrepoItem)?.Name;
						Assert.Equal("repoA", nameA);
						Assert.Equal("repoB", nameB);

						Assert.True(UiClick.WaitFor(delegate
						{
							return tabA.Bounds.Width > 0 && tabB.Bounds.Width > 0;
						}), "子仓 tab 应完成布局（bounds 非零）");
						// UpdateSubrepoTabWidths 经 0.1s 延迟动作改写 Width——等它跑完再取落点坐标，
						// 防止"按旧坐标取点、布局又变了"的竞态（取点与手势之间同步无泵，无新竞态）。
						System.Threading.Tasks.Task.Delay(400).GetAwaiter().GetResult();
						Dispatcher.UIThread.RunJobs();

						// GetVisualRoot 在 Avalonia 12 已移除，等价改判"窗口在被测 tab 的视觉祖先链上"。
						Assert.True(tabA.GetVisualAncestors().Contains(window), "拖拽发起前 repoA tab 应仍挂在窗口上（未被重建）");
						Assert.True(tabB.GetVisualAncestors().Contains(window), "拖拽发起前 repoB tab 应仍挂在窗口上（未被重建）");

						Point? pressAt = PointIn(tabA, window, 0.5, 0.5);
						// 落点取 B 的右半：SubrepoTabItem_Drop 以中线分界决定插前/插后——
						// A 拖到 B 右半 = A 移到 B 之后（[repoA, repoB] → [repoB, repoA]）。
						Point? dropAt = PointIn(tabB, window, 0.75, 0.5);
						Assert.True(pressAt.HasValue && dropAt.HasValue, "子仓 tab 布局坐标应可换算");

						// 一次手势即换位（坑1回归：RebuildSubrepoTabs 动态重建的 tab 走旧
						// DoDragDrop(source,...) 首次手势必被吞；且 v4.0.9 之前 WeakReference
						// 直传被 ToString，落点拿不回原对象，重排完全不执行）。
						PerformDrag(tabA, window, pressAt.Value, dropAt.Value);

						string[] order = subrepoTabs.Items.OfType<TabItem>()
							.Select(delegate (TabItem t)
							{
								return (t.Tag as GitMmSubrepoItem)?.Name;
							})
							.ToArray();
						Assert.Equal(new string[] { "repoB", "repoA" }, order);
						Assert.True(tabA.IsSelected, "拖拽完成后被拖子仓 tab 应被选中");
						Assert.False(HeadlessInProcessDragSource.Instance.IsActive, "拖拽会话应已结束");

						ScreenshotHelper.Snap(window, "02-gitmm-subrepo-tab-reordered", ModuleDir);

						// 收尾取走看门狗捕获的 git-mm missing 警告（预期弹窗，防遗留到下个用例）
						HeadlessAppBootstrap.TakeCapturedErrorDialogs();
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, ws);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(ws);
			}
		}
	}
}
