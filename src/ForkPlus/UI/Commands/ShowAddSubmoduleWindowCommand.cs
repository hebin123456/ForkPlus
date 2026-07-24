using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowAddSubmoduleWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Add New Submodule...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule, SubmodulesToUpdate submodulesToUpdate)
		{
			AddSubmoduleWindow addSubmoduleWindow = new AddSubmoduleWindow(gitModule, submodulesToUpdate);
			if (addSubmoduleWindow.ShowDialog().GetValueOrDefault())
			{
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Submodules);
				if (!addSubmoduleWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, addSubmoduleWindow.GitResult.Error).ShowDialog();
				}
			}
		}
	}
}
