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
			new AiCodeReviewWindow(repositoryUserControl, target, aiAgent).Show();
		}
	}
}
