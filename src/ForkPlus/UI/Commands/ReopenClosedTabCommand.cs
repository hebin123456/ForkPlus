using Avalonia;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Commands
{
	/// <summary>
	/// 重开最近关闭的仓库标签（Ctrl+Shift+T，浏览器/IDE 习惯键位）。
	/// v4.2.1 新增：TabManager 维护已关闭标签路径栈；栈空时静默返回。
	/// （原 New Tag 的 Ctrl+Shift+T 让位，后者移至 Ctrl+Shift+G。）
	/// </summary>
	public class ReopenClosedTabCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Reopen Closed Tab";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.T, global::Avalonia.Input.KeyModifiers.Control | global::Avalonia.Input.KeyModifiers.Shift);


		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			((global::Avalonia.Application.Current?.ApplicationLifetime as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow as MainWindow).TabManager.ReopenClosedTab();
		}
	}
}
