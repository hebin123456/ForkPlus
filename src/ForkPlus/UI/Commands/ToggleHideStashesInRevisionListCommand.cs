using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ToggleHideStashesInRevisionListCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Hide Stashes in Commit List";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			RepositoryUserControl activeRepositoryUserControl = MainWindow.ActiveRepositoryUserControl;
			if (activeRepositoryUserControl != null)
			{
				GitModule gitModule = activeRepositoryUserControl.GitModule;
				if (gitModule != null)
				{
					gitModule.Settings.HideStashesInRevisionList = !gitModule.Settings.HideStashesInRevisionList;
					gitModule.Settings.Save();
					activeRepositoryUserControl.InvalidateAndRefresh(SubDomain.References);
				}
			}
		}
	}
}
