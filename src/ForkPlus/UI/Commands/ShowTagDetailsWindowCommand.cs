using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowTagDetailsWindowCommand : IUICommand, IForkPlusCommand, IPaletteCommand
	{
		public string Title => "Show Annotated Tag Details...";

		public KeyGesture Shortcut { get; }

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, Tag tag)
		{
			new TagDetailsWindow(repositoryUserControl.GitModule, tag).ShowDialog();
		}
	}
}
