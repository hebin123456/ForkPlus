using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowPushMultipleTagsWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Push Tags...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, Tag[] tags, [Null] Remote remote)
		{
			new PushMultipleTagsWindow(repositoryUserControl, tags, remote).ShowDialog();
		}
	}
}
