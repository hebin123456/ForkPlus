using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowAddGitIgnorePatternWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Ignore";

		public KeyGesture Shortcut { get; }

		public KeyGesture SecondaryShortcut { get; }

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule, string initialPattern)
		{
			AddGitIgnorePatternWindow addGitIgnorePatternWindow = new AddGitIgnorePatternWindow(gitModule, initialPattern);
			if (addGitIgnorePatternWindow.ShowDialog().GetValueOrDefault())
			{
				if (!addGitIgnorePatternWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, addGitIgnorePatternWindow.GitResult.Error).ShowDialog();
				}
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Status, null, RepositoryViewMode.CommitViewMode);
			}
		}
	}
}
