using Avalonia;
using Avalonia.Input;
using ForkPlus.Settings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Commands
{
	/// <summary>
	/// 切换主窗口仓库标签条布局（顶部/左侧/右侧）。v4.x 新增。
	/// 与 SwitchRevisionListOrientationCommand 同模式：写设置 → 广播事件 → 立即应用到主窗口。
	/// </summary>
	public class SwitchTabBarLayoutCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Tab Layout";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			// 布局是三态（Top/Left/Right），无参调用无 toggle 语义，默认切到顶部。
			Execute(TabBarLayout.Top);
		}

		public void Execute(TabBarLayout newLayout)
		{
			if (ForkPlusSettings.Default.TabBarLayout == newLayout)
			{
				return;
			}
			ForkPlusSettings.Default.TabBarLayout = newLayout;
			// 布局切换是低频操作，立即落盘（同时 Window_Closing 兜底再存一次）。
			ForkPlusSettings.Default.Save();
			NotificationCenter.Current.RaiseTabBarLayoutChanged(this, newLayout);
			MainWindow.Instance?.ApplyTabBarLayout();
		}
	}
}
