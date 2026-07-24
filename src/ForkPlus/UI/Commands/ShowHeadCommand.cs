using Avalonia.Input;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowHeadCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Show HEAD";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.D0, KeyModifiers.Control);


		public KeyGesture SecondaryShortcut { get; }

		public void Execute()
		{
			RepositoryUserControl activeRepositoryUserControl = MainWindow.ActiveRepositoryUserControl;
			if (activeRepositoryUserControl != null)
			{
				if (activeRepositoryUserControl.ViewMode != 0)
				{
					activeRepositoryUserControl.ActivateRevisionView();
				}
				activeRepositoryUserControl.SelectAndScrollIntoView(new RevisionSelector.Head());
				activeRepositoryUserControl.SidebarRevealActiveBranch();
			}
		}
	}
}
