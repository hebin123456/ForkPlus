using System.Windows.Input;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands.RepositoryManager
{
	public class RenameRepositoryCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Rename";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.F2);


		public KeyGesture SecondaryShortcut => null;

		public void Execute([Null] RepositoryManagerRepositoryItem itemToRename)
		{
			if (itemToRename != null)
			{
				itemToRename.IsInEditMode = true;
			}
		}
	}
}
