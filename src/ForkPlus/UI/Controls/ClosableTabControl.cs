using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;

namespace ForkPlus.UI.Controls
{
	public class ClosableTabControl : TabControl
	{
		// 关键：让隐式 ControlTheme `{x:Type controls:ClosableTabControl}` 能命中该控件
		//（Avalonia 默认 TabControl 的 StyleKey 可能仍是基类 TabControl）。
		protected override Type StyleKeyOverride => typeof(ClosableTabControl);

		private const string AddButton = "PART_Add";

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

			ItemsPanel = new global::Avalonia.Controls.Templates.FuncTemplate<global::Avalonia.Controls.Panel>(
				() => new global::Avalonia.Controls.Primitives.UniformGrid { Rows = 1 });
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
