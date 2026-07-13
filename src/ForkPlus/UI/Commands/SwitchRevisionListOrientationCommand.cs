using System.Windows;
using System.Windows.Input;
using ForkPlus.Settings;

namespace ForkPlus.UI.Commands
{
	public class SwitchRevisionListOrientationCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Switch orientation";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			RevisionListOrientation newLayout = ((ForkPlusSettings.Default.RevisionListOrientation != RevisionListOrientation.Horizontal) ? RevisionListOrientation.Horizontal : RevisionListOrientation.Vertical);
			Execute(newLayout);
		}

		public void Execute(RevisionListOrientation newLayout)
		{
			ForkPlusSettings.Default.RevisionListOrientation = newLayout;
			NotificationCenter.Current.RaiseRevisionListOrientatioChanged(this, newLayout);
			Application.Current.ActiveRepositoryUserControl()?.ActivateRevisionView();
		}
	}
}
