using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowAddGitignoreTemplateWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Add .gitignore…";

		public KeyGesture Shortcut { get; }

		public KeyGesture SecondaryShortcut { get; }

		public void Execute(RepositoryUserControl repositoryUserControl)
		{
			string[] untrackedFiles = repositoryUserControl.RepositoryStatus?.ChangedFiles.Filter((ChangedFile x) => !x.Tracked).Map((ChangedFile x) => x.Path) ?? new string[0];
			if (new AddGitignoreTemplateWindow(repositoryUserControl, untrackedFiles).ShowDialog().GetValueOrDefault())
			{
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Status, null, RepositoryViewMode.CommitViewMode);
			}
		}
	}
}
