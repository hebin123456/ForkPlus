using System;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;

namespace ForkPlus.UI.Controls
{
	/// <summary>
	/// Migration note（根因）：WPF 原实现 override OnChecked/OnUnchecked（WPF ToggleButton 的虚方法），
	/// Avalonia ToggleButton 无这两个虚方法 → 方法从未被调用，下拉菜单永远打不开。
	/// Avalonia 对应虚方法为 OnIsCheckedChanged（12.1.1 实证 virtual），在此打开/关闭 ContextMenu。
	/// </summary>
	public class DropDownButton : ToggleButton
	{
		protected override void OnIsCheckedChanged(RoutedEventArgs e)
		{
			base.OnIsCheckedChanged(e);
			if (base.IsChecked == true)
			{
				// Migration note（2026-09-07 修复，"外观/工作区按钮无下拉"）：真实点击序 =
				// ToggleButton.OnClick 先切 IsChecked（本回调）→ 再同步 raise Click 路由事件
				// （UiClick.cs 与 E2e07 注释实证；宿主常在 Click 处理器里构建菜单项，如
				// ToolbarUserControl 的 InitializeAppearanceToolBarButtonContextMenu）。若在此立即
				// Open()，打开的是空菜单（axaml 里 <p:ContextMenu/> 无项），而 Avalonia 已打开的
				// ContextMenu 不会为后加入的 Items 重新物化/渲染弹层（WPF 会，headless 复现实证
				// popupRoot 内 MenuItem=0），用户看到"点了没下拉"。Undo/Stash 按钮正常是因为它们在
				// Opened 事件（Open 调用栈内、布局未跑）里构建项。修复 = 延迟一个 dispatcher 周期再
				// Open，让同一交互内紧随其后的 Click 路由事件先完成菜单项构建（对 Opened 构建型
				// 按钮时序不变，仅晚一拍打开）。
				Avalonia.Threading.Dispatcher.UIThread.Post(delegate
				{
					if (base.IsChecked == true)
					{
						OpenDropdown();
					}
				}, Avalonia.Threading.DispatcherPriority.Background);
			}
			else
			{
				CloseDropdown();
			}
		}

		private void OpenDropdown()
		{
			ContextMenu contextMenu = base.ContextMenu;
			if (contextMenu == null || contextMenu.IsOpen)
			{
				return;
			}
			// 兼容 WPF 行为：下拉菜单宽度至少与触发按钮等宽（原版不会因某个长文本把菜单撑得很宽）。
			// 这里用 MinWidth，既保证等宽，又避免某些菜单需要更宽时被硬裁剪。
			contextMenu.MinWidth = Math.Max(contextMenu.MinWidth, Bounds.Width);
			contextMenu.PlacementTarget = this;
			contextMenu.Placement = PlacementMode.Bottom;

			global::ForkPlus.UI.WpfCompat.ContextMenuCompat.AttachAutoDismiss(contextMenu, this);
			contextMenu.Closed -= ContextMenu_Closed;
			contextMenu.Closed += ContextMenu_Closed;
			contextMenu.Open();
		}

		private void CloseDropdown()
		{
			ContextMenu contextMenu = base.ContextMenu;
			if (contextMenu == null)
			{
				return;
			}
			contextMenu.Closed -= ContextMenu_Closed;
			contextMenu.Close();
		}

		/// <summary>菜单关闭（选中项/点击别处/按 Esc）后复位按钮选中态，保证再次点击能重新打开。</summary>
		private void ContextMenu_Closed(object sender, RoutedEventArgs e)
		{
			if (sender is ContextMenu contextMenu)
			{
				contextMenu.Closed -= ContextMenu_Closed;
			}
			SetCurrentValue(IsCheckedProperty, false);
		}
	}
}
