using System.Windows;
using System.Windows.Input;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ToggleReferenceFilterCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Filter by Active Branch";

		public KeyGesture Shortcut => new KeyGesture(Key.A, ModifierKeys.Control | ModifierKeys.Shift);

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			RepositoryUserControl repositoryUserControl = Application.Current.ActiveRepositoryUserControl();
			if (repositoryUserControl != null)
			{
				RepositoryUserControl.Commands.UpdateReferenceFilter.ToggleActiveBranchFilter(repositoryUserControl);
			}
		}
	}
}
