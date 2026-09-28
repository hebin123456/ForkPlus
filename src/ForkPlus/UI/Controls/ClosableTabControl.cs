using System;
using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;
using ForkPlus.Settings;

namespace ForkPlus.UI.Controls
{
	public class ClosableTabControl : TabControl
	{
		// 关键：让隐式 ControlTheme `{x:Type controls:ClosableTabControl}` 能命中该控件
		//（Avalonia 默认 TabControl 的 StyleKey 可能仍是基类 TabControl）。
		protected override Type StyleKeyOverride => typeof(ClosableTabControl);

		private const string AddButton = "PART_Add";

		// 垂直模板（tabbar-left / tabbar-right class，见 Tabcontrol.axaml）里的标签条宿主。
		// 用于：模板应用后从设置恢复列宽 + 拖拽 Splitter 后把宽度写回设置。
		private const string TabStripHost = "PART_TabStripHost";

		// v4.x：垂直模板里的收拢/展开切换按钮（悬浮在分隔线上，见 Tabcontrol.axaml）。
		private const string TabStripToggle = "PART_TabStripToggle";

		// 收拢态标签条列宽 = 0（切换按钮在分隔列上，不占标签条空间）；展开态列宽下限
		//（与模板 MinWidth=120 一致，收拢期间代码放开到 0，展开时恢复）。
		private const double CollapsedStripWidth = 0.0;

		private const double MinStripWidth = 120.0;

		// 当前布局缓存（构造时从设置读取，ApplyTabBarLayout 更新）。
		private TabBarLayout _tabBarLayout = TabBarLayout.Top;

		// 垂直标签条收拢态（构造时从设置读取；toggle 点击翻转并写回设置）。
		private bool _tabStripCollapsed = ForkPlusSettings.Default.TabStripCollapsed;

		// 防止 OnApplyTemplate 恢复列宽与 SizeChanged 写回设置之间形成回环：
		// 程序化恢复宽度期间置 true，跳过写回。
		private bool _restoringStripWidth;

		// 模板重建竞态防护（2026-09-07，"切换主题导致 UI 崩溃"，详见
		// TabControlContentHostGuard 类注释）：本控件构造函数一次性赋值 Theme（不随
		// 皮肤字典换装更新，模板从不重建）暂不受影响，但一旦改用
		// Theme="{DynamicResource 具名key}" 即触发同款竞态——预防性接入。
		private ContentPresenter _trackedContentHost;

		protected override bool RegisterContentPresenter(ContentPresenter presenter)
		{
			bool handled = base.RegisterContentPresenter(presenter);
			if (handled)
			{
				_trackedContentHost = TabControlContentHostGuard.OnSelectedContentHostRegistered(
					_trackedContentHost, presenter);
			}
			return handled;
		}

		public EventHandler AddButtonClicked;

		public EventHandler TabItemRemoved;

		public EventHandler<EventArgs<ClosableTabItem>> SelectedTabItemChanged;

		// Migration note：WPF 模板里 UniformGrid IsItemsHost=True Rows=1（Avalonia Panel.IsItemsHost setter 为
		// internal，XAML 设置运行时 MethodAccessException）。改为 FuncTemplate 提供 items 面板，
		// 模板里 ItemsPresenter ItemsPanel={TemplateBinding ItemsPanel}。
		// Background 由 Tabcontrol.axaml 样式选择器（/template/ ItemsPresenter > UniformGrid）设置。
		public ClosableTabControl()
		{
			// 强制应用自定义 ControlTheme（避免回落到默认 TabControl 模板）。
			// 同时设置 ItemContainerTheme，确保 Tab header 一定用 ClosableTabItem 模板。
			ControlTheme controlTheme = null;
			if (Application.Current?.TryFindResource("ClosableTabControlTheme", out var themeByName) == true)
			{
				controlTheme = themeByName as ControlTheme;
			}
			if (controlTheme == null && Application.Current?.TryFindResource(typeof(ClosableTabControl), out var themeByType) == true)
			{
				controlTheme = themeByType as ControlTheme;
			}
			if (controlTheme != null)
			{
				base.Theme = controlTheme;
			}

			if (Application.Current?.TryFindResource("ClosableTabItemTheme", out var itemThemeByName) == true && itemThemeByName is ControlTheme itemTheme)
			{
				try { ItemContainerTheme = itemTheme; } catch { }
			}

			// v4.x：按设置应用标签条布局（顶部/左侧/右侧）——同时决定 ItemsPanel 横/竖排、
			// 模板 class（tabbar-left/tabbar-right 触发 Tabcontrol.axaml 里的垂直模板）。
			// 原 WPF 行为 = UniformGrid Rows=1 固定横排，现统一收敛到 ApplyTabBarLayout。
			ApplyTabBarLayout(ForkPlusSettings.Default.TabBarLayout);
			// Migration note：WPF TabControl.OnSelectionChanged 是框架调用的虚方法重写，迁移后降级为
			// 无调用的普通方法（Avalonia 无此虚方法）→ SelectedTabItemChanged 永不触发 →
			// TabManager.TabControl_SelectedTabItemChanged（排队仓库刷新任务）整条链路断裂，
			// 打开仓库后永远停在"正在加载..."。改为订阅 Avalonia SelectionChanged 路由事件，
			// 转发到原 OnSelectionChanged 逻辑（保留 StopSelectionChangedEventWhileDropInProgress 门控）。
			base.SelectionChanged += delegate(object sender, SelectionChangedEventArgs e)
			{
				OnSelectionChanged(e);
			};
		}

		/// <summary>
		/// 应用标签条布局：顶部（水平，默认）/ 左侧 / 右侧（垂直）。
		/// 1) 设置模板 class → Tabcontrol.axaml 里 tabbar-left/tabbar-right 命中垂直 ControlTemplate；
		/// 2) 切换 ItemsPanel（UniformGrid Rows=1 ↔ 纵向 StackPanel，标签随条宽横向拉伸）；
		/// 3) 同步所有已存在标签页的 "vertical" class（新建标签由 ClosableTabItem 构造函数自理）。
		/// </summary>
		public void ApplyTabBarLayout(TabBarLayout layout)
		{
			_tabBarLayout = layout;
			Classes.Remove("tabbar-left");
			Classes.Remove("tabbar-right");
			if (layout == TabBarLayout.Left)
			{
				Classes.Add("tabbar-left");
			}
			else if (layout == TabBarLayout.Right)
			{
				Classes.Add("tabbar-right");
			}
			// 收拢 class 仅在垂直布局下有意义（顶部模板无切换按钮，收拢态遗留 class 会让
			// 切回垂直时模板隐藏逻辑误触发）→ Top 一律移除，Left/Right 且已收拢才补回。
			// 模板 class 变化触发重新套模板 → OnApplyTemplate 里 ApplyTabStripCollapseState
			// 会按收拢态恢复列宽（18px）或持久化宽度。
			Classes.Remove("tabbar-collapsed");
			if (layout != TabBarLayout.Top && _tabStripCollapsed)
			{
				Classes.Add("tabbar-collapsed");
			}
			if (layout == TabBarLayout.Top)
			{
				ItemsPanel = new global::Avalonia.Controls.Templates.FuncTemplate<global::Avalonia.Controls.Panel>(
					() => new global::Avalonia.Controls.Primitives.UniformGrid { Rows = 1 });
			}
			else
			{
				// 垂直条用 StackPanel（非 UniformGrid——UniformGrid Columns=1 会把可用高度均分，
				// 标签被拉高失真）；StackPanel 纵排下子项默认横向拉伸到条宽。
				ItemsPanel = new global::Avalonia.Controls.Templates.FuncTemplate<global::Avalonia.Controls.Panel>(
					() => new global::Avalonia.Controls.StackPanel { Orientation = global::Avalonia.Layout.Orientation.Vertical });
			}
			foreach (object item in base.Items)
			{
				if (item is ClosableTabItem tab)
				{
					tab.Classes.Remove("vertical");
					if (layout != TabBarLayout.Top)
					{
						tab.Classes.Add("vertical");
					}
				}
			}
		}

		[Null]
		public ClosableTabItem SelectedTab => base.SelectedItem as ClosableTabItem;

		public bool StopSelectionChangedEventWhileDropInProgress { get; set; }

		public void AddTab(ClosableTabItem tab)
		{
			base.Items.Add(tab);
		}

		public void RemoveTab(ClosableTabItem tab)
		{
			if (tab.IsSelected)
			{
				int num = base.SelectedIndex - 1;
				if (num >= 0)
				{
					base.SelectedIndex = num;
				}
			}
			base.Items.Remove(tab);
			TabItemRemoved?.Invoke(this, null);
			if (base.Items.Count == 0)
			{
				MainWindow.Commands.NewTab.Execute();
			}
		}

		public void RemoveAllTabs(ClosableTabItem exceptItem = null)
		{
			ClosableTabItem closableTabItem = null;
			ClosableTabItem[] array = base.Items.CompactMap((object x) => x as ClosableTabItem);
			if (exceptItem != null)
			{
				exceptItem.IsSelected = true;
			}
			else
			{
				closableTabItem = new ClosableTabItem();
				base.Items.Add(closableTabItem);
				closableTabItem.IsSelected = true;
			}
			foreach (ClosableTabItem closableTabItem2 in array)
			{
				if (exceptItem != closableTabItem2)
				{
					base.Items.Remove(closableTabItem2);
				}
			}
			if (closableTabItem != null)
			{
				base.Items.Remove(closableTabItem);
			}
			TabItemRemoved?.Invoke(this, null);
			if (base.Items.Count == 0)
			{
				MainWindow.Commands.NewTab.Execute();
			}
		}

		protected override void OnApplyTemplate(global::Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);
			if (this.GetTemplateChild("PART_Add") is Button button)
			{
				button.Click += AddButton_Clicked;
			}
			// 垂直模板：恢复持久化的标签条宽度，并监听 Splitter 拖拽后的尺寸变化写回设置。
			// 顶部（水平）模板无 PART_TabStripHost，此处自然跳过。
			if (this.GetTemplateChild("PART_TabStripHost") is global::Avalonia.Controls.Control stripHost)
			{
				stripHost.SizeChanged -= TabStripHost_SizeChanged;
				stripHost.SizeChanged += TabStripHost_SizeChanged;
			}
			// v4.x：收拢/展开切换按钮（模板可能因布局 class 切换而重建，先解绑防重复订阅）。
			if (this.GetTemplateChild(TabStripToggle) is Button toggle)
			{
				toggle.Click -= TabStripToggle_Clicked;
				toggle.Click += TabStripToggle_Clicked;
			}
			// 按收拢态恢复列宽（收拢 → 18px 且放开 MinWidth；展开 → 持久化宽度）+ 箭头方向。
			// 布局切换（Left↔Right）重建模板后由此统一收敛，避免旧模板列宽残留。
			ApplyTabStripCollapseState();
		}

		/// <summary>
		/// v4.x：把当前收拢态同步到 UI——1) tabbar-collapsed class（模板侧隐藏 + 按钮/标签
		/// 列表/Splitter）；2) 标签条列宽（收拢 = CollapsedStripWidth 且 MinWidth 放开到 0，
		/// 展开 = 持久化的 TabStripWidth 且 MinWidth 恢复 120——模板 XAML 的 MinWidth=120
		/// 会钳住 18px，必须代码同步放开）；3) 箭头方向（见 UpdateTabStripToggleGlyph）。
		/// 幂等：OnApplyTemplate / toggle 点击 / 布局切换均调用。
		/// </summary>
		private void ApplyTabStripCollapseState()
		{
			bool vertical = _tabBarLayout != TabBarLayout.Top;
			bool collapsed = vertical && _tabStripCollapsed;
			Classes.Remove("tabbar-collapsed");
			if (collapsed)
			{
				Classes.Add("tabbar-collapsed");
			}
			if (this.GetTemplateChild(TabStripHost) is global::Avalonia.Controls.Control stripHost && stripHost.Parent is global::Avalonia.Controls.Grid layoutGrid)
			{
				int stripColumn = global::Avalonia.Controls.Grid.GetColumn(stripHost);
				if (stripColumn >= 0 && stripColumn < layoutGrid.ColumnDefinitions.Count)
				{
					global::Avalonia.Controls.ColumnDefinition stripColumnDefinition = layoutGrid.ColumnDefinitions[stripColumn];
					_restoringStripWidth = true;
					try
					{
						if (collapsed)
						{
							// 模板 MinWidth=120 会钳住收拢宽度 → 先放开再压宽。
							stripColumnDefinition.MinWidth = 0.0;
							stripColumnDefinition.Width = new global::Avalonia.Controls.GridLength(CollapsedStripWidth);
						}
						else
						{
							stripColumnDefinition.MinWidth = MinStripWidth;
							stripColumnDefinition.Width = new global::Avalonia.Controls.GridLength(ForkPlusSettings.Default.TabStripWidth);
						}
					}
					finally
					{
						_restoringStripWidth = false;
					}
				}
			}
			UpdateTabStripToggleGlyph();
		}

		/// <summary>
		/// 箭头指向 = 收拢动作方向：展开态箭头指向即将收拢的一侧（Left 布局 ◀ / Right 布局 ▶），
		/// 收拢态指向即将展开的一侧（Left 布局 ▶ / Right 布局 ◀）——即 (layout==Left) XOR collapsed。
		/// 实现方式 = 切换按钮上的 point-right class（TabStripToggleStyle 内
		/// ^.point-right /template/ Path#chevron 换几何资源；默认朝左）。不用代码直接改
		/// Path.Data：嵌套模板内容查找不可靠（右侧布局箭头停留在默认左箭头的实际缺陷），
		/// class + 主题样式与 AddButtonStyle 的 plus 同款机制。
		/// </summary>
		private void UpdateTabStripToggleGlyph()
		{
			if (this.GetTemplateChild(TabStripToggle) is Button toggle)
			{
				bool pointLeft = (_tabBarLayout == TabBarLayout.Left) != _tabStripCollapsed;
				toggle.Classes.Remove("point-right");
				if (!pointLeft)
				{
					toggle.Classes.Add("point-right");
				}
			}
		}

		private void TabStripToggle_Clicked(object sender, RoutedEventArgs e)
		{
			_tabStripCollapsed = !_tabStripCollapsed;
			ForkPlusSettings.Default.TabStripCollapsed = _tabStripCollapsed;
			// 收拢期间 SizeChangedEventArgs.NewSize=18 不能覆盖 TabStripWidth
			//（展开时恢复的就是它），故收拢态在 SizeChanged 里跳过写回。
			ApplyTabStripCollapseState();
			ForkPlusSettings.Default.Save();
		}

		/// <summary>
		/// Splitter 拖拽 / 布局变化后，把标签条实际宽度写回设置（属性 setter 负责钳制范围）。
		/// 持久化时机：命令切换布局时 Save() + Window_Closing Save()，这里只更新内存值。
		/// </summary>
		// 修复（2026-09-28）：SizeChangedEventArgs 在 Avalonia 12 位于 Avalonia.Controls 命名空间
		//（同 MergeCodeEditor.OnSizeChanged / MainWindow_SizeChanged 的既有用法；
		// 原 global::Avalonia.Layout 编译报 CS0234）。
		private void TabStripHost_SizeChanged(object sender, global::Avalonia.Controls.SizeChangedEventArgs e)
		{
			// 收拢态：列宽由代码压到 18px，此变化不属于用户拖拽，不能写回 TabStripWidth
			//（展开时恢复的正是该持久化值）。
			if (_restoringStripWidth || _tabStripCollapsed)
			{
				return;
			}
			if (_tabBarLayout != TabBarLayout.Top)
			{
				ForkPlusSettings.Default.TabStripWidth = System.Math.Round(e.NewSize.Width);
			}
		}

		public void SelectTab(ClosableTabItem itemToSelect)
		{
			base.SelectedItem = itemToSelect;
		}

		public void SelectNextTab()
		{
			int num = base.SelectedIndex + 1;
			if (num == base.Items.Count)
			{
				num = 0;
			}
			base.SelectedIndex = num;
		}

		public void SelectPreviousTab()
		{
			int num = base.SelectedIndex - 1;
			if (num == -1)
			{
				num = base.Items.Count - 1;
			}
			base.SelectedIndex = num;
		}

		public void InsertAt(ClosableTabItem item, int index)
		{
			base.Items.Insert(index, item);
		}

		public int IndexOf(ClosableTabItem item)
		{
			return base.Items.IndexOf(item);
		}

		protected void OnSelectionChanged(SelectionChangedEventArgs e)
		{
			if (!StopSelectionChangedEventWhileDropInProgress)
			{
				SelectedTabItemChanged?.Invoke(this, new EventArgs<ClosableTabItem>(base.SelectedItem as ClosableTabItem));
			}
		}

		private void AddButton_Clicked(object sender, RoutedEventArgs e)
		{
			AddButtonClicked?.Invoke(this, EventArgs.Empty);
		}
	}
}
