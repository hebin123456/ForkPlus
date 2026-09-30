using Avalonia.Input;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.Commands
{
	/// <summary>帮助菜单"Getting Started"：手动重看新手引导向导。不改变 OnboardingCompleted 标志（自动弹出只看首启判定）。</summary>
	public class ShowOnboardingTourCommand : IUICommand, IForkPlusCommand
	{
		public string Title { get; } = "Getting Started";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			new OnboardingTourWindow().ShowDialog();
		}
	}
}
