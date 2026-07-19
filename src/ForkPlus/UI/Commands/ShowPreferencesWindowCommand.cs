using System.Windows;
using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

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
			// v3.0.4：设置变更后刷新 Undo/Redo 按钮可见性（开关可能被切换）
			if (Application.Current.MainWindow is MainWindow mainWindow)
			{
				mainWindow.Toolbar?.RefreshUndoRedoVisibility();
			}
		}
	}
}
