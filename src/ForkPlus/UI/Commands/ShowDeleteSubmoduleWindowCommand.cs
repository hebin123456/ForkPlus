using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowDeleteSubmoduleWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Delete...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule, [Null] Submodule submodule)
		{
			if (submodule == null)
			{
				return;
			}
			DeleteSubmoduleWindow deleteSubmoduleWindow = new DeleteSubmoduleWindow(gitModule, submodule);
			if (deleteSubmoduleWindow.ShowDialog().GetValueOrDefault())
			{
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Submodules);
				if (!deleteSubmoduleWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, deleteSubmoduleWindow.GitResult.Error).ShowDialog();
				}
			}
		}
	}
}
