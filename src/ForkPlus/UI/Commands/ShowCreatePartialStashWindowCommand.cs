using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowCreatePartialStashWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Stash Files...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, ChangedFile[] filesToStash)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			ChangedFile[] array = repositoryUserControl.RepositoryStatus?.ChangedFiles;
			if (array == null)
			{
				return;
			}
			CreatePartialStashWindow createPartialStashWindow = new CreatePartialStashWindow(gitModule, filesToStash, array);
			if (createPartialStashWindow.ShowDialog().GetValueOrDefault())
			{
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Revisions | SubDomain.Stashes, null, RepositoryViewMode.CommitViewMode);
				if (!createPartialStashWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, createPartialStashWindow.GitResult.Error).ShowDialog();
				}
			}
		}
	}
}
