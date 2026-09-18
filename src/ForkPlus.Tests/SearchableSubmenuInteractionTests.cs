// 可搜索远端分组子菜单交互专项测试（"检查远端同步状态"/"跟踪"右键二级菜单）：
// 用户缺陷报告（2026-09-18）：
//   1) 点一下搜索框，右键菜单直接消失；
//   2) 子菜单滚动条拉不了，一点菜单就消失；
//   3) 展示所有分支的框宽度偏宽（MinWidth 调整在 SidebarUserControl，此处只做行为回归 1)/2)）。
// 本文件用生产结构（CreateSearchableRemoteGroupMenuItem 同构：搜索框 MenuItem Header=
// PlaceholderTextBox + StaysOpenOnClick、分支叶子项、SetItems 生产入口、
// AttachSubmenuDragCaptureGuard 生产入口）守护 1)/2)：
// 根因分析见 SidebarUserControl.CreateSearchableRemoteGroupMenuItem 与 MenuExtensions。
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI;
using ForkPlus.UI.Controls;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class SearchableSubmenuInteractionTests
	{
		private static void RunJobs()
		{
			Dispatcher.UIThread.RunJobs();
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
		}

		/// <summary>生产同构装配：按钮 + ContextMenu（SetItems 生产入口）+ "检查远端同步状态"
		/// 顶层项 + origin 分组（搜索框行 + N 个分支叶子行）。</summary>
		private sealed class MenuFixture
		{
			public Window Window;
			public Button Button;
			public ContextMenu Menu;
			public MenuItem TopItem;
			public MenuItem GroupItem;
			public PlaceholderTextBox SearchBox;
			public MenuItem SearchBoxItem;

			public static MenuFixture Create(int branchCount)
			{
				var window = new Window { Width = 800, Height = 600 };
				window.Show();
				var button = new Button { Content = "b" };
				window.Content = button;
				RunJobs();

				var searchBox = new PlaceholderTextBox
				{
					Placeholder = "Search",
					// v4.1.4：与生产 CreateSearchableRemoteGroupMenuItem 同款（MinWidth 200）
					MinWidth = 200,
					Margin = new Thickness(4, 3, 4, 3),
					Padding = new Thickness(4, 2, 4, 2)
				};
				StyleCompat.SetStyle(searchBox, Avalonia.Application.Current?.TryFindResource("SearchPanelPlaceholderTextBox"));
				var searchBoxItem = new MenuItem
				{
					Header = searchBox,
					StaysOpenOnClick = true
				};
				// v4.1.4：生产同款——搜索行专用主题（无图标/手势/箭头槽位，Header 铺满整行，
				// 搜索框宽度跟随弹层宽度、弹层宽度由最宽分支行驱动）
				searchBoxItem.Theme = Avalonia.Application.Current?.TryFindResource("SearchableSubmenuSearchRowMenuItem") as Avalonia.Styling.ControlTheme;
				// v4.1.4：生产入口同款——搜索框行焦点防抢守卫（MenuBase/菜单交互处理器会把
				// 焦点抢到行 MenuItem 上，搜索框拿不到焦点无法输入，真实 Windows SendInput 实证）
				searchBoxItem.AttachSearchBoxFocusGuard(searchBox);
				var groupItem = new MenuItem { Header = "origin" };
				groupItem.Items.Add(searchBoxItem);
				for (int i = 0; i < branchCount; i++)
				{
					groupItem.Items.Add(new MenuItem { Header = "remote/branch-" + i.ToString("D3") });
				}
				// v4.1.4：生产入口同款——CreateSearchableRemoteGroupMenuItem 对分组项挂的拖拽捕获守卫
				groupItem.AttachSubmenuDragCaptureGuard();
				// 生产同构：子菜单打开后聚焦搜索框（生产 SubmenuOpened 挂钩同款）
				groupItem.SubmenuOpened += delegate
				{
					groupItem.Dispatcher.Post(delegate { searchBox.Focus(); }, DispatcherPriority.Background);
				};
				var topItem = new MenuItem { Header = "Check Remote Sync Status..." };
				topItem.Items.Add(groupItem);

				var menu = new ContextMenu();
				button.ContextMenu = menu;
				menu.PlacementTarget = button;
				menu.SetItems(new List<Control> { topItem });
				return new MenuFixture
				{
					Window = window,
					Button = button,
					Menu = menu,
					TopItem = topItem,
					GroupItem = groupItem,
					SearchBox = searchBox,
					SearchBoxItem = searchBoxItem
				};
			}
		}

		/// <summary>打开菜单并展开 origin 分组子菜单（生产路径：SubmenuOpened 后搜索框进视觉树）。</summary>
		private static void OpenSearchableSubmenu(MenuFixture fx)
		{
			fx.Menu.Open(fx.Button);
			RunJobs();
			Assert.True(fx.Menu.IsOpen, "菜单应打开");
			fx.TopItem.IsSubMenuOpen = true;
			RunJobs();
			fx.GroupItem.IsSubMenuOpen = true;
			RunJobs();
			Assert.True(fx.GroupItem.IsSubMenuOpen, "origin 分组子菜单应展开");
			Assert.True(fx.SearchBox.IsAttachedToVisualTree(), "搜索框应随子菜单进入视觉树");
		}

		private static void LeftPressRelease(InputElement target, Window window, Point position, int moveDx)
		{
			var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
			var properties = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
			target.RaiseEvent(new PointerPressedEventArgs(target, pointer, window, position, (ulong)Environment.TickCount64, properties, KeyModifiers.None));
			if (moveDx != 0)
			{
				for (int i = 1; i <= 5; i++)
				{
					var movePoint = position.WithX(position.X + moveDx * i / 5.0);
					target.RaiseEvent(new PointerEventArgs(
						InputElement.PointerMovedEvent, target, pointer, window, movePoint,
						(ulong)Environment.TickCount64, properties, KeyModifiers.None));
				}
			}
			target.RaiseEvent(new PointerReleasedEventArgs(target, pointer, window, position, (ulong)Environment.TickCount64, properties, KeyModifiers.None, MouseButton.Left));
			RunJobs();
		}

		// ===== 1) 点搜索框：菜单/子菜单都必须保持打开 =====

		[Fact]
		public void ClickSearchBox_MenuStaysOpen()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var fx = MenuFixture.Create(10);
				try
				{
					OpenSearchableSubmenu(fx);
					LeftPressRelease(fx.SearchBox, fx.Window, new Point(60, 8), moveDx: 0);
					Assert.True(fx.Menu.IsOpen, "点击搜索框不应关闭右键菜单（用户缺陷：点一下搜索框菜单直接消失）");
					Assert.True(fx.GroupItem.IsSubMenuOpen, "点击搜索框不应收起分组子菜单");
					Assert.True(fx.SearchBox.IsKeyboardFocused, "点击搜索框后搜索框应持有键盘焦点（可直接输入过滤）");
				}
				finally
				{
					fx.Menu.Close();
					fx.Window.Close();
				}
			});
		}

		// ===== 2) 拖拽子菜单滚动条：菜单/子菜单保持打开且滚动生效 =====

		[Fact]
		public void DragSubmenuScrollBar_MenuStaysOpenAndScrolls()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				// 大量分支项让子菜单超出高度约束出现纵向滚动条（生产现象同款）
				var fx = MenuFixture.Create(300);
				try
				{
					OpenSearchableSubmenu(fx);
					Popup popup = fx.GroupItem.GetVisualDescendants().OfType<Popup>()
						.FirstOrDefault(p => p.Name == "PART_Popup" && p.IsOpen);
					Assert.True(popup != null, "分组子菜单 Popup 应已打开");
					ScrollViewer scrollViewer = popup.Child.GetVisualDescendants().OfType<ScrollViewer>()
						.FirstOrDefault(s => s.Name == "SubMenuScrollViewer");
					Assert.True(scrollViewer != null, "子菜单应含 SubMenuScrollViewer");
					Assert.True(scrollViewer.ScrollBarMaximum.Y > 0, "分支项足够多时应出现可滚动范围，实际 max=" + scrollViewer.ScrollBarMaximum.Y);
					// 菜单列表自身的纵向滚动条：必须是 SubMenuScrollViewer 的模板后代
					// （排除搜索框内部 PART_ScrollViewer 的滚动条，后者隐藏且无 Thumb）
					ScrollBar scrollBar = scrollViewer.GetVisualDescendants().OfType<ScrollBar>()
						.FirstOrDefault(s => s.Orientation == Avalonia.Layout.Orientation.Vertical &&
							!s.GetVisualAncestors().OfType<ScrollViewer>().Any(sv => sv.Name == "PART_ScrollViewer"));
					Assert.True(scrollBar != null, "纵向 ScrollBar 应实现化");

					double before = scrollViewer.Offset.Y;
					// 触碰（按下即释放）滚动条：不应关菜单/收子菜单
					LeftPressRelease(scrollBar, fx.Window, new Point(scrollBar.Bounds.Width / 2, scrollBar.Bounds.Height / 4), moveDx: 0);
					Assert.True(fx.Menu.IsOpen, "触碰滚动条不应关闭右键菜单（用户缺陷：一点就消失）");
					Assert.True(fx.GroupItem.IsSubMenuOpen, "触碰滚动条不应收起分组子菜单");

					// 拖拽 Thumb：真实输入里按下即 Capture（headless 合成事件不走平台捕获路径，
					// 手动 Capture 等价模拟）。守卫修复前：首个 Move 被 ContextMenu 层
					// DefaultMenuInteractionHandler 的"越界 Capture(null)"HACK 掐断捕获
					// （实测 captured 由 Thumb 变空，滚动停在第一步）——"拉不了"的根因。
					Thumb thumb = scrollBar.GetVisualDescendants().OfType<Thumb>().FirstOrDefault();
					Assert.True(thumb != null, "纵向滚动条应有 Thumb");
					var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
					var properties = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
					Point start = new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2);
					thumb.RaiseEvent(new PointerPressedEventArgs(thumb, pointer, fx.Window, start, (ulong)Environment.TickCount64, properties, KeyModifiers.None));
					RunJobs();
					pointer.Capture(thumb);
					Assert.True(pointer.Captured == thumb, "拖拽开始时 Thumb 应持有指针捕获");
					for (int i = 1; i <= 8; i++)
					{
						thumb.RaiseEvent(new PointerEventArgs(
							InputElement.PointerMovedEvent, thumb, pointer, fx.Window,
							start.WithY(start.Y + i * 12), (ulong)Environment.TickCount64, properties, KeyModifiers.None));
						RunJobs();
					}
					Assert.True(pointer.Captured == thumb, "拖拽全程 Thumb 应保持指针捕获（守卫应拦下越界 Capture(null) HACK），实际 captured=" + (pointer.Captured?.GetType().Name ?? "<空>"));
					thumb.RaiseEvent(new PointerReleasedEventArgs(thumb, pointer, fx.Window, start.WithY(start.Y + 96), (ulong)Environment.TickCount64, properties, KeyModifiers.None, MouseButton.Left));
					RunJobs();

					Assert.True(fx.Menu.IsOpen, "拖拽滚动条过程中菜单不应关闭");
					Assert.True(fx.GroupItem.IsSubMenuOpen, "拖拽滚动条过程中子菜单不应收起");
					// 修复前只有第一步生效（≈150px）；修复后 8 步全程生效（≈1190px）。
					Assert.True(scrollViewer.Offset.Y > before + 300, "拖拽 Thumb 应全程滚动（拉不了=拖拽捕获被菜单交互处理器掐断），before=" + before + " after=" + scrollViewer.Offset.Y);
				}
				finally
				{
					fx.Menu.Close();
					fx.Window.Close();
				}
			});
		}

		// ===== 3) 焦点被抢到搜索框行上（MenuBase 首子项聚焦/菜单交互处理器按下抢焦）：
		//          守卫应把焦点还给搜索框（真实 Windows SendInput 实证的两条抢焦路径） =====

		[Fact]
		public void FocusStolenToRowItem_GuardRestoresSearchBoxFocus()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var fx = MenuFixture.Create(10);
				try
				{
					OpenSearchableSubmenu(fx);
					// 模拟 MenuBase 打开子菜单时把焦点给第一个子项（= 搜索框行 MenuItem）。
					// 真实 Windows 上菜单内部聚焦路径可达；headless 里显式允许行可聚焦后直接 Focus()。
					fx.SearchBoxItem.Focusable = true;
					Assert.True(fx.SearchBoxItem.Focus(), "前置条件：行应可被聚焦（模拟 MenuBase 抢焦点）");
					RunJobs();
					// 行的 GotFocus（类处理器标记 Handled，守卫以 handledEventsToo 订阅）触发守卫，
					// 守卫投递 Background 回调把焦点归还搜索框 —— 因此此处行的 IsFocused 已被归还动作清掉。
					Assert.True(fx.SearchBox.IsFocused, "守卫应把被抢走的焦点归还搜索框");
					// 守卫经 Background 投递把焦点还给搜索框
					RunJobs();
					Assert.True(fx.SearchBox.IsFocused, "焦点被抢到行上后守卫应把焦点还给搜索框（用户缺陷：搜索框无法获得焦点/无法输入）");
					Assert.True(fx.SearchBox.IsKeyboardFocused, "搜索框应持有键盘焦点");
					// 再次抢焦（点击搜索框时菜单交互处理器的抢焦路径）也应被守卫纠正
					fx.SearchBoxItem.Focus();
					RunJobs();
					RunJobs();
					Assert.True(fx.SearchBox.IsFocused, "二次抢焦后守卫应再次还焦");
				}
				finally
				{
					fx.Menu.Close();
					fx.Window.Close();
				}
			});
		}

		// ===== 4) 搜索框输入过滤：菜单保持打开、不匹配项隐藏 =====

		[Fact]
		public void TypeInSearchBox_FiltersBranches_MenuStaysOpen()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var fx = MenuFixture.Create(10);
				try
				{
					OpenSearchableSubmenu(fx);
					fx.SearchBox.Text = "branch-00";
					RunJobs();
					int visible = fx.GroupItem.Items.OfType<MenuItem>().Count(m => m.Header is string && ((string)m.Header).Contains("branch-00"));
					Assert.True(visible > 0, "过滤后应有匹配项可见");
					Assert.True(fx.Menu.IsOpen, "输入过滤过程中菜单不应关闭");
					Assert.True(fx.GroupItem.IsSubMenuOpen, "输入过滤过程中子菜单不应收起");
				}
				finally
				{
					fx.Menu.Close();
					fx.Window.Close();
				}
			});
		}

		// ===== 5) 宽度和谐：搜索框铺满整行、行宽一致、弹层宽度由最宽分支行驱动 =====
		// 用户缺陷（2026-09-18 二轮）："搜索框的宽度要和弹窗的宽度差不多"、"子菜单行右侧一大片留白"。
		// 根因：默认叶子行模板的图标列 + 手势列 + 两个 20px 槽位 ≈90px —— 搜索框只按自身 MinWidth
		// 渲染（不跟随行/弹层宽度），且搜索框行把弹层撑到 MinWidth+90px。搜索行专用主题
		// （SearchableSubmenuSearchRowMenuItem）去掉全部槽位并让 Header 铺满整行。

		[Fact]
		public void SearchBoxFillsSubmenuWidth_RowsUniform()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var fx = MenuFixture.Create(10);
				try
				{
					// 一条长分支名驱动弹层宽度超过搜索框 MinWidth 下限（200），
					// 验证弹层宽度由分支行内容驱动、搜索框跟随铺满。
					MenuItem longBranch = new MenuItem { Header = "remote/feature/an-extremely-long-branch-name-that-drives-submenu-width" };
					fx.GroupItem.Items.Add(longBranch);
					OpenSearchableSubmenu(fx);

					double boxWidth = fx.SearchBox.Bounds.Width;
					double searchRowWidth = fx.SearchBoxItem.Bounds.Width;
					double branchRowWidth = longBranch.Bounds.Width;
					// 搜索框铺满所在行（余量 = 行边框 2 + 框自身左右 Margin 8）
					Assert.True(boxWidth >= searchRowWidth - 12,
						"搜索框应铺满所在行：box=" + boxWidth.ToString("0") + " row=" + searchRowWidth.ToString("0"));
					// 各行等宽（菜单面板常规布局，搜索行不再额外撑宽或收窄）
					Assert.True(Math.Abs(searchRowWidth - branchRowWidth) <= 2,
						"搜索行应与分支行等宽：searchRow=" + searchRowWidth.ToString("0") + " branchRow=" + branchRowWidth.ToString("0"));
					// 弹层宽度由最宽分支行驱动：长分支名明显宽于搜索框 MinWidth 下限时，行宽应体现文本宽度而非 MinWidth+行铬
					Assert.True(branchRowWidth > 260,
						"长分支名应驱动行宽超过搜索框下限+行铬（branchRow=" + branchRowWidth.ToString("0") + "）");
					// 搜索框与弹层内容区宽度相当（弹层 SubMenuBorder 内缘 vs 搜索框）
					Popup popup = fx.GroupItem.GetVisualDescendants().OfType<Popup>()
						.FirstOrDefault(p => p.Name == "PART_Popup" && p.IsOpen);
					Assert.True(popup != null, "分组子菜单 Popup 应已打开");
					Border subMenuBorder = popup.Child.GetVisualDescendants().OfType<Border>()
						.FirstOrDefault(b => b.Name == "SubMenuBorder");
					Assert.True(subMenuBorder != null, "弹层应含 SubMenuBorder");
					Assert.True(subMenuBorder.Bounds.Width - boxWidth <= 20,
						"搜索框宽度应与弹窗宽度差不多：border=" + subMenuBorder.Bounds.Width.ToString("0") + " box=" + boxWidth.ToString("0"));
				}
				finally
				{
					fx.Menu.Close();
					fx.Window.Close();
				}
			});
		}
	}
}
