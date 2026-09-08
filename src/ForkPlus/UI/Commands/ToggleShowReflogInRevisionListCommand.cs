using Avalonia;
using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.UserControls;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Commands
{
	public class ToggleShowReflogInRevisionListCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Show Lost Commits (Reflog)";

		public KeyGesture Shortcut => new KeyGesture(Key.OemPeriod, global::Avalonia.Input.KeyModifiers.Control | global::Avalonia.Input.KeyModifiers.Shift);

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			RepositoryUserControl repositoryUserControl = Application.Current.ActiveRepositoryUserControl();
			if (repositoryUserControl != null)
			{
				if (repositoryUserControl.ViewMode == RepositoryViewMode.CommitViewMode)
				{
					repositoryUserControl.Content.CommitUserControl.ToggleShowIgnoredFiles();
					return;
				}
				repositoryUserControl.ShowReflogInRevisionList = !repositoryUserControl.ShowReflogInRevisionList;
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Revisions);
			}
		}
	}
}
