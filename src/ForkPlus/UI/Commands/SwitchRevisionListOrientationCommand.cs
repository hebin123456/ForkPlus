using Avalonia;
using Avalonia.Input;
using ForkPlus.Settings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

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
