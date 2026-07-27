using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace ForkPlus.UI.Controls
{
	public class DropDownButton : ToggleButton
	{
		private bool _contextMenuHandlerAttached;

		/// <summary>
		/// OnChecked 在 IsChecked 从 false→true 时触发（由 ToggleButton.OnClick→Toggle() 调用，
		/// 在 Click 事件之后）。此时打开 ContextMenu。
		/// 原实现有两个 bug：
		/// 1. 每次都 `+= ContextMenu_Closed`，多次打开会累积多个处理器。
		///    修复：用 _contextMenuHandlerAttached 标记确保只挂一次。
		/// 2. 末尾 `IsChecked = true` 冗余（已经是 true 才会进 OnChecked），且可能触发额外的
		///    属性变更通知。移除。
		/// </summary>
		protected override void OnChecked(RoutedEventArgs e)
		{
			base.OnChecked(e);
			if (base.ContextMenu == null)
			{
				return;
			}
			base.ContextMenu.PlacementTarget = this;
			base.ContextMenu.Placement = PlacementMode.Bottom;
			if (!_contextMenuHandlerAttached)
			{
				base.ContextMenu.Closed += ContextMenu_Closed;
				_contextMenuHandlerAttached = true;
			}
			base.ContextMenu.Open();
		}

		protected override void OnUnchecked(RoutedEventArgs e)
		{
			base.OnUnchecked(e);
			if (base.ContextMenu == null)
			{
				return;
			}
			if (_contextMenuHandlerAttached)
			{
				base.ContextMenu.Closed -= ContextMenu_Closed;
				_contextMenuHandlerAttached = false;
			}
			base.ContextMenu.Close();
		}

		private void ContextMenu_Closed(object sender, RoutedEventArgs e)
		{
			// 菜单关闭后取消勾选状态（用户点击菜单外或选择菜单项后）。
			// 设置 IsChecked=false 会触发 OnUnchecked，但此时菜单已关闭，
			// OnUnchecked 内的 Close() 是 no-op，不会重复关闭。
			base.IsChecked = false;
		}
	}
}
