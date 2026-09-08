using Avalonia.Input;

namespace ForkPlus.UI.Commands
{
	public class ToggleAllFilesStageCommand : ToggleFileStageCommand
	{
		public override KeyGesture Shortcut { get; } = new KeyGesture(Key.S, global::Avalonia.Input.KeyModifiers.Alt | global::Avalonia.Input.KeyModifiers.Control | global::Avalonia.Input.KeyModifiers.Shift);


		public override KeyGesture SecondaryShortcut => null;
	}
}
