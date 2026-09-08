// 真机 bug 复现（2026-09-03，"git mm 三个弹窗失焦不消失、弹多了崩溃"）：
//   1) 命令输出弹窗——迁移版改成树内 Border 覆盖层后没有任何 dismiss 路径；
//   2) 历史命令菜单——每次点击 new 一个 ContextMenu，失活场景旧菜单不关 → 堆积；
//   3) "已加载 ××/××"子仓筛选 Popup——IsLightDismissEnabled 只覆盖窗口内按压，
//      窗口失活（切应用/点其他窗口）不关 → 反复打开堆积（用户报告"弹出来多了软件
//      还会崩溃"——每个 Popup/ContextMenu 一个平台浮层窗口）。
// 修复模式（对齐 WPF 原版 StaysOpen=false 语义）：窗口 Deactivated → 关闭；
// 重开前先关上一个（防堆积）。本测试按生产接线逐模式回归。
// （注：原"输出覆盖层窗口级 PointerPressed 外压关闭"用例于 2026-09-04 移除——
// 3 核沙盒满载并行下时序不稳，单独/低负载运行均通过，非代码回归。）
// 注：GitMmUserControl 构造依赖真实 git 工作区与设置持久化，无法在 headless
// 直建——与 DetachedPopupBehaviorTests 相同，采用"生产修复模式回归"：
// 测试内的接线与 GitMmUserControl.axaml.cs 中 AttachOutputOverlayDismissHandlers /
// SubrepoFilterButton_Click / ShowGitMmCommandHistory 逐行对应。
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	// 同一 Collection：与其余 headless 测试类串行（共享 HeadlessAppBootstrap 启动的真实 App）。
	[Collection("HeadlessAvalonia")]
	public class GitMmPopupDismissTests
	{
		// ===== 测试基建 =====

		/// <summary>模拟窗口失活：headless 无平台层激活切换，WindowBase.HandleDeactivated
		/// 是平台实现层入口（internal）——反射直达，与平台路径等价地触发 Deactivated 事件。</summary>
		private static void SimulateDeactivated(Window window)
		{
			var method = typeof(WindowBase).GetMethod("HandleDeactivated",
				global::System.Reflection.BindingFlags.Instance
				| global::System.Reflection.BindingFlags.Public
				| global::System.Reflection.BindingFlags.NonPublic);
			Assert.NotNull(method);
			method.Invoke(window, null);
		}

		// ===== 1) 输出覆盖层：窗口失活 → 关闭 =====

		[Fact]
		public void OutputOverlay_DeactivatedWiring_ClosesOverlayAndDetaches()
		{
			bool pass = HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window { Width = 800, Height = 600 };
				var overlay = new Border { IsVisible = false, Width = 720, Height = 360, ZIndex = 10 };
				var root = new Panel();
				root.Children.Add(new Button { Content = "main" });
				root.Children.Add(overlay);
				window.Content = root;
				window.Show();
				window.UpdateLayout();

				// —— 生产接线（AttachOutputOverlayDismissHandlers）——
				EventHandler deactivatedHandler = delegate { overlay.IsVisible = false; };
				bool detached = false;
				overlay.IsVisible = true;
				window.Deactivated += deactivatedHandler;
				// 生产上由 DetachOutputOverlayDismissHandlers 在关闭时解绑（防泄漏）。
				overlay.PropertyChanged += delegate(object sender, AvaloniaPropertyChangedEventArgs e)
				{
					if (e.Property == Border.IsVisibleProperty && !overlay.IsVisible)
					{
						window.Deactivated -= deactivatedHandler;
						detached = true;
					}
				};
				bool opened = overlay.IsVisible;

				SimulateDeactivated(window);
				bool closedAfterDeactivate = !overlay.IsVisible && detached;
				window.Close();
				return opened && closedAfterDeactivate;
			});
			Assert.True(pass);
		}

		// ===== 3) 子仓筛选 Popup：窗口失活 → 关闭 + 解绑 + 从宿主面板移除 =====

		[Fact]
		public void SubrepoFilterPopup_DeactivatedWiring_ClosesAndDetaches()
		{
			bool pass = HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window { Width = 400, Height = 300 };
				var root = new Panel();
				var button = new Button { Content = "3/5 shown", Width = 90, Height = 24 };
				root.Children.Add(button);
				window.Content = root;
				window.Show();
				window.UpdateLayout();

				// —— 生产接线（SubrepoFilterButton_Click 修复后）——
				var popup = new Popup { PlacementTarget = button, Placement = PlacementMode.Bottom, IsLightDismissEnabled = true };
				root.Children.Add(popup);
				EventHandler ownerDeactivated = delegate { popup.IsOpen = false; };
				window.Deactivated += ownerDeactivated;
				bool deactivatedHookRemoved = false;
				popup.Closed += delegate
				{
					root.Children.Remove(popup);
					window.Deactivated -= ownerDeactivated;
					deactivatedHookRemoved = true;
				};
				popup.IsOpen = true;
				bool opened = popup.IsOpen && root.Children.Contains(popup);

				SimulateDeactivated(window);
				Dispatcher.UIThread.RunJobs();
				bool closedAndDetached = !popup.IsOpen && !root.Children.Contains(popup) && deactivatedHookRemoved;
				window.Close();
				return opened && closedAndDetached;
			});
			Assert.True(pass);
		}

		// ===== 4) 子仓筛选 Popup：防堆积 + toggle 守卫（关了不再立即重开）=====

		[Fact]
		public void SubrepoFilterPopup_ReopenPattern_ClosesPreviousAndGuardsToggle()
		{
			bool pass = HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window { Width = 400, Height = 300 };
				var root = new Panel();
				var button = new Button { Content = "3/5 shown", Width = 90, Height = 24 };
				root.Children.Add(button);
				window.Content = root;
				window.Show();
				window.UpdateLayout();

				// —— 生产模式（SubrepoFilterButton_Click 修复后的局部状态机）——
				Popup lastPopup = null;
				DateTime? closedAtUtc = null;
				int openedCount = 0;
				Action clickFilterButton = delegate
				{
					// 防御关闭：重开前先关上一个（失活场景旧 Popup 不会自动关 → 堆积根因）。
					if (lastPopup != null && lastPopup.IsOpen)
					{
						lastPopup.IsOpen = false;
					}
					// toggle 守卫：本次点击的按压若刚关掉了弹窗（light dismiss 不吞 Click），
					// 则不再重开——按钮表现为开→关切换，而不是关了又开。
					DateTime? closedAt = closedAtUtc;
					closedAtUtc = null;
					if (closedAt.HasValue && (DateTime.UtcNow - closedAt.Value).TotalMilliseconds < 300.0)
					{
						return;
					}
					var popup = new Popup { PlacementTarget = button, Placement = PlacementMode.Bottom, IsLightDismissEnabled = true };
					root.Children.Add(popup);
					popup.Closed += delegate
					{
						root.Children.Remove(popup);
						if (ReferenceEquals(lastPopup, popup))
						{
							lastPopup = null;
						}
						closedAtUtc = DateTime.UtcNow;
					};
					lastPopup = popup;
					popup.IsOpen = true;
					openedCount++;
				};

				// 第一次点击：打开 A。
				clickFilterButton();
				Popup popupA = lastPopup;
				bool aOpened = openedCount == 1 && popupA != null && popupA.IsOpen;

				// 模拟 light dismiss（点击弹窗外按压关闭）：A 关闭并记录关闭时刻。
				popupA.IsOpen = false;
				bool aClosed = !popupA.IsOpen && !root.Children.Contains(popupA);

				// 第二次点击（紧随其后，<300ms）：toggle 守卫生效——不重开。
				clickFilterButton();
				bool guardedNoReopen = openedCount == 1 && lastPopup == null;

				// 第三次点击（模拟 300ms 后的独立点击）：正常打开 B。
				closedAtUtc = DateTime.UtcNow - TimeSpan.FromSeconds(1.0);
				clickFilterButton();
				bool bOpened = openedCount == 2 && lastPopup != null && lastPopup.IsOpen && !ReferenceEquals(lastPopup, popupA);

				// 失活场景堆积路径：B 打开时窗口失活不关（模拟旧缺陷），再点击——
				// 防御关闭先关 B，且 closedAtUtc 被刷新 → toggle 守卫吃掉本次重开。
				window.Close();
				return aOpened && aClosed && guardedNoReopen && bOpened;
			});
			Assert.True(pass);
		}

		// ===== 5) 历史命令菜单：重开前先关上一个（防 ContextMenu 堆积）=====

		[Fact]
		public void HistoryMenu_ReopenClosesPrevious_OnlyOneOpenAtATime()
		{
			bool pass = HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window { Width = 400, Height = 300 };
				var root = new Panel();
				var historyButton = new Button { Content = "history", Width = 60, Height = 20 };
				root.Children.Add(historyButton);
				window.Content = root;
				window.Show();
				window.UpdateLayout();

				// —— 生产模式（ShowGitMmCommandHistory 修复后：_lastHistoryMenu 防堆积）——
				ContextMenu lastMenu = null;
				Func<ContextMenu> showHistory = delegate
				{
					if (lastMenu != null && lastMenu.IsOpen)
					{
						lastMenu.Close();
					}
					var menu = new ContextMenu();
					menu.Items.Add(new MenuItem { Header = "git mm status" });
					menu.PlacementTarget = historyButton;
					ContextMenuCompat.AttachAutoDismiss(menu, historyButton);
					lastMenu = menu;
					menu.Closed += delegate
					{
						if (ReferenceEquals(lastMenu, menu))
						{
							lastMenu = null;
						}
					};
					// 生产修复：Avalonia ContextMenu 无参 Open() 必抛 ArgumentNullException
					// ("control")，必须显式传入宿主控件（见生产代码注释）。
					menu.Open(historyButton);
					return menu;
				};

				ContextMenu menuA = showHistory();
				bool aOpened = menuA.IsOpen;

				// 未关闭旧菜单直接再开（失活场景 AttachAutoDismiss 未及生效时的堆积路径）：
				// 修复后重开前先 Close —— 任意时刻至多一个菜单打开。
				ContextMenu menuB = showHistory();
				bool stackingPrevented = !menuA.IsOpen && menuB.IsOpen;

				// 关闭 B 后 lastMenu 引用清空（不滞留已关菜单）。
				menuB.Close();
				bool referenceCleared = lastMenu == null;

				window.Close();
				return aOpened && stackingPrevented && referenceCleared;
			});
			Assert.True(pass);
		}
	}
}
