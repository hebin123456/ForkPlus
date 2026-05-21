using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ToggleCollapseAllMergeRevisionsCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Collapse All Merges (Show First Parent)";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			RepositoryUserControl activeRepositoryUserControl = MainWindow.ActiveRepositoryUserControl;
			if (activeRepositoryUserControl == null)
			{
				return;
			}
			GitModule gitModule = activeRepositoryUserControl.GitModule;
			if (gitModule != null)
			{
				if (!gitModule.Settings.CollapseAllMergeRevisions)
				{
					activeRepositoryUserControl.CollapseAllMerges();
				}
				else
				{
					activeRepositoryUserControl.ExpandAllMerges();
				}
				activeRepositoryUserControl.InvalidateAndRefresh(SubDomain.GitFlowSettings);
			}
		}
	}
}
