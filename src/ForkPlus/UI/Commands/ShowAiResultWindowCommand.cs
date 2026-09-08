using Avalonia.Input;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowAiResultWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Ai Result...";

		public KeyGesture Shortcut { get; }

		public KeyGesture SecondaryShortcut { get; }

		public void Execute(RepositoryUserControl repositoryUserControl, AiCodeReviewTarget target, [Null] AiAgent aiAgent = null)
		{
			// 修复链 23：非模态窗口用 ShowAtOwnerScreen 跟随主窗口所在屏幕
			// （原 Show() + 构造函数 CenterScreen 会把窗口甩到主显示器）。
			new AiCodeReviewWindow(repositoryUserControl, target, aiAgent).ShowAtOwnerScreen();
		}
	}
}
