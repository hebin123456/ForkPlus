using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class OpenWorktreeCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Open Worktree In New Tab";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule, Worktree[] worktrees)
		{
			for (int i = 0; i < worktrees.Length; i++)
			{
				Worktree worktree = worktrees[i];
				if (!MainWindow.Instance.TabManager.OpenRepository(worktree.Path, gitModule))
				{
					GitCommandResult<GitModule> gitCommandResult = new OpenGitRepositoryGitCommand().Execute(worktree.Path);
					if (gitCommandResult.Error is GitCommandError.UnsafeRepository)
					{
						new ErrorWindow(repositoryUserControl, gitCommandResult.Error).ShowDialog();
					}
				}
			}
		}
	}
}
