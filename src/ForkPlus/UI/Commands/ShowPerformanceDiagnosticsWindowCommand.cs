using Avalonia.Input;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.Commands
{
	public class ShowPerformanceDiagnosticsWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Name => "Performance Diagnostics";

		public string Title => "Performance Diagnostics";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			new PerformanceDiagnosticsWindow().ShowDialog();
		}
	}
}
