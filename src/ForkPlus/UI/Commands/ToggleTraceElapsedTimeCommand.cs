using Avalonia.Input;
using ForkPlus.Settings;

namespace ForkPlus.UI.Commands
{
	public class ToggleTraceElapsedTimeCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Trace Elapsed Time";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			bool flag = !ForkPlusSettings.Default.LogElapsedTime;
			Log.Info((flag ? "Enable" : "Disable") + " " + Title);
			ForkPlusSettings.Default.LogElapsedTime = flag;
		}
	}
}
