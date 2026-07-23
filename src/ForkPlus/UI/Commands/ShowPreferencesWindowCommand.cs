using System.Windows;
using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Services;
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
			ServiceLocator.WindowManager.InvalidateAndRefreshActiveRepositoryView(SubDomain.Revisions);
			// v3.0.4：设置变更后刷新 Undo/Redo 按钮可见性（开关可能被切换）
			// 阶段 3 备注：MainWindow.Toolbar 访问属另一处 View 耦合，留待 MainWindowViewModel 抽取。
			if (Application.Current.MainWindow is MainWindow mainWindow)
			{
				mainWindow.Toolbar?.RefreshUndoRedoVisibility();
			}
		}
	}
}
