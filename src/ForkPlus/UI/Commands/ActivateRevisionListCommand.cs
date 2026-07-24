using Avalonia.Input;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ActivateRevisionListCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Show All Commits";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.D2, KeyModifiers.Control);


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
				else
				{
					activeRepositoryUserControl.SelectAndScrollIntoView(new RevisionSelector.Head());
				}
			}
		}
	}
}
