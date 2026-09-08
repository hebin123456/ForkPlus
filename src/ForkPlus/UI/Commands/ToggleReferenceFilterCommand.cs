using Avalonia;
using Avalonia.Input;
using ForkPlus.UI.UserControls;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Commands
{
	public class ToggleReferenceFilterCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Filter by Active Branch";

		public KeyGesture Shortcut => new KeyGesture(Key.A, global::Avalonia.Input.KeyModifiers.Control | global::Avalonia.Input.KeyModifiers.Shift);

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
