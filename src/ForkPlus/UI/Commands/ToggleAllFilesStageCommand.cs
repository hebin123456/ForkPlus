using Avalonia.Input;

namespace ForkPlus.UI.Commands
{
	public class ToggleAllFilesStageCommand : ToggleFileStageCommand
	{
		public override KeyGesture Shortcut { get; } = new KeyGesture(Key.S, KeyModifiers.Alt | KeyModifiers.Control | KeyModifiers.Shift);


		public override KeyGesture SecondaryShortcut => null;
	}
}
