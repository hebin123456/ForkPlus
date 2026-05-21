using System.Windows.Input;

namespace ForkPlus.UI.Commands
{
	public interface IUICommand : IForkPlusCommand
	{
		string Title { get; }

		KeyGesture Shortcut { get; }

		KeyGesture SecondaryShortcut { get; }
	}
}
