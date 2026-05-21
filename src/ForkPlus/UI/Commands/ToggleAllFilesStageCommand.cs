using System.Windows.Input;

namespace ForkPlus.UI.Commands
{
	public class ToggleAllFilesStageCommand : ToggleFileStageCommand
	{
		public override KeyGesture Shortcut { get; } = new KeyGesture(Key.S, ModifierKeys.Alt | ModifierKeys.Control | ModifierKeys.Shift);


		public override KeyGesture SecondaryShortcut => null;
	}
}
