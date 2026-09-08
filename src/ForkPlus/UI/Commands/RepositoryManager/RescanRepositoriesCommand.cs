using Avalonia.Input;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands.RepositoryManager
{
	public class RescanRepositoriesCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Rescan Repositories...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryManagerUserControl repositoryManager)
		{
			if (new RescanRepositoriesWindow().ShowDialog().GetValueOrDefault())
			{
				repositoryManager.Refresh(restoreSelection: false);
			}
		}
	}
}
