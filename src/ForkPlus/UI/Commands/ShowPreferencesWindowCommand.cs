using System.Windows;
using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.Commands
{
	public class ShowPreferencesWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title { get; } = "Preferences...";


		public KeyGesture Shortcut => new KeyGesture(Key.OemComma, ModifierKeys.Control);

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			new PreferencesWindow().ShowDialog();
			Application.Current.ActiveRepositoryUserControl()?.InvalidateAndRefresh(SubDomain.Revisions);
		}
	}
}
